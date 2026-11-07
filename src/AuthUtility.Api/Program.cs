using System.Text;
using AuthUtility.Api.Chat;
using AuthUtility.Api.Configuration;
using AuthUtility.Api.IoT;
using AuthUtility.Api.Security;
using AuthUtility.Api.Weather;
using AuthUtility.Application.Interfaces;
using AuthUtility.Application.Security;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using AuthUtility.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "HelpDeskAPI";
});

// builder.WebHost.UseUrls("http://127.0.0.1:5100");

builder.WebHost.UseUrls(
    builder.Configuration["ASPNETCORE_URLS"]
    ?? "http://127.0.0.1:5100"
);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Masukkan JWT token"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ===== Database =====
// Database HelpDesk (PostgreSQL) - baca & tulis
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);
// Database PKBPayroll (SQL Server) - hanya dibaca
builder.Services.AddDbContext<PKBPayrollDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("PKBPayrollConnection")
    )
);
// Database MyPAD & RSUPPKB (SQL Server) - hanya dibaca, master persil kelapa / nanas
builder.Services.AddDbContext<MyPADDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MyPADConnection")));
builder.Services.AddDbContext<RSUPPKBDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RSUPPKBConnection")));
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IIotDeviceCredentialService, IotDeviceCredentialService>();
builder.Services.Configure<IotDeviceOptions>(builder.Configuration.GetSection("IoT"));
builder.Services.Configure<IotFirmwareOptions>(builder.Configuration.GetSection("IoT:Firmware"));
builder.Services.AddScoped<IotFirmwareService>();
builder.Services.AddScoped<AuthUtility.Api.Sensus.PersilReader>();
// Peta GIS: pembaca read-only database PostGIS WebGIS (ConnectionStrings:WebGis).
builder.Services.AddSingleton<AuthUtility.Api.WebGis.WebGisReader>();

// Database MyKTTATest (SQL Server) - hanya dibaca, data sensor ECS Monitoring (akses WAD)
builder.Services.AddDbContext<MyKTTATestDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MyKTTATestConnection")));

builder.Services.AddScoped<
    IPasswordHasher<UtlUser>,
    PasswordHasher<UtlUser>
>();

builder.Services.Configure<WeatherOptions>(
    builder.Configuration.GetSection("Weather"));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<WeatherService>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<IOptions<WeatherOptions>>()
        .Value;

    client.Timeout = TimeSpan.FromSeconds(
        Math.Clamp(options.TimeoutSeconds, 1, 60));
});

var jwtKeyValue = builder.Configuration["Jwt:Key"];
if (!JwtConfiguration.IsStrongEnough(jwtKeyValue))
{
    throw new InvalidOperationException(
        "Konfigurasi Jwt:Key wajib diisi dengan minimal 32 byte random secret.");
}

var jwtKey = Encoding.UTF8.GetBytes(jwtKeyValue!);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            IssuerSigningKey =
                new SymmetricSecurityKey(jwtKey)
        };

        // WebSocket (SignalR) tidak bisa memakai header Authorization,
        // jadi token live chat dikirim lewat query string ?access_token=...
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = token;

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var rawUserId = context.Principal?
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!int.TryParse(rawUserId, out var userId))
                {
                    context.Fail("Token user tidak valid.");
                    return;
                }

                using var scope = context.HttpContext.RequestServices.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = await db.Users
                    .AsNoTracking()
                    .Where(x => x.Id == userId)
                    .Select(x => new
                    {
                        x.Active,
                        x.MustChangePassword,
                        x.LastPasswordChangedAt,
                        GroupActive = x.Group != null && x.Group.Active
                    })
                    .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

                if (user == null || !user.Active || !user.GroupActive)
                {
                    context.Fail("User atau group sudah tidak aktif.");
                    return;
                }

                var tokenStamp = context.Principal?
                    .FindFirst(AuthClaimNames.PasswordChangedTicks)?.Value;
                if (!AuthTokenValidation.IsPasswordStampCurrent(user.LastPasswordChangedAt, tokenStamp))
                {
                    context.Fail("Token diterbitkan sebelum password terakhir diubah.");
                    return;
                }

                if (context.Principal?.Identity is ClaimsIdentity identity)
                {
                    foreach (var claim in identity.FindAll(AuthClaimNames.MustChangePassword).ToList())
                        identity.RemoveClaim(claim);

                    identity.AddClaim(new Claim(
                        AuthClaimNames.MustChangePassword,
                        user.MustChangePassword ? "true" : "false"));
                }
            }
        };
    });

builder.Services.AddAuthorization();

// ===== Live Chat (realtime) =====
builder.Services.AddSignalR();
builder.Services.AddSingleton<ChatPresenceTracker>();
builder.Services.AddSingleton<AuthUtility.Api.Presence.AppPresenceTracker>();

var configuredCorsOrigins = builder.Configuration
    .GetSection("Cors:Origins")
    .GetChildren()
    .Select(section => section.Value);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
       FrontendCorsPolicy.Configure(policy, configuredCorsOrigins);
    });
});

var app = builder.Build();

// app.UseSwagger();
// app.UseSwaggerUI();

// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }

if (app.Environment.IsDevelopment()
    || app.Environment.IsEnvironment("IoTTest"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        var error = feature?.Error;

        app.Logger.LogError(
            error,
            "Unhandled exception. TraceId: {TraceId}",
            context.TraceIdentifier
        );

        await context.Response.WriteAsJsonAsync(new
        {
            message = "Terjadi kesalahan pada server.",
            detail = app.Environment.IsDevelopment()
                ? (error?.InnerException?.Message ?? error?.Message)
                : null,
            traceId = context.TraceIdentifier
        });
    });
});

app.UseAuthentication();
app.UseMiddleware<PasswordChangeRequiredMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<AuthUtility.Api.Presence.PresenceHub>("/hubs/presence");

app.MapGet("/", () => Results.Redirect("/swagger"));

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    var hasher = scope.ServiceProvider
        .GetRequiredService<IPasswordHasher<UtlUser>>();

    if (await db.Database.CanConnectAsync())
    {
        var adminGroup = await db.Groups
            .FirstOrDefaultAsync(x => x.Code == "ADMIN");

        if (adminGroup != null)
        {
            var bootstrapUserId = builder.Configuration["BootstrapAdmin:UserId"] ?? "admin";
            var admin = await db.Users
                .FirstOrDefaultAsync(x => x.UserId == bootstrapUserId);

            var bootstrapPassword = builder.Configuration["BootstrapAdmin:Password"];

            if (admin == null && !string.IsNullOrWhiteSpace(bootstrapPassword))
            {
                var policyError = PasswordPolicy.Validate(bootstrapPassword);
                if (policyError != null)
                {
                    app.Logger.LogError(
                        "BootstrapAdmin:Password tidak memenuhi policy password: {Reason}. Admin bootstrap tidak dibuat.",
                        policyError);
                }
                else
                {
                    var u = new UtlUser
                    {
                        UserId = bootstrapUserId,
                        Name = builder.Configuration["BootstrapAdmin:Name"] ?? "Administrator",
                        Email = builder.Configuration["BootstrapAdmin:Email"] ?? "admin@local",
                        GroupId = adminGroup.Id,
                        Active = true,
                        MustChangePassword = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    u.PasswordHash = hasher.HashPassword(u, bootstrapPassword);
                    db.Users.Add(u);
                    await db.SaveChangesAsync();
                }
            }
        }
    }
}

app.Run();

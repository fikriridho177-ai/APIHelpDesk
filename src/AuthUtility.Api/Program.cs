using System.Text;
using AuthUtility.Api.Chat;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using AuthUtility.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

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

var jwtKey = Encoding.UTF8.GetBytes(
    builder.Configuration["Jwt:Key"]!
);

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
            }
        };
    });

builder.Services.AddAuthorization();

// ===== Live Chat (realtime) =====
builder.Services.AddSignalR();
builder.Services.AddSingleton<ChatPresenceTracker>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
       policy.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:5174",
                "http://127.0.0.1:5174",
                "http://192.168.9.23:5173"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
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
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

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
            var admin = await db.Users
                .FirstOrDefaultAsync(x => x.UserId == "admin");

            if (admin == null)
            {
                var u = new UtlUser
                {
                    UserId = "admin",
                    Name = "Administrator",
                    Email = "admin@local",
                    GroupId = adminGroup.Id,
                    Active = true,
                    MustChangePassword = true,
                    CreatedAt = DateTime.UtcNow
                };

                u.PasswordHash = hasher.HashPassword(
                    u,
                    "Admin@123"
                );

                db.Users.Add(u);

                await db.SaveChangesAsync();
            }
        }
    }
}

app.Run();
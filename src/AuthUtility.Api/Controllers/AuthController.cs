using System.Security.Claims;
using AuthUtility.Application.DTOs.Auth;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    IPasswordHasher<UtlUser> hasher,
    IJwtTokenService jwt
) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest r)
    {
        var u = await db.Users
            .Include(x => x.Group)
            .FirstOrDefaultAsync(
                x => x.UserId == r.UserId &&
                     x.Active
            );

        if (
            u == null ||
            hasher.VerifyHashedPassword(
                u,
                u.PasswordHash,
                r.Password
            ) == PasswordVerificationResult.Failed
        )
        {
            return Unauthorized(new
            {
                message = "User ID atau password salah"
            });
        }

        return Ok(
            new LoginResponse(
                jwt.CreateToken(u),
                u.Id,
                u.UserId,
                u.Name,
                u.Group?.Name ?? "",
                u.MustChangePassword
            )
        );
    }


    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> Change(
        ChangePasswordRequest r
    )
    {
        var id = int.Parse(
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            )!
        );

        var u = await db.Users.FindAsync(id);

        if (u == null)
        {
            return NotFound();
        }

        if (
            hasher.VerifyHashedPassword(
                u,
                u.PasswordHash,
                r.OldPassword
            ) == PasswordVerificationResult.Failed
        )
        {
            return BadRequest(new
            {
                message = "Password lama salah"
            });
        }

        if (r.NewPassword.Length < 8)
        {
            return BadRequest(new
            {
                message = "Password baru minimal 8 karakter"
            });
        }

        db.PasswordHistories.Add(
            new UtlPasswordHistory
            {
                UserId = u.Id,
                PasswordHash = u.PasswordHash
            }
        );

        u.PasswordHash =
            hasher.HashPassword(
                u,
                r.NewPassword
            );

        u.MustChangePassword = false;
        u.LastPasswordChangedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "Password berhasil diganti"
        });
    }


    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            id = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            ),

            userId = User.Identity?.Name,

            role = User.FindFirstValue(
                ClaimTypes.Role
            )
        });
    }
}
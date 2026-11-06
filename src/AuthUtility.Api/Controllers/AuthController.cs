using System.Security.Claims;
using AuthUtility.Application.DTOs.Auth;
using AuthUtility.Application.Interfaces;
using AuthUtility.Application.Security;
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
                     x.Active &&
                     x.Group != null &&
                     x.Group.Active
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

        var u = await db.Users
            .Include(x => x.Group)
            .FirstOrDefaultAsync(x => x.Id == id && x.Active && x.Group != null && x.Group.Active);

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

        var policyError = PasswordPolicy.Validate(r.NewPassword);
        if (policyError != null)
        {
            return BadRequest(new
            {
                message = policyError
            });
        }

        if (hasher.VerifyHashedPassword(u, u.PasswordHash, r.NewPassword) != PasswordVerificationResult.Failed)
        {
            return BadRequest(new
            {
                message = "Password baru tidak boleh sama dengan password sebelumnya"
            });
        }

        var previousHashes = await db.PasswordHistories
            .AsNoTracking()
            .Where(x => x.UserId == u.Id)
            .OrderByDescending(x => x.ChangedAt)
            .Take(5)
            .Select(x => x.PasswordHash)
            .ToListAsync();

        if (previousHashes.Any(hash =>
                hasher.VerifyHashedPassword(u, hash, r.NewPassword) != PasswordVerificationResult.Failed))
        {
            return BadRequest(new
            {
                message = "Password baru tidak boleh memakai salah satu dari lima password sebelumnya"
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
            message = "Password berhasil diganti",
            token = jwt.CreateToken(u)
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

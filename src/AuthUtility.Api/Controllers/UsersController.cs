using AuthUtility.Application.DTOs.Users;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

[ApiController]
[Route("api/utility/users")]
[Authorize]
public class UsersController(
    AppDbContext db,
    IPasswordHasher<UtlUser> hasher,
    IPermissionService permissionService
) : ControllerBase
{
    private const string MenuCode = "MASTER_USER";
    private const string DefaultPassword = "User@123";

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!await permissionService.HasPermissionAsync(User, MenuCode, MenuPermission.Read)) return Forbid();

        var users = await db.Users
            .Include(x => x.Group)
            .OrderBy(x => x.UserId)
            .Select(x => new UserDto(
                x.Id,
                x.UserId,
                x.Name,
                x.Email,
                x.Phone,
                x.Nik,
                x.RegNo,
                x.DeptId,
                x.CompanyId,
                x.GroupId,
                x.Group!.Name,
                x.Active,
                x.MustChangePassword
            ))
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest r)
    {
        if (!await permissionService.HasPermissionAsync(User, MenuCode, MenuPermission.Create)) return Forbid();

        if (await db.Users.AnyAsync(x => x.UserId == r.UserId))
            return Conflict(new { message = "User ID sudah ada" });

        if (r.RegNo.HasValue && await db.Users.AnyAsync(x => x.RegNo == r.RegNo))
            return Conflict(new { message = "RegNo karyawan sudah terdaftar sebagai user" });

        var user = new UtlUser
        {
            UserId = r.UserId,
            Name = r.Name,
            Email = r.Email ?? "",
            Phone = r.Phone,
            Nik = r.Nik,
            RegNo = r.RegNo,
            DeptId = r.DeptId,
            CompanyId = r.CompanyId,
            GroupId = r.GroupId,
            Active = r.Active,
            MustChangePassword = true
        };

        var password = string.IsNullOrWhiteSpace(r.Password) ? DefaultPassword : r.Password;
        user.PasswordHash = hasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Ok(new { id = user.Id, message = "User berhasil dibuat" });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateUserRequest r)
    {
        if (!await permissionService.HasPermissionAsync(User, MenuCode, MenuPermission.Update)) return Forbid();

        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound();

        // User ID hanya diganti jika diisi dan berbeda dari sebelumnya.
        var newUserId = r.UserId?.Trim();
        if (!string.IsNullOrWhiteSpace(newUserId) && newUserId != user.UserId)
        {
            if (await db.Users.AnyAsync(x => x.Id != id && x.UserId == newUserId))
                return Conflict(new { message = "User ID sudah digunakan user lain" });

            user.UserId = newUserId;
        }

        if (r.RegNo.HasValue && await db.Users.AnyAsync(x => x.Id != id && x.RegNo == r.RegNo))
            return Conflict(new { message = "RegNo karyawan sudah terdaftar sebagai user lain" });

        user.Name = r.Name;
        user.Email = r.Email ?? "";
        user.Phone = r.Phone;
        user.Nik = r.Nik;
        user.RegNo = r.RegNo;
        user.DeptId = r.DeptId;
        user.CompanyId = r.CompanyId;
        user.GroupId = r.GroupId;
        user.Active = r.Active;

        // Password hanya diganti jika diisi; password lama disimpan ke riwayat.
        if (!string.IsNullOrWhiteSpace(r.Password))
        {
            db.PasswordHistories.Add(new UtlPasswordHistory
            {
                UserId = user.Id,
                PasswordHash = user.PasswordHash
            });

            user.PasswordHash = hasher.HashPassword(user, r.Password);
            user.MustChangePassword = true;
        }

        await db.SaveChangesAsync();

        return Ok(new { message = "User berhasil diubah" });
    }

    // Tidak menghapus data, hanya menonaktifkan user.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await permissionService.HasPermissionAsync(User, MenuCode, MenuPermission.Delete)) return Forbid();

        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.Active = false;
        await db.SaveChangesAsync();

        return Ok(new { message = "User dinonaktifkan" });
    }
}

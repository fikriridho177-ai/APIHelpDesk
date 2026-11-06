using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
namespace AuthUtility.Infrastructure.Security;
public class JwtTokenService(IConfiguration cfg) : IJwtTokenService
{
 public string CreateToken(UtlUser user)
 {
  var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
  var claims = new[]
  {
   new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
   new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
   new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
   new Claim(ClaimTypes.Name, user.UserId),
   new Claim(ClaimTypes.Role, user.Group?.Code ?? ""),
   new Claim(AuthClaimNames.MustChangePassword, user.MustChangePassword ? "true" : "false"),
   new Claim(AuthClaimNames.PasswordChangedTicks, AuthTokenValidation.ToPasswordStamp(user.LastPasswordChangedAt))
  };
  var token = new JwtSecurityToken(
   cfg["Jwt:Issuer"],
   cfg["Jwt:Audience"],
   claims,
   expires: DateTime.UtcNow.AddMinutes(30),
   signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
  return new JwtSecurityTokenHandler().WriteToken(token);
 }
}

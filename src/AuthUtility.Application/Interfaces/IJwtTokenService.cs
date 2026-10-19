using AuthUtility.Domain.Entities;
namespace AuthUtility.Application.Interfaces;
public interface IJwtTokenService { string CreateToken(UtlUser user); }

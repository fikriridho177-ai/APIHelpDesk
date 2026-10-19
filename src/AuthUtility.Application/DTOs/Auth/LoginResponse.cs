namespace AuthUtility.Application.DTOs.Auth;
public record LoginResponse(string Token, int Id, string UserId, string Name, string Group, bool MustChangePassword);

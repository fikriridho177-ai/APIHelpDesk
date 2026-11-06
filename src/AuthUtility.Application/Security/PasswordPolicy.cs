namespace AuthUtility.Application.Security;

public static class PasswordPolicy
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 128;

    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "Password wajib diisi.";

        if (password.Length < MinimumLength)
            return $"Password minimal {MinimumLength} karakter.";

        if (password.Length > MaximumLength)
            return $"Password maksimal {MaximumLength} karakter.";

        if (password.Any(char.IsWhiteSpace))
            return "Password tidak boleh mengandung spasi.";

        if (!password.Any(char.IsUpper))
            return "Password harus mengandung huruf besar.";

        if (!password.Any(char.IsLower))
            return "Password harus mengandung huruf kecil.";

        if (!password.Any(char.IsDigit))
            return "Password harus mengandung angka.";

        if (!password.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)))
            return "Password harus mengandung simbol.";

        return null;
    }
}

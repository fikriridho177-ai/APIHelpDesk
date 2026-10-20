namespace AuthUtility.Domain.Entities;

// Tipe baris UtlMenu.
//   GROUP  = parent / dropdown di navbar
//   MENU   = halaman
//   WIDGET = kotak di Portal (halaman utama), tidak punya route dan tidak tampil di navbar
public static class MenuTypes
{
    public const string Group = "GROUP";
    public const string Menu = "MENU";
    public const string Widget = "WIDGET";

    // Nilai yang tidak dikenal dianggap MENU (perilaku lama).
    public static string Normalize(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            Group => Group,
            Widget => Widget,
            _ => Menu
        };
}

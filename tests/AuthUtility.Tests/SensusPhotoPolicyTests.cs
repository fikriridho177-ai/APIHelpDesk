using AuthUtility.Api.Sensus;

namespace AuthUtility.Tests;

public class SensusPhotoPolicyTests
{
    [Theory]
    [InlineData("foto.png", "image/png")]
    [InlineData("FOTO.PNG", "image/png")]
    public void AcceptsPngWithMatchingContent(string name, string mime) =>
        Assert.Null(SensusPhotoPolicy.Validate(name, mime, 8, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));

    [Fact]
    public void RejectsExecutablesSpoofedMimeEmptyAndOversizedFiles()
    {
        var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        Assert.NotNull(SensusPhotoPolicy.Validate("bad.exe", "image/png", 8, png));
        Assert.NotNull(SensusPhotoPolicy.Validate("bad.png", "image/jpeg", 8, png));
        Assert.NotNull(SensusPhotoPolicy.Validate("bad.png", "image/png", 8, new byte[] { 77, 90, 0, 0 }));
        Assert.NotNull(SensusPhotoPolicy.Validate("empty.png", "image/png", 0, png));
        Assert.NotNull(SensusPhotoPolicy.Validate("big.png", "image/png", 5 * 1024 * 1024 + 1, png));
    }

    [Fact]
    public void AllowsOnlyOwnSubmittedOrRevisionPhotoChanges()
    {
        Assert.True(SensusPhotoPolicy.CanEdit(1, 1, "SUBMITTED", true));
        Assert.True(SensusPhotoPolicy.CanEdit(1, 1, "REVISI", true));
        Assert.False(SensusPhotoPolicy.CanEdit(1, 2, "SUBMITTED", true));
        Assert.False(SensusPhotoPolicy.CanEdit(1, 1, "SUBMITTED", false));
        Assert.False(SensusPhotoPolicy.CanEdit(1, 1, "APPROVED", true));
        Assert.False(SensusPhotoPolicy.CanEdit(1, 1, "REJECTED", true));
    }

    [Fact]
    public void NeverDeletesExternalOrTraversingOrOtherSensusPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "sensus-photo-policy");
        var name = Guid.NewGuid().ToString("N") + ".png";
        Assert.NotNull(SensusPhotoPolicy.OwnedPath(root, 7, $"/uploads/sensus/2026/11/7/{name}"));
        Assert.Null(SensusPhotoPolicy.OwnedPath(root, 7, $"/uploads/sensus/2026/11/8/{name}"));
        Assert.Null(SensusPhotoPolicy.OwnedPath(root, 7, "/uploads/sensus/../../settings.json"));
        Assert.Null(SensusPhotoPolicy.OwnedPath(root, 7, "https://example.com/photo.png"));
        Assert.Null(SensusPhotoPolicy.OwnedPath(root, 7, "/uploads/sensus/2026/11/7/executable.exe"));
    }
}

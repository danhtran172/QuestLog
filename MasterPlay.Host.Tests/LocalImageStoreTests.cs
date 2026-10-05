using MasterPlay.Infrastructure.Integrations.Images;

namespace MasterPlay.Host.Tests;

/// <summary>DOMAIN_RULES §12 — anh thu vien khong thuoc admin luu tren may.</summary>
public sealed class LocalImageStoreTests
{
    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "jpg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 }, "png")]
    [InlineData(new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' }, "gif")]
    [InlineData(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' }, "webp")]
    [InlineData(new byte[] { 0, 0, 0, 0x20, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'a', (byte)'v', (byte)'i', (byte)'f' }, "avif")]
    public void Recognises_supported_raster_images(byte[] bytes, string extension) =>
        Assert.Equal(extension, LocalImageStore.SniffExtension(bytes));

    [Fact]
    public void Rejects_svg_and_html_because_they_are_served_from_the_app_origin()
    {
        Assert.Null(LocalImageStore.SniffExtension("<svg xmlns=\"http://www.w3.org/2000/svg\"><script/></svg>"u8));
        Assert.Null(LocalImageStore.SniffExtension("<!doctype html><script>alert(1)</script>"u8));
        Assert.Null(LocalImageStore.SniffExtension([]));
    }

    [Fact]
    public void Decodes_base64_data_urls_only()
    {
        Assert.Equal(new byte[] { 1, 2, 3 }, LocalImageStore.TryDecodeDataUrl("data:image/png;base64,AQID"));
        Assert.Null(LocalImageStore.TryDecodeDataUrl("data:image/svg+xml,<svg/>"));
        Assert.Null(LocalImageStore.TryDecodeDataUrl("data:image/png;base64,@@not-base64@@"));
        Assert.Null(LocalImageStore.TryDecodeDataUrl("https://example.com/a.png"));
    }

    [Theory]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950e-banner-0123456789abcdef.jpg", true)]
    [InlineData("game-poster-abc.webp", true)]
    [InlineData("../QuestLog.db", false)]
    [InlineData("..\\config.json", false)]
    [InlineData("a/b.jpg", false)]
    [InlineData("image.svg", false)]
    [InlineData("image.jpg.tmp", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Validates_file_names_before_touching_the_disk(string? fileName, bool valid) =>
        Assert.Equal(valid, LocalImageStore.IsValidFileName(fileName));

    [Fact]
    public void Path_traversal_urls_never_resolve()
    {
        Assert.False(LocalImageStore.TryResolveFile("/local-images/../QuestLog.db", out _));
        Assert.False(LocalImageStore.TryResolveFile("/local-images/..%2FQuestLog.db", out _));
        Assert.False(LocalImageStore.TryResolveFile("https://example.com/x.jpg", out _));
    }

    [Fact]
    public void Storage_follows_recorded_role_and_never_guesses_for_unseen_accounts()
    {
        var owners = new Dictionary<string, bool> { ["user:admin"] = true, ["user:member"] = false };

        Assert.True(ImageStorageOwners.Resolve(owners, "user:admin"));
        Assert.False(ImageStorageOwners.Resolve(owners, "user:member"));
        // Tai khoan chua dang nhap tren Host nay: khong biet role -> nguoi goi bo qua.
        Assert.Null(ImageStorageOwners.Resolve(owners, "user:unseen"));
        // Guest/legacy khong co role -> luon tren may.
        Assert.False(ImageStorageOwners.Resolve(owners, "guest:local"));
        Assert.False(ImageStorageOwners.Resolve(owners, "legacy:local"));
    }

    [Theory]
    // Thu vien admin: chi URL Cloudinary cua app la xong.
    [InlineData(true, "https://res.cloudinary.com/demo/image/upload/v1/QuestLog/Covers/x.jpg", false)]
    [InlineData(true, "/local-images/x-banner-0123.jpg", true)]
    [InlineData(true, "https://cdn.akamai.steamstatic.com/steam/apps/1/header.jpg", true)]
    // Thu vien khac: chi anh tren may la xong — ke ca anh Cloudinary cua kho cung phai tai ve.
    [InlineData(false, "/local-images/x-banner-0123.jpg", false)]
    [InlineData(false, "https://res.cloudinary.com/demo/image/upload/v1/QuestLog/Covers/x.jpg", true)]
    [InlineData(false, "data:image/png;base64,AQID", true)]
    [InlineData(false, "", false)]
    [InlineData(true, null, false)]
    public void Needs_work_until_the_image_sits_in_the_owner_storage(bool usesCloud, string? path, bool needsWork) =>
        Assert.Equal(needsWork, GameImageSync.NeedsWork(usesCloud, path));
}

using Microsoft.Extensions.FileProviders;

namespace MasterPlay.Host;

/// <summary>
/// Ban cai: giao dien da build (MasterPlay.Web/dist) nam o wwwroot canh exe va Host
/// phuc vu luon, cung origin voi /api. Dev khong co thu muc nay — Vite :5173 lo
/// giao dien va proxy /api sang Host, nen ca hai ham deu khong lam gi.
/// </summary>
internal static class BundledWebUi
{
    private const string IndexFile = "index.html";

    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "wwwroot");

    public static void UseBundledWebUi(this WebApplication app)
    {
        if (!File.Exists(Path.Combine(Root, IndexFile))) return;

        var files = new PhysicalFileProvider(Root);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            OnPrepareResponse = context =>
            {
                // index.html phai luon tai lai de sau khi cap nhat WebView2 lay dung
                // bo asset moi; asset co hash trong ten nen cache lau duoc.
                context.Context.Response.Headers.CacheControl =
                    context.File.Name == IndexFile ? "no-cache" : "public, max-age=31536000, immutable";
            },
        });

        // Duong dan giao dien khong phai file (SPA) -> index.html. /api va
        // /local-images khong bao gio roi vao day de loi 404 cua API van la 404.
        app.MapFallback(async context =>
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments("/api") || path.StartsWithSegments("/local-images"))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";
            await context.Response.SendFileAsync(files.GetFileInfo(IndexFile));
        });
    }
}

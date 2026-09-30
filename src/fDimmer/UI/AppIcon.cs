namespace fDimmer.UI;

/// <summary>
/// Иконка приложения. Маленькая для заголовков окон читается из самого exe — так же, как её
/// видит проводник. Крупная для шапки главного окна берётся из встроенного app.ico: из exe
/// Windows отдаёт только 32×32.
/// </summary>
internal static class AppIcon
{
    private static readonly Lazy<Icon?> Cached = new(() =>
        Icon.ExtractAssociatedIcon(Application.ExecutablePath));

    public static Icon? Default => Cached.Value;

    /// <summary>Картинка иконки нужного размера в пикселях (ближайший кадр из app.ico).</summary>
    public static Bitmap? Large(int size)
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("fDimmer.app.ico");
        if (stream is null) return Default?.ToBitmap();

        using var icon = new Icon(stream, size, size);
        return icon.ToBitmap();
    }
}

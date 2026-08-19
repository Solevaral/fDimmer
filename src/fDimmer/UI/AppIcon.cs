namespace fDimmer.UI;

/// <summary>
/// Иконка окон приложения. Читается из самого exe — так же, как её видит проводник, —
/// а не из отдельного файла: тогда single-file публикация не может её потерять.
/// </summary>
internal static class AppIcon
{
    private static readonly Lazy<Icon?> Cached = new(() =>
        Icon.ExtractAssociatedIcon(Application.ExecutablePath));

    public static Icon? Default => Cached.Value;
}

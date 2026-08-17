using fDimmer.Interop;
using fDimmer.UI;

namespace fDimmer;

internal static class Program
{
    private const string SingleInstanceName = @"Local\fDimmer.SingleInstance";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceName, out var isFirstInstance);
        if (!isFirstInstance) return;

        ApplicationConfiguration.Initialize();

        // Страховка: экран не должен остаться затемнённым, если приложение упало
        // или сеанс завершается до штатной выгрузки контекста.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => RestoreScreen();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => RestoreScreen();
        Application.ThreadException += (_, e) =>
        {
            RestoreScreen();
            MessageBox.Show(Core.Strings.UnexpectedError(e.Exception), "fDimmer",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        };

        using var context = new TrayApplicationContext();
        Application.Run(context);

        RestoreScreen();
    }

    /// <summary>
    /// Снимает цветовой эффект напрямую, минуя движки: вызывается из аварийных обработчиков,
    /// где состояние приложения уже могло разрушиться.
    /// </summary>
    private static void RestoreScreen()
    {
        try
        {
            var identity = Magnification.Identity();
            Magnification.MagSetFullscreenColorEffect(ref identity);
        }
        catch (DllNotFoundException)
        {
            // magnification.dll отсутствует — затемнять было нечем, восстанавливать нечего.
        }
        catch (EntryPointNotFoundException)
        {
        }
    }
}

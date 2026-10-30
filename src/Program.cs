using HocoKeepAlive.UI;

namespace HocoKeepAlive;

internal static class Program
{
    private const string AppMutexName = @"Local\KeepAlive_SingleInstance_Mutex_9E6F1B51";

    [STAThread]
    private static void Main(string[] args)
    {
        // Enforce single running application instance
        using var mutex = new Mutex(true, AppMutexName, out bool createdNew);
        if (!createdNew)
        {
            // Another instance is already running
            return;
        }

        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        }
        catch
        {
            // Fallback if already initialized by runtime
        }

        ApplicationConfiguration.Initialize();

        bool startMinimized = args.Any(a => string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(a, "-m", StringComparison.OrdinalIgnoreCase));

        Application.Run(new KeepAliveContext(startMinimized));
    }
}

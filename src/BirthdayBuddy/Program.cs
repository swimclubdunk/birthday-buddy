using BirthdayBuddy.Core;
using BirthdayBuddy.Ui;

namespace BirthdayBuddy;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            SingleInstance.SignalExisting();
            return;
        }

        Log.FilePath = Path.Combine(BirthdayStore.DefaultDirectory, "log.txt");
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Error("Unhandled UI exception.", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Fatal exception.", e.ExceptionObject as Exception);

        ApplicationConfiguration.Initialize();
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        bool startInTray = args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
        Application.Run(new TrayApp(startInTray, instance));
    }
}

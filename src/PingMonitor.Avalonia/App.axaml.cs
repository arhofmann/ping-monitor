using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PingMonitor.Core;

namespace PingMonitor.Avalonia
{
    internal sealed partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            IClassicDesktopStyleApplicationLifetime desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (desktop != null)
            {
                // This is where the program is wired together - to measure ping or data rate with a different technique, swap it here.
                ConnectionMonitor monitor = new ConnectionMonitor(new IcmpPinger(), new NetworkInterfaceTrafficCounter());
                CsvOutageLog log = new CsvOutageLog(CsvOutageLog.DefaultPath());
                desktop.MainWindow = new MainWindow(monitor, log);
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}

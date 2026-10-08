// Ping Monitor - records when the internet connection goes down and when it is back up.
// To run: dotnet run --project src/PingMonitor.Avalonia

using System;
using Avalonia;

namespace PingMonitor.Avalonia
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // The IDEs' preview designer looks for this method too - under exactly this name.
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
        }
    }
}

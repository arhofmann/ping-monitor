using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using PingMonitor.Core;

namespace PingMonitor.Avalonia
{
    // The main window. It only displays and passes input on - measuring and calculating happen in the core.
    // Layout and appearance are in MainWindow.axaml, the colors of the light and the dark appearance in App.axaml.
    // Title, status and statistics are bound to the DisplayState that RefreshView sets anew as the DataContext every time.
    internal sealed partial class MainWindow : Window
    {
        private readonly ConnectionMonitor monitor;
        private readonly CsvOutageLog log;
        private readonly GraphModel graphModel;
        private readonly ObservableCollection<OutageListRow> outageRows = new ObservableCollection<OutageListRow>();
        private readonly DispatcherTimer uiTimer = new DispatcherTimer();

        // The row of the ongoing outage; its duration keeps counting up in the list.
        private OutageListRow openOutageRow;

        public MainWindow(ConnectionMonitor monitor, CsvOutageLog log)
        {
            this.monitor = monitor;
            this.log = log;
            graphModel = new GraphModel(monitor);
            InitializeComponent();

            graph.Model = graphModel;
            graph.ViewChanged += delegate { ShowRange(); };
            rangeBox.ItemsSource = TimeRange.All;
            rangeBox.SelectedItem = TimeRange.Default;
            thresholdBox.Value = monitor.OutageThreshold;
            outageList.ItemsSource = outageRows;
            ShowLogState(true);

            uiTimer.Interval = TimeSpan.FromSeconds(1);
            uiTimer.Tick += delegate { RefreshView(); };

            monitor.Changed += MonitorChanged;
            monitor.OutageStarted += MonitorOutageStarted;
            monitor.OutageEnded += MonitorOutageEnded;

            UpdateControls();
            RefreshView();
        }

        private async void StartClicked(object sender, RoutedEventArgs e)
        {
            if (!monitor.CanStart) return;
            string host = (targetBox.Text ?? "").Trim();
            if (host.Length == 0)
            {
                await MessageDialog.ShowAsync(this, "Please enter a target, for example 1.1.1.1.");
                return;
            }

            // A field that was typed empty has no value - the smallest interval applies then.
            int seconds = (int)(intervalBox.Value ?? intervalBox.Minimum);
            StartResult result = await monitor.StartAsync(host, seconds * 1000);
            // Whoever starts a measurement wants to watch it - even if the graph was showing the past until now.
            if (result == StartResult.Started)
            {
                graphModel.Follow();
                graph.InvalidateVisual();
            }
            if (result != StartResult.HostNotFound) return;
            await MessageDialog.ShowAsync(this, "The name “" + host + "” could not be resolved.\n\nAn IP address such as 1.1.1.1 works without DNS.");
        }

        private void StopClicked(object sender, RoutedEventArgs e)
        {
            monitor.Stop();
        }

        private void RangeChanged(object sender, SelectionChangedEventArgs e)
        {
            TimeRange range = rangeBox.SelectedItem as TimeRange;
            if (range == null) return;
            graphModel.Show(range.Span);
            graph.InvalidateVisual();
        }

        // The box names a time range only while the graph shows exactly that one. Once the mouse has zoomed
        // or moved the window, the box is empty and its placeholder says so.
        private void ShowRange()
        {
            TimeRange shown = null;
            foreach (TimeRange range in TimeRange.All)
                if (graphModel.IsFollowing && range.Span == graphModel.Window) shown = range;
            rangeBox.SelectedItem = shown;
        }

        private void ThresholdChanged(object sender, NumericUpDownValueChangedEventArgs e)
        {
            if (e.NewValue.HasValue) monitor.OutageThreshold = (int)e.NewValue.Value;
        }

        // The switch applies to the whole application, so that dialogs follow as well.
        private void ThemeChosen(object sender, RoutedEventArgs e)
        {
            if (((RadioButton)sender).IsChecked != true) return;
            ThemeVariant theme = ThemeVariant.Default;
            if (sender == lightTheme) theme = ThemeVariant.Light;
            if (sender == darkTheme) theme = ThemeVariant.Dark;
            Application.Current.RequestedThemeVariant = theme;
        }

        private async void ClearClicked(object sender, RoutedEventArgs e)
        {
            if (monitor.Samples.Count == 0 && monitor.Outages.Count == 0) return;
            bool confirmed = await MessageDialog.AskAsync(this,
                "Clear the history, the statistics and the list of outages?\n\nOutages that have already been logged stay in the CSV file.");
            if (!confirmed) return;

            monitor.Clear();
            outageRows.Clear();
        }

        private async void ExportClicked(object sender, RoutedEventArgs e)
        {
            if (monitor.Samples.Count == 0)
            {
                await MessageDialog.ShowAsync(this, "There are no measurements yet.");
                return;
            }

            FilePickerFileType csv = new FilePickerFileType("CSV file");
            csv.Patterns = new string[] { "*.csv" };
            FilePickerSaveOptions options = new FilePickerSaveOptions();
            options.SuggestedFileName = HistoryExport.SuggestFileName(DateTime.Now);
            options.DefaultExtension = "csv";
            options.FileTypeChoices = new FilePickerFileType[] { csv };

            using (IStorageFile file = await StorageProvider.SaveFilePickerAsync(options))
            {
                if (file == null) return;
                try
                {
                    HistoryExport.Write(file.TryGetLocalPath(), monitor.Samples);
                }
                catch (Exception ex)
                {
                    await MessageDialog.ShowAsync(this, "The export failed:\n" + ex.Message);
                }
            }
        }

        private async void LogLabelClicked(object sender, EventArgs e)
        {
            string path = log.FilePath;
            try
            {
                // Only Windows Explorer can select the file in its folder right away; everywhere else the folder simply opens.
                if (OperatingSystem.IsWindows() && File.Exists(path)) Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else await Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(Path.GetDirectoryName(path)));
            }
            catch (Exception)
            {
                // The folder could not be opened - the path is in the status bar anyway, and in full in its tooltip.
            }
        }

        private void MonitorChanged(object sender, EventArgs e)
        {
            UpdateControls();
            RefreshView();
        }

        private void MonitorOutageStarted(object sender, OutageEventArgs e)
        {
            openOutageRow = new OutageListRow(e.Outage);
            outageRows.Insert(0, openOutageRow);
        }

        private void MonitorOutageEnded(object sender, OutageEventArgs e)
        {
            openOutageRow.Refresh();
            openOutageRow = null;
            ShowLogState(log.TryAppend(e.Outage));
        }

        private void ShowLogState(bool written)
        {
            logLabel.SetPath(written
                ? "Outage log: "
                : "Outage log could not be written (file open?): ", log.FilePath);
        }

        private void UpdateControls()
        {
            bool running = monitor.IsRunning;
            // Start and Stop share one place. While a name is being resolved, Start stays there but cannot be pressed again.
            startButton.IsVisible = !running;
            startButton.IsEnabled = monitor.CanStart;
            stopButton.IsVisible = running;
            targetBox.IsEnabled = !running;
            intervalBox.IsEnabled = !running;
            uiTimer.IsEnabled = running;
        }

        private void RefreshView()
        {
            DisplayState state = new DisplayState(monitor, DateTime.UtcNow);
            DataContext = state;
            ShowLevel(state.Level);

            if (openOutageRow != null) openOutageRow.Refresh();
            graph.InvalidateVisual();
        }

        // The status block takes its colors from a style class named after the level - see MainWindow.axaml.
        private void ShowLevel(StatusLevel level)
        {
            foreach (StatusLevel candidate in Enum.GetValues(typeof(StatusLevel)))
                statusBlock.Classes.Set(candidate.ToString(), candidate == level);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            // Write an outage still in progress to the log before exiting.
            monitor.Stop();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            monitor.Changed -= MonitorChanged;
            monitor.OutageStarted -= MonitorOutageStarted;
            monitor.OutageEnded -= MonitorOutageEnded;
            uiTimer.Stop();
            base.OnClosed(e);
        }
    }
}

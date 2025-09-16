using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shapes;

namespace FastDownloader
{

    public static class FastDownloaderDialog
    {
        public static Action<bool>? OnCompleted { get; set; }
        public static Action<bool>? OnCancelled { get; set; }
        public static Action<int>? OnProgress { get; set; }

        public static void ShowDialogFromContent(string jsonContent)
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            var win = new FastDownloader.MainWindow();
            win.LoadPackageInfoFromFileContent(jsonContent);
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;

            // This ensures closing win will shutdown app
            app.MainWindow = win;
            // Wire up the events to static callbacks
            win.CompletedCallback += (s, result) => OnCompleted?.Invoke(result);
            win.CancelledCallback += (s, cancelled) => OnCancelled?.Invoke(cancelled);
            win.ProgressCallback += (s, progress) => OnProgress?.Invoke(progress);
            win.Loaded += async (s, e) =>
            {
                bool result = await win.StartDownloadFiles();
                Console.WriteLine($"[INFO] Download completed. Success: {result}");
                // Optionally close the window or Application here
            };

            app.Run(win);
        }
    }
}

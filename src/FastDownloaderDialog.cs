using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace FastDownloader
{
    public static class FastDownloaderDialog
    {
        public static void ShowDialog(string jsonFilePath, Action<bool>? onCompleted = null)
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            var win = new FastDownloader.MainWindow();
            win.LoadPackageInfoFromFile(jsonFilePath);
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;

            // This ensures closing win will shutdown app
            app.MainWindow = win;

            win.Loaded += async (s, e) =>
            {
                bool result = await win.StartDownloadFiles();
                Console.WriteLine($"[INFO] Download completed. Success: {result}");
                // Optionally close the window or Application here
            };

            app.Run(win);
        }
        public static void ShowDialogFromContent(string jsonContent, Action<bool>? onCompleted = null)
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            var win = new FastDownloader.MainWindow();
            win.LoadPackageInfoFromFileContent(jsonContent);
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;

            // This ensures closing win will shutdown app
            app.MainWindow = win;

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

using System.IO;
using System.Windows;

namespace FastDownloader
{
    public static class FastDownloaderDialog
    {
        /// <summary>
        /// Shows the FastDownloader dialog, loading the package list from the given JSON file.
        /// </summary>
        public static void ShowDialog(string jsonFilePath)
        {
            // Load JSON as string
            string json = System.IO.File.ReadAllText(jsonFilePath);


            // Create window and load downloads
            var win = new MainWindow();
            win.LoadJsonPackageInfo(json); // or any setup/init method you use
            win.ShowDialog();
        }
    }
}

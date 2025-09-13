using System;
using FastDownloader;
class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Path to the JSON file with download info
        string jsonFile = @"C:\path\to\downloads.json";

        // Call the static launcher in your DLL
        FastDownloader.FastDownloaderDialog.ShowDialog(jsonFile);
    }
}

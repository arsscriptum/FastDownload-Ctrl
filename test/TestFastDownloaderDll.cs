using FastDownloader;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;      // Important

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            if (System.Windows.Application.Current == null)
                new System.Windows.Application();

            string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string jsonFile = Path.Combine(exeDir, "json", "bmw-advanced-tools.json");
            Stopwatch _sw = new Stopwatch(); 
            _sw.Start();
            if (!System.IO.File.Exists(jsonFile))
            {
                Console.Error.WriteLine("[Error] JSON file not found: " + jsonFile);
                Environment.Exit(1);
            }
            string jsonString = System.IO.File.ReadAllText(jsonFile);
            // Pass a callback for when the download is complete
            FastDownloader.FastDownloaderDialog.ShowDialogFromContent(jsonString, (bool success) =>
            {
                Console.WriteLine($"[INFO] Download completed. Success: {success}");
                _sw.Stop();
                string globalDuration = $"{_sw.Elapsed.TotalSeconds:F2}s";
      
                MessageBox.Show($"All downloads completed in {globalDuration}.");
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[Exception] " + ex.Message);
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(2);
        }
    }
}

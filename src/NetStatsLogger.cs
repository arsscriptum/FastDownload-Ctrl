
using System;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

#pragma warning disable CS0169
#pragma warning disable CS0067
#pragma warning disable CS0414

namespace FastDownloader
{
    [Flags]
    public enum LogChannel
    {
        None = 0,
        Gui = 1,
        File = 2,
        Events = 4
    }

    internal static partial class NetStatsLogger
    {

        public static LogChannel Channels = LogChannel.File; // Default

        private static readonly object LockObj = new();
        public static event Action<string> LogAppended;
        private static RichTextBox rtb;

        private static readonly string LogDirectory = @"C:\Tmp\FastDownloader\Logs";
        private static readonly string DebugDirectory = @"C:\Tmp\FastDownloader";
        private static readonly string DebugFilePath = @"C:\Tmp\FastDownloader\trace.log";
        private static readonly string DebugExceptionLogFilePath = @"C:\Tmp\FastDownloader\exception.log";

        private static readonly string LogFilePath;
        public enum InstallerLogLevel
        {
            Muted = 0,
            Verbose = 1,
            Info = 2,
            Important = 3,
            Warning = 4,
            Error = 5
        }

        public static class LogColorHelper
        {
            public static Color GetColorFromType(InstallerLogLevel level)
            {
                switch (level)
                {
                    case InstallerLogLevel.Muted: return Color.DimGray;
                    case InstallerLogLevel.Verbose: return Color.Purple;
                    case InstallerLogLevel.Info: return Color.Brown;
                    case InstallerLogLevel.Important: return Color.MediumBlue;
                    case InstallerLogLevel.Warning: return Color.Red;
                    case InstallerLogLevel.Error: return Color.Crimson;
                    default: return Color.Violet;
                }
            }
        }

        static NetStatsLogger()
        {

            try
            {

          
                LogFilePath = Path.Combine(LogDirectory, $"logs.txt");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Logger initialization failed: {ex.Message}");
            }
        }

        public static void LogException(Exception error_ex, string inClass = "")
        {
            Debug.WriteLine(error_ex.Message);

               try
                {

                    System.IO.File.AppendAllText(DebugExceptionLogFilePath, $"[{DateTime.Now}] Exception in {inClass}:\n{error_ex}\n\n");

                    //lock (LockObj)
                    {
                        System.IO.File.AppendAllText(DebugFilePath, $"[{DateTime.Now}] Exception in {inClass}:\n{error_ex}\n\n");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Logger write failed: {ex.Message}");
                }

            }
            public static void SetRTB(RichTextBox _rtb)
            {
                rtb = _rtb;
            }

            public static void Log(string message)
            {

                try
                {
                    string logMsg = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                    // lock (LockObj)
                    {
                        WriteLogString(logMsg, InstallerLogLevel.Info, false, true);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Logger write failed: {ex.Message}");
                }

            }


            public static void LogError(string msg, bool clear = false) => WriteLogString(msg, InstallerLogLevel.Error, clear, true);
            public static void LogWarning( string msg, bool clear = false) => WriteLogString( msg, InstallerLogLevel.Warning, clear, false);
            public static void LogVerbose(string msg, bool clear = false) => WriteLogString(msg, InstallerLogLevel.Verbose, clear, false);
            public static void LogImportant(string msg, bool clear = false) => WriteLogString(msg, InstallerLogLevel.Important, clear, false);


            public static void SetLogFilePath(string path)
            {

            }
            public static void WriteLogString(string message, InstallerLogLevel type = InstallerLogLevel.Info, bool clear = false, bool bold = false)
            {
                Debug.WriteLine(message);

            // Log to file
            if (Channels.HasFlag(LogChannel.File))
              {
                try
                 {
                   string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}";
                    System.IO.File.AppendAllText(LogFilePath, line);
                }
               catch (Exception ex)
                {
                 Debug.WriteLine($"Logger write to file failed: {ex.Message}");
               }
             }

        }



    }
}


#pragma warning restore CS0169
#pragma warning restore CS0067
#pragma warning restore CS0414
using FastDownloader;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static System.Net.WebRequestMethods;
using static System.Windows.Forms.AxHost;


namespace FastDownloader
{
    public static class Config
    {
        public static int MaxDegreeOfParallelism { get; set; } = 30;

        public static HttpClient HttpClient { get; } = new HttpClient();
    }
    public enum DownloadState
    {
        Idle = 0,
        Initialized = 1,
        TransferInProgress = 2,
        Completed = 3,
        ErrorOccured =4 ,
        MAX = 5
    }
    public class File
    {
        public int Index { get; set; }
        public string DownloadUrl { get; set; } = "";
        public string ParentFolder { get; set; } = "";
    }
    public class SegmentInfo
    {
        public string PackageId { get; set; } = "";
        public string PartId { get; set; } = "";
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public long Size { get; set; }
        public string Url { get; set; } = "";
        public string Hash { get; set; } = "";
        public string HashAlgorithm { get; set; } = "";
        public bool Encrypted { get; set; }
        public string Version { get; set; } = "";
    }
    public class PackageInfo
    {
        public string PackageId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Name { get; set; } = "";
        public long Size { get; set; }
        public string IndexUrl { get; set; } = "";
        public string Hash { get; set; } = "";
        public string HashAlgorithm { get; set; } = "";
        public bool Encrypted { get; set; }
        public string Version { get; set; } = "";
        public int NumParts { get; set; }
        public List<SegmentInfo> Parts { get; set; } = new();
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<FileDownloadItem> Segments { get; set; } = new();
        public string DownloadPath { get; set; } = "C:\\tmp";
        public string PackagePath { get; set; } = "C:\\tmp\\bmw_installer_package.rar.aes";
        public DateTime StartTime { get; set; } = DateTime.Now;
        public Stopwatch GlobalTimer { get; set; } = new Stopwatch();
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Change your property implementations to notify:
        private int _downloadedSegments = 0;
        public int DownloadedSegments
        {
            get => _downloadedSegments;
            set { _downloadedSegments = value; OnPropertyChanged(nameof(DownloadedSegments)); OnPropertyChanged(nameof(SummaryDownloaded)); OnPropertyChanged(nameof(SummaryDownloaded)); }
        }

        private int _totalSegments = 0;
        public int TotalSegments
        {
            get => _totalSegments;
            set { _totalSegments = value; OnPropertyChanged(nameof(TotalSegments)); OnPropertyChanged(nameof(SummaryDownloaded)); OnPropertyChanged(nameof(SummaryDownloaded)); }
        }

        public string SummaryDownloaded => $"{DownloadedSegments} / {TotalSegments} segments";

        // Add this:
        public double OverallProgress => TotalSegments == 0 ? 0 : (double)DownloadedSegments / TotalSegments * 100;

        public MainWindow()
        {
             
            //InitializeComponent();
            DataContext = this;

            LoadPackageInfoFromResource();

        }
        public static string ToHumanReadableSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return $"{kb:F2} KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return $"{mb:F2} MB";
            double gb = mb / 1024.0;
            if (gb < 1024) return $"{gb:F2} GB";
            double tb = gb / 1024.0;
            return $"{tb:F2} TB";
        }

        private void LoadPackageInfoFromResource()
        {
            try
            {
                // Resource name must match what is embedded
                string resourceName = "FastDownloader.res.bmw-advanced-tools.json";
                string jsonString = "";

                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                using (var reader = new StreamReader(stream))
                {
                    jsonString = reader.ReadToEnd();
                }

                var files = LoadJsonPackageInfo(jsonString);
                foreach (var f in files)
                {
                    Segments.Add(f);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load JSON file: {ex.Message}");
            }
        }


        public List<FileDownloadItem> LoadJsonPackageInfo(string jsonString)
        {
            
            var model = JsonSerializer.Deserialize<PackageInfo>(jsonString);

            if (model == null || model.Parts == null)
                throw new Exception("JSON is invalid or missing listparts.");

            // Convert JSON URLs into FileDownloadItem objects
            var files = model.Parts.Select(p => new FileDownloadItem
            {
                SegmentNumber = p.Index + 1,
                Url = p.Url,
                FileName = Path.GetFileName(p.Url),
                Status = "Pending",
                State = DownloadState.Idle,
                Size = p.Size,
                SizeString = ToHumanReadableSize(p.Size),
                Progress = 0
            }).ToList();

            return files;
        }

        public async Task<FileInfo> DownloadFile( File file, string rootDirectory, CancellationToken ct = default, Action<DownloadState>? updateState = null, Action<int, long, long>? reportProgress = null, Action<int>? startTransfer = null, Action<int>? transferCompleted = null)
        {
            var sw = Stopwatch.StartNew();
            updateState?.Invoke(DownloadState.Initialized);
            
            string fileName = System.IO.Path.GetFileName(new Uri(file.DownloadUrl).LocalPath);
            string downloadPath = System.IO.Path.Combine(rootDirectory, fileName);

            using var response = await Config.HttpClient.GetAsync(file.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(downloadPath)!);


            using var httpStream = await response.Content.ReadAsStreamAsync();


            using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long totalRead = 0;
            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            bool xferStarted = false;


            int httpStreamRead = await httpStream.ReadAsync(buffer, 0, buffer.Length, ct);
            while (httpStreamRead > 0)
            {
                if(xferStarted == false)
                {
                    xferStarted = true;
                    startTransfer?.Invoke(file.Index);
                    updateState?.Invoke(DownloadState.TransferInProgress);
                    reportProgress?.Invoke(0, 0, 0);
                }
                httpStreamRead = await httpStream.ReadAsync(buffer, 0, buffer.Length, ct);


                fileStream.Write(buffer, 0, httpStreamRead);
                totalRead += httpStreamRead;
                long tmpRemaining = totalBytes - totalRead;

                if (totalBytes > 0)
                {
                    int percent = (int)(totalRead * 100 / totalBytes);
                    reportProgress?.Invoke(percent, tmpRemaining, totalBytes);
                }
                else
                {
                    reportProgress?.Invoke(0, 0, 0);
                }
            }

 

 
            sw.Stop();
            //


            // Format download time
           
            updateState?.Invoke(DownloadState.Completed);

            return new FileInfo(downloadPath);
        }

     

       
        public async Task<List<FileInfo>> DownloadFiles(IEnumerable<File> fileList, string rootDirectory, CancellationToken ct = default)
        {
            var fileInfoBag = new ConcurrentBag<FileInfo>();
            var semaphore = new SemaphoreSlim(Config.MaxDegreeOfParallelism);
       

            var tasks = fileList.Select(async file =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var fileName = System.IO.Path.GetFileName(new Uri(file.DownloadUrl).LocalPath);
                    var segmentId = file.Index;
                    var matchingItem = Segments.FirstOrDefault(f => f.FileName == fileName);

                    Action<DownloadState> updateState = (state) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (matchingItem != null)
                            {
                                matchingItem.State = state;
                                switch (state)
                                {
                                    case DownloadState.Idle:
                                        break;
                                    case DownloadState.Initialized:
                                        var durationQueuedWait = matchingItem.FileTimer.Elapsed;
                                        string durationQueuedWaitStr = $" Queued for {durationQueuedWait.TotalMilliseconds:F2} ms";
                                        matchingItem.Status = durationQueuedWaitStr;
                                        break;
                                   case DownloadState.TransferInProgress:
                                        matchingItem.TransferStarted = true;
                                        matchingItem.StartTime = DateTime.Now;
                                        var durationQueued = matchingItem.FileTimer.Elapsed;

                                        string durationQueuedStr = $" Started after {durationQueued.TotalMilliseconds:F2} ms";
                                        matchingItem.Status = durationQueuedStr;
                                        matchingItem.FileTimer.Restart();
                                        break;
                                    case DownloadState.Completed:
                                        var duration = matchingItem.FileTimer.Elapsed;
                                        string durationStr = $" Transfered in {duration.TotalMilliseconds:F2} ms";
                                        matchingItem.Status = durationStr;
                                        matchingItem.Progress = 100;
                                        matchingItem.RemainingString = ToHumanReadableSize(0);

                                        matchingItem.Remaining = 0;
                                        break;
                                    case DownloadState.ErrorOccured:
                                        string errorstr = $" ❌ Error Occured {matchingItem.LastErrorId}";
                                        matchingItem.Status = errorstr;
                                        break;
                                    default:
                                        break;


                                }
                                
                            }
                        });
                    };
                    Action<int, long, long> reportProgress = (percent, remaining, totalBytes) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (matchingItem != null)
                            {

                                string xferingStr = $" Transferring...";
                                matchingItem.Status = xferingStr;


                                long downloadedBytes = totalBytes - remaining;
                                double elapsedMilliseconds = matchingItem.FileTimer.Elapsed.Milliseconds;
                                matchingItem.Remaining = remaining;

                                // Calculate speed in KB/s, protect against division by zero
                                double speedKbelapsedMilliseconds = (elapsedMilliseconds > 0)
                                           ? downloadedBytes / 1024.0 / elapsedMilliseconds
                                           : 0;
                                double speedKbSec = speedKbelapsedMilliseconds * 1000;
                                matchingItem.Speed = $"{speedKbSec:F1} KB/s";
                                matchingItem.Progress = percent;
                                matchingItem.RemainingString = ToHumanReadableSize(remaining);
               
                                

                            }
                        });
                    };
                    Action<int> startTransfer = (segmentId) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (matchingItem != null)
                            {
                                matchingItem.FileTimer.Start();

                            }
                        });
                    };
                    Action<int> transferCompleted = (segmentId) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (matchingItem != null)
                            {
                                matchingItem.State = DownloadState.Completed;
                                DateTime nowDoneTime = DateTime.Now;
                                matchingItem.CompletionTime = nowDoneTime;
                                matchingItem.Remaining = 0; 
                                matchingItem.RemainingString = ToHumanReadableSize(0);
                                matchingItem.Progress = 100;


                            }
                        });
                    };
                    var fileInfo = await DownloadFile(file, rootDirectory, ct, updateState, reportProgress, startTransfer, transferCompleted);
                    fileInfoBag.Add(fileInfo);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            return fileInfoBag.ToList();
        }

        private async void StartDownload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GlobalTimer.Start();
                StartTime = DateTime.Now;

                string downloadRoot = this.DownloadPath;
                Directory.CreateDirectory(downloadRoot);

                var segs = Segments.Select(f => new File
                {
                    Index = f.SegmentNumber,
                    DownloadUrl = f.Url,
                    ParentFolder = ""
                }).ToList();

                var downloadedFiles = await DownloadFiles(segs, downloadRoot);

                foreach (var fileInfo in downloadedFiles)
                {
                    var matchingFile = Segments.FirstOrDefault(f => f.FileName == fileInfo.Name);
                    if (matchingFile != null)
                    {
                        matchingFile.Progress = 100;
                        // Status already updated in DownloadFile
                    }
                }

                GlobalTimer.Stop();
                string globalDuration = $"{GlobalTimer.Elapsed.TotalSeconds:F2}s";

                MessageBox.Show($"All downloads completed in {globalDuration}.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading files: {ex.Message}");
            }
        }


    }


    public class FileDownloadItem : DependencyObject
    {
        public int SegmentNumber { get; set; } = 0;
        public int LastErrorId { get; set; } = 0;
        public DownloadState State { get; set; } = DownloadState.Idle;
        public long Size { get; set; } = 0;
        public long Remaining { get; set; } = 0;
        public bool TransferStarted { get; set; } = false;
        public string Url { get; set; } = "";
        public string FileName { get; set; } = "";
        public DateTime CompletionTime { get; set; } = new DateTime();
        public DateTime StartTime { get; set; } = new DateTime();
        public Stopwatch FileTimer { get; set; } = new Stopwatch();

        public string Status
        {
            get => (string)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }
        public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register("Status", typeof(string), typeof(FileDownloadItem), new PropertyMetadata(""));

        public string Speed
        {
            get => (string)GetValue(SpeedProperty);
            set => SetValue(SpeedProperty, value);
        }
        public static readonly DependencyProperty SpeedProperty =
        DependencyProperty.Register("Speed", typeof(string), typeof(FileDownloadItem), new PropertyMetadata(""));


        public int Progress
        {
            get => (int)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register("Progress", typeof(int), typeof(FileDownloadItem), new PropertyMetadata(0));



        public string SizeString
        {
            get => (string)GetValue(SizeStringProperty);
            set => SetValue(SizeStringProperty, value);
        }
        public static readonly DependencyProperty SizeStringProperty =
        DependencyProperty.Register("SizeString", typeof(string), typeof(FileDownloadItem), new PropertyMetadata(""));



        public string RemainingString
        {
            get => (string)GetValue(RemainingStringProperty);
            set => SetValue(RemainingStringProperty, value);
        }
        public static readonly DependencyProperty RemainingStringProperty =
        DependencyProperty.Register("RemainingString", typeof(string), typeof(FileDownloadItem), new PropertyMetadata(""));



    }

}

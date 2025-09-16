using CommandLine;
using FastDownloader;
using Microsoft.Diagnostics.Tracing.Analysis.JIT;
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
using System.Windows.Media.Animation;
using static System.Net.WebRequestMethods;
using static System.Windows.Forms.AxHost;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Header;

#pragma warning disable CS8632
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
        Pausing = 3,
        Paused = 4,
        Cancelling = 5,
        Cancelled = 6,
        Completed = 7,
        ErrorOccured = 8,
        MAX = 9
    }
    public enum FastDownloaderStatus
    {
        Idle = 0,
        Initialized = 1,
        TransferInProgress = 2,
        Paused = 3,
        Cancelled = 4,
        Completed = 5,
        ErrorOccured = 6,
        MAX = 7
    }

    public class File
    {
        public int Index { get; set; }
        public string DownloadUrl { get; set; } = "";
        public string ParentFolder { get; set; } = "";
        public long Size { get; set; }
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
        
        public bool GlobalTransferStarted { get; set; } = false;
        public Stopwatch GlobalTimer { get; set; } = new Stopwatch();
        public Stopwatch EtaTimer { get; set; } = new Stopwatch();
        public NetworkStatsHelper NetworkStatisticsHelper = new NetworkStatsHelper();
        public event PropertyChangedEventHandler PropertyChanged;
        private bool _detailsShown = false;
        private FastDownloaderStatus CurrentStatus;

        private double _collapsedHeight = 240; // Set this to your "collapsed" height
        private double _maxExpandedHeight = 560;  // Set this to your "expanded" height
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Change your property implementations to notify:
        private int _downloadedSegments = 0;
        public int DownloadedSegments
        {
            get => _downloadedSegments;
            set { _downloadedSegments = value; OnPropertyChanged(nameof(DownloadedSegments)); OnPropertyChanged(nameof(SummaryDownloaded)); OnPropertyChanged(nameof(SummaryDownloaded)); OnPropertyChanged(nameof(OverallProgress)); }
        }
        public string RemainingTotal
        {
            get
            {
                long totalRemaining = Segments.Sum(s => s.Remaining);
                return ToHumanReadableSize(totalRemaining);
            }
        }

        private int _totalSegments = 0;
        public int TotalSegments
        {
            get => _totalSegments;
            set { _totalSegments = value; OnPropertyChanged(nameof(TotalSegments)); OnPropertyChanged(nameof(SummaryDownloaded)); OnPropertyChanged(nameof(SummaryDownloaded)); }
        }
        private string _transferRate = "0 KB/s";
        public string TransferRate
        {
            get => _transferRate;
            set { _transferRate = value; OnPropertyChanged(nameof(TransferRate)); }
        }
        private string _timeLeft = "";
        public string TimeLeft
        {
            get => _timeLeft;
            set { _timeLeft = value; OnPropertyChanged(nameof(TimeLeft)); }
        }
        private readonly System.Windows.Threading.DispatcherTimer _uiTimer = new System.Windows.Threading.DispatcherTimer();

        private string _packageSize;
        public string PackageSize
        {
            get => _packageSize;
            set { _packageSize = value; OnPropertyChanged(nameof(PackageSize)); }
        }

        private string _packageRemaining;
        public string PackageRemaining
        {
            get => _packageRemaining;
            set { _packageRemaining = value; OnPropertyChanged(nameof(PackageRemaining)); }
        }

        public string SummaryDownloaded => $"{DownloadedSegments} / {TotalSegments} segments";

        public double OverallProgress
        {
            get
            {
                if (Segments.Count == 0)
                    return 0;
                // Average of all per-file progress (each 0–100)
                double total = Segments.Sum(seg => seg.Progress);
                return total / Segments.Count;
            }
        }


        public MainWindow()
        {
             
            InitializeComponent();
            DataContext = this; 
            _uiTimer.Interval = TimeSpan.FromMilliseconds(500);
            _uiTimer.Tick += (s, e) => UpdateStats();
            _uiTimer.Start();
            CurrentStatus = FastDownloaderStatus.Idle;
            this.Closed += (s, e) => Application.Current?.Shutdown();
            ShowDetailsControls(false);
            //NetStatsLogger.Log($"FastDownloader Initialize ");
            //LoadPackageInfoFromResource();

        }
        private void UpdateStats()
        {
            if (GlobalTransferStarted)
            {
                long totalDownloaded = Segments.Sum(f => f.Size - f.Remaining);

                double seconds = EtaTimer.Elapsed.TotalSeconds;



                double totalSpeed = Segments.Where(f => f.State == DownloadState.TransferInProgress).Sum(f => f.SpeedValue);
                int count = Segments.Count(f => f.State == DownloadState.TransferInProgress);

                double averageSpeed = (count > 0) ? totalSpeed / count : 0;

                double averageSpeedBytes = averageSpeed;
                double averageSpeedKB = averageSpeed / 1024.0;
                double averageSpeedMB = averageSpeedKB / 1024.0;

                if (averageSpeedMB > 1)
                {
                    TransferRate = $"{averageSpeedMB:F2} MB/s";
                }
                else
                {
                    TransferRate = $"{averageSpeedKB:F2} KB/s";
                }
                long totalRemaining = Segments.Where(f => f.State == DownloadState.TransferInProgress).Sum(f => f.Remaining);
            
                double etaSeconds = (totalSpeed > 0) ? totalRemaining / averageSpeedBytes : 0;

                NetworkStatisticsHelper.Update();
                TimeLeft = NetworkStatisticsHelper.GetETAString();

                PackageSize = ToHumanReadableSize(NetworkStatisticsHelper.TotalBytes);
                PackageRemaining = ToHumanReadableSize(NetworkStatisticsHelper.RemainingBytes());

                OnPropertyChanged(nameof(RemainingTotal));
                OnPropertyChanged(nameof(SummaryDownloaded));
                OnPropertyChanged(nameof(RemainingTotal));
            }
 
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
#if TEST_ERROR
                string resourceName = "FastDownloader.res.bmw-advanced-tools-error.json";
#else
                string resourceName = "FastDownloader.res.bmw-advanced-tools.json";
#endif
                string jsonString = "";

                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                using (var reader = new StreamReader(stream))
                {
                    jsonString = reader.ReadToEnd();
                }

                var files = LoadJsonPackageInfo(jsonString);
                Segments.Clear();
                foreach (var f in files)
                {
                    Segments.Add(f);
                }
                TotalSegments = Segments.Count;
                DownloadedSegments = 0;

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load JSON file: {ex.Message}");
            }
        }

        public void LoadPackageInfoFromFile(string jsonFilePath)
        {
            try
            {
                // Resource name must match what is embedded
                if (!System.IO.File.Exists(jsonFilePath))
                {
                    Console.Error.WriteLine("[Error] JSON file not found: " + jsonFilePath);
                    Environment.Exit(1);
                }

                var win = new FastDownloader.MainWindow();
                string jsonString = System.IO.File.ReadAllText(jsonFilePath);


                var files = LoadJsonPackageInfo(jsonString);
                Segments.Clear();
                foreach (var f in files)
                {
                    Segments.Add(f);
                }
                TotalSegments = Segments.Count;
                DownloadedSegments = 0;

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

            NetworkStatisticsHelper.Init(model.Size);

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

        public FileInfo ProcessCancelledFile(File file, string rootDirectory, CancellationToken ct = default, Action<DownloadState>? updateState = null)
        {
            
            updateState?.Invoke(DownloadState.Cancelled);

            string fileName = System.IO.Path.GetFileName(new Uri(file.DownloadUrl).LocalPath);
            string downloadPath = System.IO.Path.Combine(rootDirectory, fileName);


            return new FileInfo(downloadPath);
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
            //NetStatsLogger.Log($"DOWNLOAD FILE {file.DownloadUrl}. {file.Size} bytes");

            using var httpStream = await response.Content.ReadAsStreamAsync();


            using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long totalRead = 0;
            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            bool fileTransferStarted = false;

            int tick = 0;
            int httpStreamRead = await httpStream.ReadAsync(buffer, 0, buffer.Length, ct);

            var segment = NetworkStatisticsHelper.AddSegment(fileName, file.Size);
            

            while (httpStreamRead > 0)
            {
                if (IsCancelled())
                {
                    updateState?.Invoke(DownloadState.Cancelled);
                    return new FileInfo(downloadPath);

                }
                if (fileTransferStarted == false)
                {
                    NetworkStatisticsHelper.Start();

                    EtaTimer.Start();
                    GlobalTransferStarted = true;

                    fileTransferStarted = true;
                    startTransfer?.Invoke(file.Index);
                    updateState?.Invoke(DownloadState.TransferInProgress);
                    reportProgress?.Invoke(0, 0, 0);
                }
                httpStreamRead = await httpStream.ReadAsync(buffer, 0, buffer.Length, ct);


                fileStream.Write(buffer, 0, httpStreamRead);
                totalRead += httpStreamRead;
                long tmpRemaining = totalBytes - totalRead;
                segment.Receive(httpStreamRead);

                if ((tick++ % 10) == 0)
                {
                    segment.Update();
                    var totalSpeedLogString = NetworkStatisticsHelper.GetTotalSpeedString();
                    var etaLogString = NetworkStatisticsHelper.GetETAString();
                    var segmentSpeedLogString = segment.GetSpeedString();

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

            }


            //NetStatsLogger.Log("DONE");
            sw.Stop();

            updateState?.Invoke(DownloadState.Completed);
            DownloadedSegments++;

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
                                    case DownloadState.Cancelled:
                                        matchingItem.Status = " ❌ Cancelled";
                                 
                                        matchingItem.CompletionTime = DateTime.Now;
                                        matchingItem.Remaining = 100;
                                        matchingItem.RemainingString = ToHumanReadableSize(0);
                                        matchingItem.Progress = 0;
                                        break;
                                    case DownloadState.Paused:
                                        matchingItem.Status = "PAUSED";
                                     
                                        matchingItem.CompletionTime = DateTime.Now;
                                        matchingItem.Remaining = 100;
                                        matchingItem.RemainingString = ToHumanReadableSize(0);
                                        matchingItem.Progress = 0;
                                        break;
                                    case DownloadState.Initialized:
                                        var durationQueuedWait = matchingItem.FileTimer.Elapsed;
                                        string durationQueuedWaitStr = $" Queued";
                                        matchingItem.Status = durationQueuedWaitStr;
                                        break;
                                   case DownloadState.TransferInProgress:
                                        matchingItem.TransferStarted = true;
                                       
                                        var durationQueued = matchingItem.FileTimer.Elapsed;

                                        string durationQueuedStr = $" Started after {durationQueued.TotalMilliseconds:F2} ms";
                                        matchingItem.Status = durationQueuedStr;
                                        matchingItem.FileTimer.Restart();
                                        break;
                                    case DownloadState.Completed:
                                        matchingItem.State = DownloadState.Completed;

                                        var duration = matchingItem.FileTimer.Elapsed;
                                        string durationStr = $" Transfered in {duration.TotalMilliseconds:F2} ms";
                                        matchingItem.Status = durationStr;
                                        matchingItem.Progress = 100;
                                        matchingItem.RemainingString = ToHumanReadableSize(0);

                                        matchingItem.Remaining = 0;
                                        break;
                                    case DownloadState.ErrorOccured:
                                        string errorstr = $" Error Occured {matchingItem.LastErrorId}";
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
                                
                                // Notify UI to update main progress bar
                                OnPropertyChanged(nameof(OverallProgress));

                                string xferingStr = $" Transferring...";
                                matchingItem.Status = xferingStr;


                                long downloadedBytes = totalBytes - remaining;
                                double elapsedMilliseconds = matchingItem.FileTimer.Elapsed.Milliseconds;
                                matchingItem.Remaining = remaining;

                                // Calculate speed in KB/s, protect against division by zero
                                double speedKbelapsedMilliseconds = (elapsedMilliseconds > 0) ? downloadedBytes / 1024.0 / elapsedMilliseconds : 0;
                                double speedMbelapsedMilliseconds = (elapsedMilliseconds > 0) ? downloadedBytes / 1024.0 / 1024.0 / elapsedMilliseconds : 0;
                                double speedKbSec = speedKbelapsedMilliseconds * 1000;
                                double speedMbSec = speedMbelapsedMilliseconds * 1000;
                                
                                matchingItem.Progress = percent;
                                matchingItem.RemainingString = ToHumanReadableSize(remaining);

                                if (speedMbSec > 1)
                                {
                                    matchingItem.Speed = $"{speedMbSec:F1} MB/s";
                                }
                                else
                                {
                                    matchingItem.Speed = $"{speedKbSec:F1} KB/s";
                                }

                                double speedBytesPerSec = (elapsedMilliseconds > 0) ? downloadedBytes / (elapsedMilliseconds / 1000.0): 0;
                                matchingItem.SpeedValue = speedBytesPerSec;

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

                                var duration = matchingItem.FileTimer.Elapsed;
                                string durationStr = $" Transfered in {duration.TotalMilliseconds:F2} ms";
                                matchingItem.Status = durationStr;
                                matchingItem.Progress = 100;
                                matchingItem.RemainingString = ToHumanReadableSize(0);

                                matchingItem.Remaining = 0;


                            }
                        });
                    };

                    // We need to set all the non completed transfers has cancelled and quit
                    if (CanContinue())
                    {
                        var fileInfo = await DownloadFile(file, rootDirectory, ct, updateState, reportProgress, startTransfer, transferCompleted);
                        fileInfoBag.Add(fileInfo);
                    }
                    else
                    {
                        if (IsCancelled())
                        {
                            var fileInfo = ProcessCancelledFile(file, rootDirectory, ct, updateState);
                            fileInfoBag.Add(fileInfo);
                            
                        }
                    }
            
                    

                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            return fileInfoBag.ToList();
        }
        private int CancelAllNonCompletedSegments()
        {
            int numCancelled = 0;
            foreach (var seg in Segments)
            {
                if (seg.State != DownloadState.Completed)
                {
                    numCancelled++;
                    seg.State = DownloadState.Cancelling;
                    seg.Status = "Cancelling...";
                    seg.Progress = 0;
                    seg.Remaining = seg.Size;
                    seg.RemainingString = ToHumanReadableSize(seg.Size);
                }
            }
            btnCancel.IsEnabled = false;
            return numCancelled;
        }
        private int PauseAllNonCompletedSegments()
        {
            int numPaused = 0;
            foreach (var seg in Segments)
            {
                if (seg.State != DownloadState.Completed)
                {
                    numPaused++;
                    seg.State = DownloadState.Pausing;
                    seg.Status = "Pausing...";
                    seg.Progress = 0;
                    seg.Remaining = seg.Size;
                    seg.RemainingString = ToHumanReadableSize(seg.Size);
                }
            }
            return numPaused;
        }


        private async void StartDownload_Click(object sender, RoutedEventArgs e)
        {
            await StartDownloadFiles();
        }

        public async Task<bool> StartDownloadFiles()
        {
            try
            {
                GlobalTimer.Start();

                string downloadRoot = this.DownloadPath;
                Directory.CreateDirectory(downloadRoot);

                var segs = Segments.Select(f => new File
                {
                    Index = f.SegmentNumber,
                    DownloadUrl = f.Url,
                    ParentFolder = "",
                    Size = f.Size
                }).ToList();

                var downloadedFiles = await DownloadFiles(segs, downloadRoot);
                GlobalTimer.Stop();
                string globalDuration = $"{GlobalTimer.Elapsed.TotalSeconds:F2}s";
                if (IsCancelled())
                {
                   
                    var dlgErr = new ErrorDialog($"All downloads cancelled after {globalDuration}.", ErrorDialogMode.Cancelled);
                    dlgErr.Owner = this; // sets parent window
                    dlgErr.ShowDialog();
                    this.Close();
                    return true;
                }

                foreach (var fileInfo in downloadedFiles)
                {
                    var matchingFile = Segments.FirstOrDefault(f => f.FileName == fileInfo.Name);
                    if (matchingFile != null)
                    {
                        matchingFile.Progress = 100;
                    }
                }
                SetCompleted();
                NetworkStatisticsHelper.SetCompleted();
                var dlg = new SuccessDialog($"All downloads completed in {globalDuration}.");
                dlg.Owner = this; // sets parent window
                dlg.ShowDialog();

                this.Close();
                return true;

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading files: {ex.Message}");
            }
            return false;
        }
        private void btnShowDetails_Click(object sender, RoutedEventArgs e)
        {
            _detailsShown = !_detailsShown;
            ShowDetailsControls(_detailsShown);
        }
        private void ShowDetailsControls(bool show)
        {
            if (show)
            {
                if (FilesListDetails.Visibility == Visibility.Collapsed)
                {
                    FilesListDetails.Visibility = Visibility.Visible;
                    btnShowDetails.Content = "<< Hide Details";
                    var newH = _collapsedHeight + 40 + (TotalSegments * 20);
                    if (newH > _maxExpandedHeight) { newH = _maxExpandedHeight; } 
                    this.Height = newH;
                }
            }
            else
            {
                if (FilesListDetails.Visibility == Visibility.Visible)
                {
                    FilesListDetails.Visibility = Visibility.Collapsed;
                    btnShowDetails.Content = "Show Details >>";
                    this.Height = _collapsedHeight;
                }

            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            if(CurrentStatus == FastDownloaderStatus.Completed)
            {
                this.Close();
            }
            else
            {
                CurrentStatus = FastDownloaderStatus.Cancelled;
                CancelAllNonCompletedSegments();
            }
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            CurrentStatus = FastDownloaderStatus.Paused;
            PauseAllNonCompletedSegments();
        
        }
        private bool IsPaused()
        {
            return (CurrentStatus == FastDownloaderStatus.Paused);
        }
        private bool IsCancelled()
        {
            return (CurrentStatus == FastDownloaderStatus.Cancelled);
        }
        private bool CanContinue()
        {
            return !(IsCancelled() || IsPaused());
        }
        private void SetCompleted()
        {
            btnCancel.Content = "Close";
            CurrentStatus = FastDownloaderStatus.Completed;
        }
        private bool IsCompleted()
        {
            return (CurrentStatus == FastDownloaderStatus.Completed);
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
      
        public Stopwatch FileTimer { get; set; } = new Stopwatch();
        public double SpeedValue { get; set; } = 0;  // Holds speed in bytes/sec for calculation


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

#pragma warning restore CS8632
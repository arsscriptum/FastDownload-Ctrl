using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using FastDownloader;


namespace FastDownloader
{
    public static class Config
    {
        public static int MaxDegreeOfParallelism { get; set; } = 30;

        public static HttpClient HttpClient { get; } = new HttpClient();
    }
   
    public class File
    {
        public string DownloadUrl { get; set; } = "";
        public string ParentFolder { get; set; } = "";
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<FileDownloadItem> FilesToDownload { get; set; } = new();
        public string DownloadPath { get; set; } = "C:\\tmp";
        public string PackagePath { get; set; } = "C:\\tmp\\bmw_installer_package.rar.aes";

        public MainWindow()
        {
            //InitializeComponent();
            DataContext = this;

            InitializeDataUrls();
        }
        private void InitializeDataUrls()
        {
            try
            {
                List<FileDownloadItem> url_list = LoadUrls();
                MessageBox.Show($"url_list.Count:\n{url_list.Count()}");
                foreach (var f in url_list)
                {
                    FilesToDownload.Add(f);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load JSON file: {ex.Message}");
            }
        }
        public List<FileDownloadItem> LoadUrls()
        {
            string[] urls = Enumerable.Range(1, 40).Select(i => $"http://arsscriptum.github.io/bmw-advanced-tools/data/project_source{i:D4}.cpp").ToArray();

            var files = urls.Select(url => new FileDownloadItem
            {
                Url = url,
                FileName = Path.GetFileName(url),
                Status = "Pending",
                Progress = 0
            }).ToList();

            return files;
      
        }

        public async Task<FileInfo> DownloadFile(
    File file,
    string rootDirectory,
    CancellationToken ct = default,
    Action<string>? updateStatus = null,
    Action<int>? reportProgress = null)
        {
            var sw = Stopwatch.StartNew();

            updateStatus?.Invoke("Downloading");

            string fileName = System.IO.Path.GetFileName(new Uri(file.DownloadUrl).LocalPath);
            string downloadPath = System.IO.Path.Combine(rootDirectory, fileName);

            using var response = await Config.HttpClient.GetAsync(file.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(downloadPath)!);

#if NETNEW
            using var httpStream = await response.Content.ReadAsStreamAsync(ct);
#else
            using var httpStream = await response.Content.ReadAsStreamAsync();

#endif 
            using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long totalRead = 0;
            var totalBytes = response.Content.Headers.ContentLength ?? -1;



#if NETNEW
            int httpStreamRead = await httpStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            while (httpStreamRead > 0)
            {
                httpStreamRead = await httpStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);

#else
            int httpStreamRead = await httpStream.ReadAsync(buffer, 0, buffer.Length, ct);
            while (httpStreamRead > 0)
            {
                httpStreamRead = await httpStream.ReadAsync(buffer, 0, buffer.Length, ct);
#endif 

                fileStream.Write(buffer, 0, httpStreamRead);
                totalRead += httpStreamRead;

                if (totalBytes > 0)
                {
                    int percent = (int)(totalRead * 100 / totalBytes);
                    reportProgress?.Invoke(percent);
                }
            }

            sw.Stop();

            // Format download time
            var duration = sw.Elapsed;
            string durationStr = $"{duration.TotalSeconds:F2}s";

            // Update status to show transfer time
            updateStatus?.Invoke($"Completed in {durationStr}");

            return new FileInfo(downloadPath);
        }

     
#if SEQUENTIAL_FOREACH
        public async Task<List<FileInfo>> DownloadFiles(IEnumerable<File> fileList, string rootDirectory, CancellationToken ct = default)
        {
            var fileInfoList = new List<FileInfo>();

            foreach (var file in fileList)
            {
                ct.ThrowIfCancellationRequested();

                var fileName = System.IO.Path.GetFileName(new Uri(file.DownloadUrl).LocalPath);

                // Find matching FileDownloadItem
                var matchingItem = Files.FirstOrDefault(f => f.FileName == fileName);

                // Define how to update the status safely on the UI thread
                Action<string> updateStatus = (status) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (matchingItem != null)
                        {
                            matchingItem.Status = status;
                        }
                    });
                };
                Action<int> reportProgress = (percent) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (matchingItem != null)
                        {
                            matchingItem.Progress = percent;
                        }
                    });
                };

                var fileInfo = await DownloadFile(file, rootDirectory, ct, updateStatus, reportProgress);
                fileInfoList.Add(fileInfo);
            }

            return fileInfoList;
        }
#endif

#if PARALLEL_FOREACH_ASYNC
        public async Task<List<FileInfo>> DownloadFiles(IEnumerable<File> fileList, string rootDirectory, CancellationToken ct = default)
        {
            var fileInfoBag = new ConcurrentBag<FileInfo>();

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = Config.MaxDegreeOfParallelism
            };

            await Parallel.ForEachAsync(fileList, parallelOptions, async (file, ctx) =>
            {
                var fileName = System.IO.Path.GetFileName(new Uri(file.DownloadUrl).LocalPath);

                // Find matching FileDownloadItem
                var matchingItem = Files.FirstOrDefault(f => f.FileName == fileName);

                // Define how to update the status safely on the UI thread
                Action<string> updateStatus = (status) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (matchingItem != null)
                        {
                            matchingItem.Status = status;
                        }
                    });
                };
                Action<int> reportProgress = (percent) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (matchingItem != null)
                        {
                            matchingItem.Progress = percent;
                        }
                    });
                };
                var fileInfo = await DownloadFile(file, rootDirectory, ctx, updateStatus, reportProgress);
                fileInfoBag.Add(fileInfo);
            });


            return fileInfoBag.ToList();
        }
#endif

//#if THROTTLED_ASYNC_NET472
       
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

                    var matchingItem = FilesToDownload.FirstOrDefault(f => f.FileName == fileName);

                    Action<string> updateStatus = (status) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (matchingItem != null)
                            {
                                matchingItem.Status = status;
                            }
                        });
                    };
                    Action<int> reportProgress = (percent) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (matchingItem != null)
                            {
                                matchingItem.Progress = percent;
                            }
                        });
                    };

                    var fileInfo = await DownloadFile(file, rootDirectory, ct, updateStatus, reportProgress);
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
        
//#endif

        private async void StartDownload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var swGlobal = Stopwatch.StartNew();

                string downloadRoot = this.DownloadPath;
                Directory.CreateDirectory(downloadRoot);

                var filesToDownload = FilesToDownload.Select(f => new File
                {
                    DownloadUrl = f.Url,
                    ParentFolder = ""
                }).ToList();

                var downloadedFiles = await DownloadFiles(filesToDownload, downloadRoot);

                foreach (var fileInfo in downloadedFiles)
                {
                    var matchingFile = FilesToDownload.FirstOrDefault(f => f.FileName == fileInfo.Name);
                    if (matchingFile != null)
                    {
                        matchingFile.Progress = 100;
                        // Status already updated in DownloadFile
                    }
                }

                swGlobal.Stop();
                string globalDuration = $"{swGlobal.Elapsed.TotalSeconds:F2}s";

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
        public string Url { get; set; } = "";
        public string FileName { get; set; } = "";

        public string Status
        {
            get => (string)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register("Status", typeof(string), typeof(FileDownloadItem), new PropertyMetadata(""));

        public int Progress
        {
            get => (int)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register("Progress", typeof(int), typeof(FileDownloadItem), new PropertyMetadata(0));
    }

}

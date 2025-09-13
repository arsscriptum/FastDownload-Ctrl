using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;

public class SegmentStats
{
    public string Name { get; set; }
    public long Size { get; set; }
    public long Remaining { get; set; }
    public long Downloaded { get; set; }
    public Stopwatch Timer { get; set; } = new Stopwatch();

    // --- Sliding window for speed ---
    private readonly Queue<(DateTime time, long downloaded)> _history = new Queue<(DateTime, long)>();
    private const double SPEED_WINDOW_SECONDS = 1.0; // 1-second window
    private double _lastSpeed = 0.0;
    private double _emaSpeed = 0;
    private const double ALPHA = 0.1; // Smoothing factor (0.1–0.3)

    public bool IsCompleted => Downloaded >= Size;

    public SegmentStats(string name, long size)
    {
        Name = name;
        Size = size;
        Downloaded = 0;
        Remaining = size;
    }

    public void Receive(long size)
    {
        Downloaded += size;
        if (Downloaded > Size) Downloaded = Size;
        Remaining  -= Size;
        if (Remaining < 0) Remaining = 0;
    }

    public void Update()
    {
        var now = DateTime.UtcNow;
        _history.Enqueue((now, Downloaded));
        while (_history.Count > 0 && (now - _history.Peek().time).TotalSeconds > SPEED_WINDOW_SECONDS)
            _history.Dequeue();

        double newSpeed = 0;
        if (_history.Count > 1)
        {
            var (startTime, startBytes) = _history.Peek();
            var duration = (now - startTime).TotalSeconds;
            var bytes = Downloaded - startBytes;
            newSpeed = (duration > 0) ? bytes / duration : 0;
        }

        // Exponential moving average
        if (_emaSpeed == 0)
            _emaSpeed = newSpeed;
        else
            _emaSpeed = ALPHA * newSpeed + (1 - ALPHA) * _emaSpeed;

        _lastSpeed = _emaSpeed;
    }

    public double GetSpeed() => _lastSpeed;

    public string GetSpeedString()
    {
        double speed = GetSpeed();
        double speedKB = speed / 1024.0;
        double speedMB = speedKB / 1024.0;
        if (speedMB > 1)
            return $"{speedMB:F2} MB/s";
        else
            return $"{speedKB:F2} KB/s";
    }
}


    public class NetworkStatsHelper
{
    public List<SegmentStats> Segments { get; set; } = new List<SegmentStats>();
    public long TotalBytes { get; set; } = 0;
 
    public bool Initialized { get; private set; } = false;
    public Stopwatch TransferTimer { get; set; } = new Stopwatch();
    public DateTime StartTime { get; set; } = DateTime.MinValue;
    private double _lastEta = 0;
    private bool _completed = false;
    public void Init(long totalBytes)
    {
        TotalBytes = totalBytes;
        _completed = false;
        Initialized = true;
    }
    public void Init() => InitFromSegments();

    public void InitFromSegments()
    {
        TotalBytes = Segments.Sum(s => s.Size);
        Initialized = true;
    }

    // Constructor with total size
    public NetworkStatsHelper()
    {
        TotalBytes = 0;
    }

    // Add a new segment (returns the created SegmentStats)
    public SegmentStats AddSegment(string name, long size)
    {
        if (!Initialized) { throw new Exception("not initialized"); }
        var segment = new SegmentStats(name, size);
        Segments.Add(segment);
        return segment;
    }
    public void Update()
    {
        foreach (var segment in Segments)
            segment.Update();
    }

    public void SetCompleted()
    {
        _completed = true;
    }
    public int TotalSegments => Segments.Count;
    public long DownloadedBytes => Segments.Sum(s => s.Downloaded);
 
    public int DownloadedSegments => Segments.Count(s => s.Downloaded >= s.Size);
    public long SegmentsSumBytes => Segments.Sum(s => s.Size);
    public long RemainingBytes()
    {
        long ret = TotalBytes - DownloadedBytes;
        if (_completed) { return 0; }
        return ret;
    }
    public void Start()
    {
        if (!Initialized) { throw new Exception("not initialized"); }
        StartTime = DateTime.Now;
        TransferTimer.Restart();
        foreach (var seg in Segments) seg.Timer.Restart();
    }

    public void Stop()
    {

        if (!Initialized) { throw new Exception("not initialized"); }
        TransferTimer.Stop();
        foreach (var seg in Segments) seg.Timer.Stop();
    }

    public double ElapsedSeconds => TransferTimer.Elapsed.TotalSeconds;

    public double GetTotalSpeed() => Segments.Where(s => !s.IsCompleted).Sum(s => s.GetSpeed());


    public string GetTotalSpeedString()
    {
        if (!Initialized) { throw new Exception("not initialized"); }
        double speed = GetTotalSpeed();
        double speedKB = speed / 1024.0;
        double speedMB = speedKB / 1024.0;
        if (speedMB > 1)
            return $"{speedMB:F2} MB/s";
        else
            return $"{speedKB:F2} KB/s";
    }

    public double GetProgress() => (TotalBytes > 0) ? (DownloadedBytes * 100.0 / TotalBytes) : 0.0;

    public double GetETASeconds()
    {
        if (!Initialized) { throw new Exception("not initialized"); }
        double speed = GetTotalSpeed();
        long remaining = TotalBytes - DownloadedBytes;
        if (_completed) { return 0; }
        return (speed > 0) ? (remaining / speed) : 0;
    }

    public double GetCappedETASeconds()
    {
        double rawEta = GetETASeconds();
        if (_lastEta == 0)
            _lastEta = rawEta;
        double capped = Math.Max(_lastEta - 2, Math.Min(_lastEta + 2, rawEta)); // ETA can change by max ±2s per update
        _lastEta = capped;
        if (_completed) { return 0; }
        return capped;
    }

    public string GetETAString()
    {
        if (!Initialized) { throw new Exception("not initialized"); }
        double etaSec = GetCappedETASeconds();
        if (DownloadedBytes == 0)
            return "Unknown";
        if (etaSec < 60)
            return $"{etaSec:F0} seconds";
        else
            return $"{(int)(etaSec / 60)} min {((int)etaSec % 60)} sec";
    }
}


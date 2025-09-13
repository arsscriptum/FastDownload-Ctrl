using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;


public class SegmentStats
{
    public string Name { get; set; }
    public long Size { get; set; }
    public long Downloaded { get; set; }
    public Stopwatch Timer { get; set; } = new Stopwatch();

    public SegmentStats(string name, long size)
    {
        Name = name;
        Size = size;
        Downloaded = 0;
    }

    public double GetSpeed()
    {
        double seconds = Timer.Elapsed.TotalSeconds;
        return (seconds > 0) ? Downloaded / seconds : 0;
    }

    public double Progress => (Size > 0) ? (Downloaded * 100.0 / Size) : 0.0;

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
    public long TotalBytes { get; private set; } = 0;
    public bool Initialized { get; private set; } = false;
    public Stopwatch TransferTimer { get; set; } = new Stopwatch();
    public DateTime StartTime { get; set; } = DateTime.MinValue;
    public void Init(long totalBytes)
    {
        TotalBytes = totalBytes;
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

    public int TotalSegments => Segments.Count;
    public long DownloadedBytes => Segments.Sum(s => s.Downloaded);
    public int DownloadedSegments => Segments.Count(s => s.Downloaded >= s.Size);
    public long SegmentsSumBytes => Segments.Sum(s => s.Size);

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

    public double GetTotalSpeed() => Segments.Sum(s => s.GetSpeed());

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
        return (speed > 0) ? (remaining / speed) : 0;
    }

    public string GetETAString()
    {
        if (!Initialized) { throw new Exception("not initialized"); }
        double etaSec = GetETASeconds();
        if (DownloadedBytes == 0)
            return "Unknown";
        if (etaSec < 60)
            return $"{etaSec:F0} seconds";
        else
            return $"{(int)(etaSec / 60)} min {((int)etaSec % 60)} sec";
    }
}


using System.Net.NetworkInformation;
using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Api.Services;

public sealed class NetworkThroughputService
{
    public const int DefaultCapacity = 90;
    public static readonly TimeSpan DefaultMinInterval = TimeSpan.FromSeconds(4);

    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly TimeSpan _minInterval;
    private readonly Func<DateTime> _clock;
    private readonly Func<(long InBytes, long OutBytes)> _readCounters;
    private readonly Queue<NetworkSampleDto> _samples = new();

    private bool _hasBaseline;
    private DateTime _baselineAtUtc;
    private long _baselineInBytes;
    private long _baselineOutBytes;

    public NetworkThroughputService()
        : this(DefaultCapacity, DefaultMinInterval, () => DateTime.UtcNow, ReadInterfaceCounters)
    {
    }

    internal NetworkThroughputService(
        int capacity,
        TimeSpan minInterval,
        Func<DateTime> clock,
        Func<(long InBytes, long OutBytes)> readCounters)
    {
        _capacity = capacity;
        _minInterval = minInterval;
        _clock = clock;
        _readCounters = readCounters;
    }

    public IReadOnlyList<NetworkSampleDto> GetSamples()
    {
        lock (_gate)
        {
            var now = _clock();
            var (inBytes, outBytes) = _readCounters();

            if (!_hasBaseline)
            {
                _baselineAtUtc = now;
                _baselineInBytes = inBytes;
                _baselineOutBytes = outBytes;
                _hasBaseline = true;
                return Snapshot();
            }

            var elapsed = (now - _baselineAtUtc).TotalSeconds;
            if (elapsed < _minInterval.TotalSeconds)
            {
                return Snapshot();
            }

            var inDelta = inBytes - _baselineInBytes;
            var outDelta = outBytes - _baselineOutBytes;
            if (inDelta < 0 || outDelta < 0)
            {
                // counters reset or wrapped mid-window; re-baseline without pushing a sample
                _baselineAtUtc = now;
                _baselineInBytes = inBytes;
                _baselineOutBytes = outBytes;
                return Snapshot();
            }

            _samples.Enqueue(new NetworkSampleDto(
                now,
                Math.Round(RatePerSecond(inBytes, _baselineInBytes, elapsed), 1),
                Math.Round(RatePerSecond(outBytes, _baselineOutBytes, elapsed), 1)));

            while (_samples.Count > _capacity)
            {
                _samples.Dequeue();
            }

            _baselineAtUtc = now;
            _baselineInBytes = inBytes;
            _baselineOutBytes = outBytes;

            return Snapshot();
        }
    }

    private IReadOnlyList<NetworkSampleDto> Snapshot() => _samples.ToArray();

    internal static double RatePerSecond(long currentBytes, long previousBytes, double elapsedSeconds) =>
        elapsedSeconds <= 0 || currentBytes < previousBytes
            ? 0
            : (currentBytes - previousBytes) / elapsedSeconds;

    public static (long InBytes, long OutBytes) ReadInterfaceCounters()
    {
        long inBytes = 0;
        long outBytes = 0;

        try
        {
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    iface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                var stats = iface.GetIPv4Statistics();
                inBytes += Math.Max(0, stats.BytesReceived);
                outBytes += Math.Max(0, stats.BytesSent);
            }
        }
        catch
        {
            // no counters reachable right now; report zero and free-fall the next poll
        }

        return (inBytes, outBytes);
    }
}
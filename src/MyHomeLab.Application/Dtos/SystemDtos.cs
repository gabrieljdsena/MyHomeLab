namespace MyHomeLab.Application.Dtos;

public record DiskMetricDto(
    string Name,
    string DriveType,
    string FileSystem,
    long TotalBytes,
    long AvailableBytes,
    long UsedBytes,
    double UsagePercent);

public record TemperatureReadingDto(
    string Component,
    string Name,
    double Celsius);

public record StorageHealthReadingDto(
    string Model,
    double? RemainingLifePercent,
    long? DataWrittenBytes,
    long? PowerOnHours);

public record NetworkSampleDto(
    DateTime SampledAtUtc,
    double DownloadBytesPerSec,
    double UploadBytesPerSec);

public record SystemMetricsDto(
    string HostName,
    string OperatingSystem,
    string Architecture,
    string RuntimeVersion,
    int ProcessorCount,
    double CpuUsagePercent,
    long TotalMemoryBytes,
    long AvailableMemoryBytes,
    long UsedMemoryBytes,
    double MemoryUsagePercent,
    long UptimeSeconds,
    DateTime SampledAtUtc,
    IReadOnlyList<DiskMetricDto> Disks,
    IReadOnlyList<TemperatureReadingDto> Temperatures,
    IReadOnlyList<StorageHealthReadingDto> StorageHealth);
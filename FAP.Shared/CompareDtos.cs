using System;
using System.Text.Json.Serialization;

namespace FAP.Shared
{
    public sealed record CompareResponseV1(
        bool Allowed,
        string? DenyReason,
        string Nickname,
        string Location,
        CompareSpecsV1? Specs
    );

    public sealed record CompareSpecsV1(
        CpuInfoV1 Cpu,
        MemoryInfoV1 Memory,
        GpuInfoV1? Gpu,
        DisplayInfoV1 Display,
        StorageInfoV1 Storage,
        NetworkInfoV1 Network,
        AudioInfoV1? Audio,
        long Score
    );

    public sealed record CpuInfoV1(string Model, int Cores, int Threads, int Bits, int MaxClockMhz);
    public sealed record MemoryInfoV1(long TotalBytes);
    public sealed record GpuInfoV1(string Model, long TotalMemoryBytes, int Count);
    public sealed record DisplayInfoV1(int PrimaryWidth, int PrimaryHeight, int TotalWidth, int TotalHeight);
    public sealed record StorageInfoV1(long TotalBytes, long FreeBytes, int Count);
    public sealed record NetworkInfoV1(long MaxLinkSpeedbps);
    public sealed record AudioInfoV1(string DeviceName);

    [JsonSourceGenerationOptions(WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(CompareResponseV1))]
    [JsonSerializable(typeof(CompareSpecsV1))]
    [JsonSerializable(typeof(CpuInfoV1))]
    [JsonSerializable(typeof(MemoryInfoV1))]
    [JsonSerializable(typeof(GpuInfoV1))]
    [JsonSerializable(typeof(DisplayInfoV1))]
    [JsonSerializable(typeof(StorageInfoV1))]
    [JsonSerializable(typeof(NetworkInfoV1))]
    [JsonSerializable(typeof(AudioInfoV1))]
    public partial class CompareJsonContext : JsonSerializerContext { }
}



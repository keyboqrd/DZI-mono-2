using System.Text.Json.Serialization;
using DZI_shared.Models;

namespace DZI_gen.Workers;

[JsonSerializable(typeof(ImageMetadata))]
[JsonSerializable(typeof(ImageStatus))]
[JsonSerializable(typeof(StatusUpdatePayload))]
internal partial class ApiJsonContext : JsonSerializerContext
{
}

/// <summary>
/// 专门用于 AOT 序列化的内部传输对象
/// </summary>
public class StatusUpdatePayload
{
    public ImageStatus Status { get; set; }
    public string? R2TilePrefix { get; set; }
    public string? ErrorMessage { get; set; }
}

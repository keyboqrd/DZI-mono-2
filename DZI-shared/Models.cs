using System.ComponentModel.DataAnnotations;

namespace DZI_shared.Models;

public class AppUser
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// RevenueCat 的 App User ID (敏感信息，仅用于后端验证)
    /// </summary>
    [Required]
    public string AppUserId { get; set; } = string.Empty;

    public DateTime? SubscriptionActiveUntil { get; set; }
    public DateTime LastSyncTime { get; set; } = DateTime.UtcNow;

    public ICollection<ProcessedImage> Images { get; set; } = new List<ProcessedImage>();
}

public class ProcessedImage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public string? R2OriginalKey { get; set; }
    public string? R2TilePrefix { get; set; }

    public ImageStatus Status { get; set; } = ImageStatus.PendingUpload;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public string? ErrorMessage { get; set; }
}

public enum ImageStatus
{
    PendingUpload,
    Uploaded,
    Processing,
    Success,
    Failed
}

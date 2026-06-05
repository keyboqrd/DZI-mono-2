namespace DZI_gen.Options;

public sealed class CloudflareR2Options
{
    public const string Position = "CloudflareR2";

    public string AccountId { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public int MaxConcurrentUploads { get; set; } = 5;
    }
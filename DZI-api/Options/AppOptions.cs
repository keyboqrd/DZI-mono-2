namespace DZI_api.Options;

public class RevenueCatOptions
{
    public const string Position = "RevenueCat";
    public string ApiKey { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
}

public class AzureContainerAppsOptions
{
    public const string Position = "AzureContainerApps";
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
}

public class CloudflareR2Options
{
    public const string Position = "CloudflareR2";
    public string AccountId { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
}

public class InternalApiOptions
{
    public const string Position = "InternalApi";
    public string ApiKey { get; set; } = string.Empty;
}

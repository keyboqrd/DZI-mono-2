using Amazon.Runtime;
using Amazon.S3;
using Azure.Identity;
using DZI_gen.Options;
using DZI_gen.Workers;
using DZI_gen;

var builder = Host.CreateApplicationBuilder(args);

// Load configuration from Azure App Configuration if endpoint is provided
string appConfigEndpoint = builder.Configuration["AzureAppConfigurationEndpoint"] ?? "";
if (!string.IsNullOrEmpty(appConfigEndpoint))
{
    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        options.Connect(new Uri(appConfigEndpoint), new DefaultAzureCredential());
    });
}

// 1. 绑定配置
builder.Services.Configure<CloudflareR2Options>(
    builder.Configuration.GetSection(CloudflareR2Options.Position));
builder.Services.Configure<TilingOptions>(
    builder.Configuration.GetSection(TilingOptions.Position));
builder.Services.Configure<InternalApiOptions>(
    builder.Configuration.GetSection(InternalApiOptions.Position));

// 2. 核心注入：配置标准的 AWS S3 客户端指向 Cloudflare 终节点
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CloudflareR2Options>>().Value;

    var s3Config = new AmazonS3Config
    {
        // 格式必须为: https://<account_id>.r2.cloudflarestorage.com
        ServiceURL = $"https://{config.AccountId}.r2.cloudflarestorage.com",
        ForcePathStyle = true, // Cloudflare R2 必须强制启用 PathStyle

        // 生产抗抖动核心配置：
        RetryMode = RequestRetryMode.Adaptive, // 自适应重试，自动应对 R2 端的频控 (Rate Limiting)
        MaxErrorRetry = 5,                     // 网络异常最大重试次数
        Timeout = TimeSpan.FromSeconds(30)     // 单个碎片图片上传超时阈值
    };

    var credentials = new BasicAWSCredentials(config.AccessKeyId, config.SecretAccessKey);
    return new AmazonS3Client(credentials, s3Config);
});

// 3. 注册业务服务与 API 客户端
builder.Services.AddHttpClient<IApiService, ApiService>();
builder.Services.AddTransient<IR2FileDownloader, R2FileDownloader>();
builder.Services.AddTransient<IImageTilerService, ImageTilerService>();
builder.Services.AddTransient<IR2TileUploader, R2TileUploader>();

// 5. 注册后台托管任务 (你的实际 Worker 进程)
builder.Services.AddHostedService<TilingProcessorJob>();

var host = builder.Build();
await host.RunAsync();
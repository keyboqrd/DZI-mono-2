using Amazon.S3;
using Amazon.S3.Model;
using DZI_gen.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;

namespace DZI_gen.Workers;

public interface IR2TileUploader
{
    Task UploadTileFolderAsync(string localFolderPath, string r2Prefix, CancellationToken cancellationToken);
}

public sealed class R2TileUploader(
    IAmazonS3 s3Client,
    IOptions<CloudflareR2Options> options,
    ILogger<R2TileUploader> logger) : IR2TileUploader
{
    private readonly IAmazonS3 _s3Client = s3Client;
    private readonly CloudflareR2Options _options = options.Value;
    private readonly ILogger<R2TileUploader> _logger = logger;

    public async Task UploadTileFolderAsync(string localFolderPath, string r2Prefix, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(localFolderPath))
        {
            throw new DirectoryNotFoundException($"本地切片目录未找到: {localFolderPath}");
        }

        // 1. 获取所有待上传的文件（包括 index.dzi 和 index_files 文件夹下的图片）
        var files = Directory.GetFiles(localFolderPath, "*.*", SearchOption.AllDirectories);
        _logger.LogInformation("找到待上传切片文件共计 {Count} 个，准备上传至 R2 路径: {Prefix}", files.Length, r2Prefix);

        // 2. 使用 Parallel.ForEachAsync 进行高并发上传
        // 这比手动维护 Task 列表和 SemaphoreSlim 更内存友好，且代码更简洁
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _options.MaxConcurrentUploads,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(files, parallelOptions, async (file, ct) =>
        {
            await UploadSingleFileWithRetryAsync(file, localFolderPath, r2Prefix, ct);
        });

        _logger.LogInformation("成功将所有切片（共 {Count} 个文件）同步至 Cloudflare R2。", files.Length);
    }


    private async Task UploadSingleFileWithRetryAsync(
        string filePath,
        string baseFolder,
        string r2Prefix,
        CancellationToken cancellationToken)
    {
        // 计算其在 R2 存储桶中的相对路径 (Key)
        string relativePath = Path.GetRelativePath(baseFolder, filePath).Replace("\\", "/");
        string r2Key = $"{r2Prefix}/{relativePath}".TrimStart('/');

        var putRequest = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = r2Key,
            FilePath = filePath,
            ContentType = GetContentType(filePath),
            // 生产优化：Cloudflare R2 不需要对 Payload 进行 SHA256 签名，禁用它可以显著减少 CPU 开销
            DisablePayloadSigning = true
        };

        // 依靠 AWS SDK 内部配置的重试策略执行，这里仅做最外层日志兜底
        try
        {
            await _s3Client.PutObjectAsync(putRequest, cancellationToken);
            _logger.LogDebug("上传成功: {Key}", r2Key);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "R2 服务端返回错误。文件: {File}, 状态码: {Code}", filePath, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传文件到 R2 发生未知网络异常: {File}", filePath);
            throw;
        }
    }

    /// <summary>
    /// 精准映射 Deep Zoom 所需的媒体类型
    /// 规避传统的 FileExtensionContentTypeProvider，保证 Native AOT 下 100% 裁剪安全
    /// </summary>
    private static string GetContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.RawEqualsIgnoreCase(".jpg") || extension.RawEqualsIgnoreCase(".jpeg") ? "image/jpeg" :
               extension.RawEqualsIgnoreCase(".png") ? "image/png" :
               extension.RawEqualsIgnoreCase(".xml") || extension.RawEqualsIgnoreCase(".dzi") ? "application/xml" :
               "application/octet-stream";
    }
}

/// <summary>
/// 高性能的高级字符串扩展，避免 AOT 场景下的隐藏装箱
/// </summary>
internal static class StringExtensions
{
    public static bool RawEqualsIgnoreCase(this string? value, string target) =>
        string.Equals(value, target, StringComparison.OrdinalIgnoreCase);
}
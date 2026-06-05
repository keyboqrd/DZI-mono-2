using Amazon.S3;
using Amazon.S3.Model;
using DZI_gen.Options;
using Microsoft.Extensions.Options;

namespace DZI_gen.Workers;
public interface IR2FileDownloader
{
    Task DownloadOriginalImageAsync(string r2Key, string localSavePath, CancellationToken cancellationToken);
}

public sealed class R2FileDownloader(
    IAmazonS3 s3Client,
    IOptions<CloudflareR2Options> options,
    ILogger<R2FileDownloader> logger) : IR2FileDownloader
{
    private readonly CloudflareR2Options _options = options.Value;

    public async Task DownloadOriginalImageAsync(string r2Key, string localSavePath, CancellationToken cancellationToken)
    {
        logger.LogInformation("开始从 R2 下载原始大图。Key: {Key} -> 本地路径: {LocalPath}", r2Key, localSavePath);

        var getRequest = new GetObjectRequest
        {
            BucketName = _options.BucketName,
            Key = r2Key
        };

        try
        {
            // 1. 发起请求获取对象（此时仅建立连接并读取了 Header，未下载完整 Body）
            using GetObjectResponse response = await s3Client.GetObjectAsync(getRequest, cancellationToken);

            // 确保本地存放临时文件的目录存在
            string? directory = Path.GetDirectoryName(localSavePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 2. 生产核心：配置高性能的 FileStream 
            // 显式指定 bufferSize (4KB)，开启 useAsync: true 充分利用 Linux 的异步 I/O 
            using var fileStream = new FileStream(
                localSavePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            // 3. 流式对接：网络流直接 copy 到文件流
            // 几 GB 的数据会以 4KB 为单位分批通过内存，内存开销恒定在极低水平，完全免除 OOM 风险
            await response.ResponseStream.CopyToAsync(fileStream, cancellationToken);

            logger.LogInformation("原始大图流式下载完成。本地文件大小: {Size} Bytes", new FileInfo(localSavePath).Length);
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogError(ex, "从 R2 下载文件失败，S3 错误码: {Code}, 状态码: {Status}", ex.ErrorCode, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "下载大图时发生未知的网络或文件 I/O 错误。");
            throw;
        }
    }
}
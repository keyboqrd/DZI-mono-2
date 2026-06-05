using DZI_gen.Workers;
using DZI_shared;
using DZI_shared.Models;

namespace DZI_gen;

public sealed class TilingProcessorJob(
    IR2FileDownloader r2Downloader,
    IImageTilerService tilerService,
    IR2TileUploader r2Uploader,
    IHostApplicationLifetime hostLifetime,
    IApiService apiService,
    ILogger<TilingProcessorJob> logger) : BackgroundService
{
    private readonly IR2FileDownloader _r2Downloader = r2Downloader;
    private readonly IImageTilerService _tilerService = tilerService;
    private readonly IR2TileUploader _r2Uploader = r2Uploader;
    private readonly IHostApplicationLifetime _hostLifetime = hostLifetime;
    private readonly IApiService _apiService = apiService;
    private readonly ILogger<TilingProcessorJob> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. 获取任务参数
        string imageIdStr = Environment.GetEnvironmentVariable("TARGET_IMAGE_ID") ?? throw new InvalidOperationException("未找到 TARGET_IMAGE_ID 环境变量");
        if (!Guid.TryParse(imageIdStr, out Guid imageId))
        {
            throw new InvalidOperationException($"TARGET_IMAGE_ID {imageIdStr} 不是有效的 Guid");
        }

        string baseScratchPath = Path.Combine(Path.GetTempPath(), "dzi-scratch");
        string localInputFile = string.Empty;
        string localOutputDir = Path.Combine(baseScratchPath, $"{imageId}_tiles_tmp");
        string dzOutputPrefix = Path.Combine(localOutputDir, "index");

        string? r2OriginalKey = null;
        Guid userId = Guid.Empty;

        try
        {
            _logger.LogInformation("=== 启动 Job 流水线，任务 ID: {JobId} ===", imageId);

            // 通过 API 获取元数据并更新为 Processing 状态
            var metadata = await _apiService.GetImageMetadataAsync(imageId);
            if (metadata == null)
            {
                _logger.LogError("无法从 API 获取 ID 为 {ImageId} 的元数据", imageId);
                return;
            }

            r2OriginalKey = metadata.R2OriginalKey;
            userId = metadata.UserId;

            if (string.IsNullOrEmpty(r2OriginalKey))
            {
                throw new InvalidOperationException("图片记录中缺少 R2OriginalKey");
            }

            await _apiService.UpdateStatusAsync(imageId, ImageStatus.Processing);

            // 动态确定后缀
            string extension = Path.GetExtension(r2OriginalKey);
            if (string.IsNullOrEmpty(extension)) extension = ".bin";
            localInputFile = Path.Combine(baseScratchPath, $"{imageId}_original{extension}");

            if (string.IsNullOrEmpty(r2OriginalKey))
            {
                throw new InvalidOperationException("图片记录中缺少 R2OriginalKey");
            }

            // 确保本地目录存在
            Directory.CreateDirectory(baseScratchPath);
            if (Directory.Exists(localOutputDir)) Directory.Delete(localOutputDir, true);
            Directory.CreateDirectory(localOutputDir);

            // 【步骤一】下载
            var downloadStart = DateTime.UtcNow;
            _logger.LogInformation("【步骤 1/3】开始从 R2 下载原图: {Key}", r2OriginalKey);
            await _r2Downloader.DownloadOriginalImageAsync(r2OriginalKey, localInputFile, stoppingToken);
            _logger.LogInformation("下载完成，耗时: {Duration}s", (DateTime.UtcNow - downloadStart).TotalSeconds);

            // 【步骤二】切片
            var tilingStart = DateTime.UtcNow;
            _logger.LogInformation("【步骤 2/3】开始生成切片: {Output}", dzOutputPrefix);
            await _tilerService.GenerateTilesAsync(localInputFile, dzOutputPrefix, stoppingToken);
            _logger.LogInformation("切片完成，耗时: {Duration}s", (DateTime.UtcNow - tilingStart).TotalSeconds);

            // 【步骤三】转储
            var uploadStart = DateTime.UtcNow;
            string r2TargetPrefix = $"tiles/{userId}/{imageId}";
            _logger.LogInformation("【步骤 3/3】开始上传切片到 R2: {Prefix}", r2TargetPrefix);
            await _r2Uploader.UploadTileFolderAsync(localOutputDir, r2TargetPrefix, stoppingToken);
            _logger.LogInformation("上传完成，耗时: {Duration}s", (DateTime.UtcNow - uploadStart).TotalSeconds);

            // 更新成功状态
            await _apiService.UpdateStatusAsync(imageId, ImageStatus.Success, r2TargetPrefix);

            _logger.LogInformation("=== 任务 {JobId} 全链路处理成功 ===", imageId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("任务 {JobId} 已被取消。", imageId);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "任务 {JobId} 在运行期间遭遇致命错误", imageId);
            
            // 更新失败状态
            await _apiService.UpdateStatusAsync(imageId, ImageStatus.Failed, errorMessage: ex.Message);
        }
        finally
        {
            // 清理本地文件
            try
            {
                if (File.Exists(localInputFile)) File.Delete(localInputFile);
                if (Directory.Exists(localOutputDir)) Directory.Delete(localOutputDir, true);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "清理临时文件时发生错误");
            }

            _logger.LogInformation("进程即将退出，释放 ACA 计算资源。");
            _hostLifetime.StopApplication();
        }
    }
}

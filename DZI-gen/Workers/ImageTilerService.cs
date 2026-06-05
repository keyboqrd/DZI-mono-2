using System;
using System.Collections.Generic;
using System.Text;

namespace DZI_gen.Workers
{
    using DZI_gen.Options;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NetVips;
    using System.IO;
    using static NetVips.Enums;

    public interface IImageTilerService
    {
        Task GenerateTilesAsync(string inputFilePath, string outputFolder, CancellationToken cancellationToken);
    }

    public sealed class ImageTilerService : IImageTilerService
    {
        private readonly TilingOptions _options;
        private readonly ILogger<ImageTilerService> _logger;

        public ImageTilerService(IOptions<TilingOptions> options, ILogger<ImageTilerService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public Task GenerateTilesAsync(string inputFilePath, string outputFolder, CancellationToken cancellationToken)
        {
            if (!File.Exists(inputFilePath))
            {
                throw new FileNotFoundException($"找不到原始大图文件: {inputFilePath}");
            }

            return Task.Run(() =>
            {
                _logger.LogInformation("启动 libvips 开始切片。输入文件: {Input}, 目标目录: {Output}", inputFilePath, outputFolder);

                // 1. 核心防御：必须指定 access 模式为 Sequential（顺序流模式）
                // 这能让 libvips 以管道流的形式边读边算，几 GB 的图片在内存里永远只占几十 MB
                using var image = Image.NewFromFile(inputFilePath, access: Enums.Access.Sequential);

                // 3. 执行物理切片
                // 注意：dzsave 会在 outputFolder 路径下生成一个 my_image.dzi 文件和一个 my_image_files 文件夹
                image.Dzsave(outputFolder, 
                    tileSize: _options.TileSize,
                    layout: _options.Layout,
                    overlap: 1,
                    container: ForeignDzContainer.Fs);

                _logger.LogInformation("libvips 切片计算完成。");
            }, cancellationToken);
        }
    }
}

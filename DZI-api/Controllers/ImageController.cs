using DZI_api.Services;
using DZI_shared;
using DZI_shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DZI_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImageController : ControllerBase
{
    private readonly DziDbContext _context;
    private readonly IRevenueCatService _revenueCatService;
    private readonly IR2Service _r2Service;
    private readonly IJobTriggerService _jobTriggerService;
    private readonly ILogger<ImageController> _logger;

    public ImageController(
        DziDbContext context,
        IRevenueCatService revenueCatService,
        IR2Service r2Service,
        IJobTriggerService jobTriggerService,
        ILogger<ImageController> logger)
    {
        _context = context;
        _revenueCatService = revenueCatService;
        _r2Service = r2Service;
        _jobTriggerService = jobTriggerService;
        _logger = logger;
    }

    [HttpPost("request-upload")]
    public async Task<IActionResult> RequestUpload([FromQuery] string appUserId, [FromQuery] string? fileName = null)
    {
        if (string.IsNullOrEmpty(appUserId)) return BadRequest("AppUserId is required.");

        // 1. Verify subscription
        bool isActive = await _revenueCatService.IsSubscriptionActiveAsync(appUserId);
        if (!isActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Active subscription required to upload images." });
        }

        // 2. Ensure user exists in DB
        var user = await _context.Users.FirstOrDefaultAsync(u => u.AppUserId == appUserId);
        if (user == null)
        {
            user = new AppUser { AppUserId = appUserId };
            _context.Users.Add(user);
            // 保存以生成用户内部 ID
            await _context.SaveChangesAsync();
        }

        // 3. Create image record
        var image = new ProcessedImage
        {
            UserId = user.Id,
            Status = ImageStatus.PendingUpload
        };

        // Determine extension
        string extension = !string.IsNullOrEmpty(fileName) ? Path.GetExtension(fileName) : ".bin";
        if (string.IsNullOrEmpty(extension)) extension = ".bin";

        // Key: originals/{internalUserId}/{imageId}{extension}
        // 注意：这里使用 user.Id (Guid) 而不是 appUserId，保护隐私且路径更规范
        image.R2OriginalKey = $"originals/{user.Id}/{image.Id}{extension}";
        
        _context.Images.Add(image);
        await _context.SaveChangesAsync();

        // 4. Generate presigned URL for upload
        var uploadUrl = _r2Service.GeneratePresignedUrl(image.R2OriginalKey, TimeSpan.FromHours(2));

        return Ok(new
        {
            ImageId = image.Id,
            UploadUrl = uploadUrl,
            Key = image.R2OriginalKey
        });
    }

    [HttpPost("{id}/complete-upload")]
    public async Task<IActionResult> CompleteUpload(Guid id)
    {
        var image = await _context.Images.FindAsync(id);
        if (image == null) return NotFound();

        if (image.Status != ImageStatus.PendingUpload)
        {
            return BadRequest("Image is not in pending upload state.");
        }

        // Update status
        image.Status = ImageStatus.Uploaded;
        image.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Trigger job
        try
        {
            await _jobTriggerService.TriggerTilingJobAsync(image.Id, image.R2OriginalKey!);
            
            image.Status = ImageStatus.Processing;
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger job for image {ImageId}", id);
            return StatusCode(500, "Failed to start processing job.");
        }

        return Ok(new { Status = image.Status });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetStatus(Guid id)
    {
        var image = await _context.Images.FindAsync(id);
        if (image == null) return NotFound();

        return Ok(new
        {
            image.Id,
            image.Status,
            image.R2TilePrefix,
            image.ErrorMessage,
            image.CreatedAt,
            image.UpdatedAt
        });
    }

    [HttpGet("user/{appUserId}")]
    public async Task<IActionResult> GetUserImages(string appUserId)
    {
        var user = await _context.Users
            .Include(u => u.Images)
            .FirstOrDefaultAsync(u => u.AppUserId == appUserId);

        if (user == null) return NotFound("User not found.");

        var images = user.Images
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                i.Id,
                i.Status,
                i.R2TilePrefix,
                i.CreatedAt,
                i.UpdatedAt,
                i.ErrorMessage
            });

        return Ok(images);
    }
}

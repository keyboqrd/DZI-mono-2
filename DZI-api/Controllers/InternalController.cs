using DZI_api.Options;
using DZI_shared;
using DZI_shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DZI_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InternalController : ControllerBase
{
    private readonly DziDbContext _context;
    private readonly InternalApiOptions _options;
    private readonly ILogger<InternalController> _logger;

    public InternalController(
        DziDbContext context,
        IOptions<InternalApiOptions> options,
        ILogger<InternalController> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    private bool IsAuthorized()
    {
        if (!Request.Headers.TryGetValue("X-Internal-Api-Key", out var extractedKey))
        {
            return false;
        }
        return _options.ApiKey == extractedKey;
    }

    [HttpGet("image/{id}")]
    public async Task<IActionResult> GetImageMetadata(Guid id)
    {
        if (!IsAuthorized()) return Unauthorized();

        var image = await _context.Images.FindAsync(id);
        if (image == null) return NotFound();

        return Ok(new
        {
            image.Id,
            image.UserId,
            image.R2OriginalKey,
            image.Status
        });
    }

    [HttpPost("image/{id}/status")]
    public async Task<IActionResult> UpdateImageStatus(Guid id, [FromBody] StatusUpdateRequest request)
    {
        if (!IsAuthorized()) return Unauthorized();

        var image = await _context.Images.FindAsync(id);
        if (image == null) return NotFound();

        image.Status = request.Status;
        image.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(request.R2TilePrefix))
        {
            image.R2TilePrefix = request.R2TilePrefix;
        }

        if (!string.IsNullOrEmpty(request.ErrorMessage))
        {
            image.ErrorMessage = request.ErrorMessage;
        }

        await _context.SaveChangesAsync();
        return Ok();
    }
}

public class StatusUpdateRequest
{
    public ImageStatus Status { get; set; }
    public string? R2TilePrefix { get; set; }
    public string? ErrorMessage { get; set; }
}

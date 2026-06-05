using DZI_gen.Options;
using DZI_shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace DZI_gen.Workers;

public interface IApiService
{
    Task<ImageMetadata?> GetImageMetadataAsync(Guid imageId);
    Task UpdateStatusAsync(Guid imageId, ImageStatus status, string? tilePrefix = null, string? errorMessage = null);
}

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly InternalApiOptions _options;
    private readonly ILogger<ApiService> _logger;

    public ApiService(HttpClient httpClient, IOptions<InternalApiOptions> options, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        // 智能回退：如果未配 BaseUrl，则默认使用 Azure Container App 的内部 DNS 名字
        string baseUrl = _options.BaseUrl;
        if (string.IsNullOrEmpty(baseUrl))
        {
            // 假设 Container App 的名称是 dzi-api
            baseUrl = "http://dzi-api";
            _logger.LogInformation("InternalApi:BaseUrl not found. Falling back to internal service discovery: {Url}", baseUrl);
        }

        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Add("X-Internal-Api-Key", _options.ApiKey);
    }

    public async Task<ImageMetadata?> GetImageMetadataAsync(Guid imageId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/Internal/image/{imageId}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get image metadata. Status: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ImageMetadata>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling internal API for image {ImageId}", imageId);
            return null;
        }
    }

    public async Task UpdateStatusAsync(Guid imageId, ImageStatus status, string? tilePrefix = null, string? errorMessage = null)
    {
        try
        {
            var request = new
            {
                Status = status,
                R2TilePrefix = tilePrefix,
                ErrorMessage = errorMessage
            };

            var response = await _httpClient.PostAsJsonAsync($"api/Internal/image/{imageId}/status", request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to update status for image {ImageId}. Status: {StatusCode}", imageId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for image {ImageId}", imageId);
        }
    }
}

public class ImageMetadata
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? R2OriginalKey { get; set; }
    public ImageStatus Status { get; set; }
}

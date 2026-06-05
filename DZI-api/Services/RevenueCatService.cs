using DZI_api.Options;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DZI_api.Services;

public interface IRevenueCatService
{
    Task<bool> IsSubscriptionActiveAsync(string appUserId);
}

public class RevenueCatService : IRevenueCatService
{
    private readonly HttpClient _httpClient;
    private readonly RevenueCatOptions _options;
    private readonly ILogger<RevenueCatService> _logger;

    public RevenueCatService(HttpClient httpClient, IOptions<RevenueCatOptions> options, ILogger<RevenueCatService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://api.revenuecat.com/v2/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.ApiKey}");
    }

    public async Task<bool> IsSubscriptionActiveAsync(string appUserId)
    {
        try
        {
            // V2 正确路径是 projects/{project_id}/customers/{customer_id}
            var encodedUserId = Uri.EscapeDataString(appUserId);
            var response = await _httpClient.GetAsync($"projects/{_options.ProjectId}/customers/{encodedUserId}");
            var content = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("User {AppUserId} not found in RevenueCat V2 (no transactions yet).", appUserId);
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RevenueCat V2 API returned {StatusCode} for user {AppUserId}. Response: {Content}", 
                    response.StatusCode, appUserId, content);
                return false;
            }

            var customer = JsonSerializer.Deserialize<RevenueCatCustomerResponse>(content);

            // V2 中，如果用户有任何活跃的权利（包括订阅和永久买断），它们会出现在 active_entitlements.items 中
            if (customer?.ActiveEntitlements?.Items != null && customer.ActiveEntitlements.Items.Count > 0)
            {
                _logger.LogInformation("User {AppUserId} has {Count} active entitlements.", appUserId, customer.ActiveEntitlements.Items.Count);
                return true;
            }

            // 对于没有关联 Entitlement 的普通非订阅购买，V2 通常建议检查 subscriptions 列表
            // 但在大多数 DZI 场景下，只要有 active_entitlements 就足够了。
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking V2 customer for user {AppUserId}", appUserId);
            return false;
        }
    }
}

// RevenueCat V2 Customer Response Models
public class RevenueCatCustomerResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("active_entitlements")]
    public ActiveEntitlementsList? ActiveEntitlements { get; set; }
}

public class ActiveEntitlementsList
{
    [JsonPropertyName("items")]
    public List<V2EntitlementItem>? Items { get; set; }
}

public class V2EntitlementItem
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("lookup_key")]
    public string? LookupKey { get; set; }

    [JsonPropertyName("expires_at")]
    public long? ExpiresAt { get; set; } // Unix timestamp in ms
}

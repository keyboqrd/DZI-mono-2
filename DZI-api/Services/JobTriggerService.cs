using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using DZI_api.Options;
using Microsoft.Extensions.Options;

namespace DZI_api.Services;

public interface IJobTriggerService
{
    Task TriggerTilingJobAsync(Guid imageId, string r2OriginalKey);
}

public class JobTriggerService : IJobTriggerService
{
    private readonly AzureContainerAppsOptions _options;
    private readonly ILogger<JobTriggerService> _logger;
    private readonly ArmClient _armClient;

    public JobTriggerService(IOptions<AzureContainerAppsOptions> options, ILogger<JobTriggerService> logger)
    {
        _options = options.Value;
        _logger = logger;
        // Using DefaultAzureCredential which works in Azure and locally if signed in via Azure CLI/VS
        _armClient = new ArmClient(new DefaultAzureCredential());
    }

    public async Task TriggerTilingJobAsync(Guid imageId, string r2OriginalKey)
    {
        try
        {
            var resourceGroupId = $"/subscriptions/{_options.SubscriptionId}/resourceGroups/{_options.ResourceGroupName}";
            var jobId = $"{resourceGroupId}/providers/Microsoft.App/jobs/{_options.JobName}";
            
            ResourceIdentifier resourceIdentifier = new ResourceIdentifier(jobId);
            ContainerAppJobResource containerAppJob = await _armClient.GetContainerAppJobResource(resourceIdentifier).GetAsync();

            // 获取现有的镜像名称，因为 StartAsync 的 override 必须包含完整的容器定义（包括 Image）
            var existingContainer = containerAppJob.Data.Template.Containers.FirstOrDefault();
            if (existingContainer == null) throw new Exception("Job configuration contains no containers.");
            
            var executionTemplate = new ContainerAppJobExecutionTemplate();
            var container = new JobExecutionContainer
            {
                Name = existingContainer.Name,
                Image = existingContainer.Image
            };

            // 1. 先把 Job 现有的环境变量全部拷贝过来，否则会被覆盖
            foreach (var env in existingContainer.Env)
            {
                container.Env.Add(new() { Name = env.Name, Value = env.Value, SecretRef = env.SecretRef });
            }

            // 2. 再注入本次任务特有的 ID
            container.Env.Add(new() { Name = "TARGET_IMAGE_ID", Value = imageId.ToString() });
            
            executionTemplate.Containers.Add(container);

            _logger.LogInformation("Triggering Azure Container App Job {JobName} with image {Image} for Image {ImageId}", 
                _options.JobName, existingContainer.Image, imageId);
            
            // 重要：改为 WaitUntil.Started，API 不需要等切片完成
            await containerAppJob.StartAsync(Azure.WaitUntil.Started, executionTemplate);
            
            _logger.LogInformation("Successfully triggered job for Image {ImageId}", imageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger Azure Container App Job for Image {ImageId}", imageId);
            throw;
        }
    }
}

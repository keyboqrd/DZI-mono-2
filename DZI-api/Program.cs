using Azure.Identity;
using DZI_api.Options;
using DZI_api.Services;
using DZI_shared;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Load configuration from Azure App Configuration if endpoint is provided
string appConfigEndpoint = builder.Configuration["AzureAppConfigurationEndpoint"] ?? "";
if (!string.IsNullOrEmpty(appConfigEndpoint))
{
    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        options.Connect(new Uri(appConfigEndpoint), new DefaultAzureCredential());
    });
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 1. Configure Options
builder.Services.Configure<RevenueCatOptions>(builder.Configuration.GetSection(RevenueCatOptions.Position));
builder.Services.Configure<AzureContainerAppsOptions>(builder.Configuration.GetSection(AzureContainerAppsOptions.Position));
builder.Services.Configure<CloudflareR2Options>(builder.Configuration.GetSection(CloudflareR2Options.Position));
builder.Services.Configure<InternalApiOptions>(builder.Configuration.GetSection(InternalApiOptions.Position));

// 2. Configure Database
builder.Services.AddDbContext<DziDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

// 3. Register Custom Services
builder.Services.AddHttpClient<IRevenueCatService, RevenueCatService>();
builder.Services.AddSingleton<IR2Service, R2Service>();
builder.Services.AddScoped<IJobTriggerService, JobTriggerService>();

var app = builder.Build();

// Non-blocking database initialization
_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<DziDbContext>();
        logger.LogInformation("Starting database initialization in background...");
        await context.Database.EnsureCreatedAsync();
        logger.LogInformation("Database initialization successful.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during background database initialization.");
    }
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

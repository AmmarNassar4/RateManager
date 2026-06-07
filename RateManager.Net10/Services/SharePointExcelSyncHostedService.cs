using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RateManager.Net10.ViewModels;

namespace RateManager.Net10.Services;

public class SharePointExcelSyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SharePointExcelSyncHostedService> _logger;
    private readonly SharePointExcelSyncOptions _options;
    private readonly string _stateFilePath;

    public SharePointExcelSyncHostedService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IOptions<SharePointExcelSyncOptions> options,
        IWebHostEnvironment environment,
        ILogger<SharePointExcelSyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        _stateFilePath = Path.Combine(environment.ContentRootPath, "App_Data", "sharepoint-excel-sync-state.json");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("SharePoint Excel Sync is disabled.");
            return;
        }

        ValidateOptions();
        Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndImportAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SharePoint Excel Sync failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _options.CheckEveryMinutes)), stoppingToken);
        }
    }

    private async Task CheckAndImportAsync(CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken);
        var graphToken = await GetGraphTokenAsync(cancellationToken);
        var metadata = await GetDriveItemMetadataAsync(graphToken, cancellationToken);

        if (!string.IsNullOrWhiteSpace(state.LastETag)
            && string.Equals(state.LastItemId, metadata.Id, StringComparison.Ordinal)
            && string.Equals(state.LastETag, metadata.ETag, StringComparison.Ordinal))
        {
            _logger.LogInformation("SharePoint Excel Sync: no changes detected.");
            return;
        }

        var tempFilePath = await DownloadFileAsync(graphToken, metadata.Name, cancellationToken);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<IExcelRateImportService>();

            var model = new ExcelImportViewModel
            {
                RatePlanId = _options.RatePlanId,
                DiscountRatePlanId = _options.DiscountRatePlanId,
                StartDate = _options.UseTodayAsStartDate ? DateOnly.FromDateTime(DateTime.Today) : _options.StartDate,
                NumberOfDays = _options.NumberOfDays,
                DefaultGuestCount = _options.DefaultGuestCount <= 0 ? 1 : _options.DefaultGuestCount,
                DefaultRoomCount = _options.DefaultRoomCount <= 0 ? 1 : _options.DefaultRoomCount,
                ExcelFilePath = tempFilePath,
                Notes = "Imported automatically from SharePoint Excel Sync"
            };

            var batchId = await importService.ImportAsync(model, "SharePointSync");

            state.LastItemId = metadata.Id;
            state.LastETag = metadata.ETag;
            state.LastModifiedDateTime = metadata.LastModifiedDateTime;
            state.LastSuccessfulImportAt = DateTimeOffset.UtcNow;
            state.LastBatchId = batchId;
            state.LastMessage = $"Imported {metadata.Name}. BatchId: {batchId}";
            await SaveStateAsync(state, cancellationToken);

            _logger.LogInformation("SharePoint Excel Sync imported file successfully. BatchId: {BatchId}", batchId);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
        }
    }

    private async Task<string> GetGraphTokenAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var tokenUrl = $"https://login.microsoftonline.com/{_options.TenantId}/oauth2/v2.0/token";

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["grant_type"] = "client_credentials"
        });

        using var response = await client.PostAsync(tokenUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Microsoft Graph access token was not returned.");
    }

    private async Task<GraphDriveItemMetadata> GetDriveItemMetadataAsync(string graphToken, CancellationToken cancellationToken)
    {
        var client = CreateGraphClient(graphToken);
        var encodedPath = EncodeGraphPath(_options.FilePath);
        var url = $"https://graph.microsoft.com/v1.0/drives/{_options.DriveId}/root:/{encodedPath}?$select=id,name,eTag,lastModifiedDateTime,size";

        using var response = await client.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<GraphDriveItemMetadata>(body, JsonOptions())
            ?? throw new InvalidOperationException("Unable to parse SharePoint file metadata.");
    }

    private async Task<string> DownloadFileAsync(string graphToken, string originalFileName, CancellationToken cancellationToken)
    {
        var client = CreateGraphClient(graphToken);
        var encodedPath = EncodeGraphPath(_options.FilePath);
        var url = $"https://graph.microsoft.com/v1.0/drives/{_options.DriveId}/root:/{encodedPath}:/content";

        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".xlsx";

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".xlsx", ".xlsm", ".xltx", ".xltm" };
        if (!allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("The SharePoint file must be an Excel file.");
        }

        var tempFolder = Path.Combine(Path.GetTempPath(), "RateManagerSharePointSync");
        Directory.CreateDirectory(tempFolder);
        var tempFilePath = Path.Combine(tempFolder, $"{Guid.NewGuid():N}{extension}");

        await using var fileStream = File.Create(tempFilePath);
        await response.Content.CopyToAsync(fileStream, cancellationToken);
        return tempFilePath;
    }

    private HttpClient CreateGraphClient(string graphToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", graphToken);
        return client;
    }

    private async Task<SharePointExcelSyncState> LoadStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_stateFilePath)) return new SharePointExcelSyncState();
        var json = await File.ReadAllTextAsync(_stateFilePath, cancellationToken);
        return JsonSerializer.Deserialize<SharePointExcelSyncState>(json, JsonOptions()) ?? new SharePointExcelSyncState();
    }

    private async Task SaveStateAsync(SharePointExcelSyncState state, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_stateFilePath, json, cancellationToken);
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.TenantId)) throw new InvalidOperationException("SharePointSync:TenantId is required.");
        if (string.IsNullOrWhiteSpace(_options.ClientId)) throw new InvalidOperationException("SharePointSync:ClientId is required.");
        if (string.IsNullOrWhiteSpace(_options.ClientSecret)) throw new InvalidOperationException("SharePointSync:ClientSecret is required.");
        if (string.IsNullOrWhiteSpace(_options.DriveId)) throw new InvalidOperationException("SharePointSync:DriveId is required.");
        if (string.IsNullOrWhiteSpace(_options.FilePath)) throw new InvalidOperationException("SharePointSync:FilePath is required.");
        if (_options.RatePlanId <= 0) throw new InvalidOperationException("SharePointSync:RatePlanId must be greater than zero.");
        if (_options.NumberOfDays <= 0) throw new InvalidOperationException("SharePointSync:NumberOfDays must be greater than zero.");
    }

    private static string EncodeGraphPath(string path)
    {
        return string.Join('/', path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
    }

    private static JsonSerializerOptions JsonOptions() => new() { PropertyNameCaseInsensitive = true };

    private sealed class GraphDriveItemMetadata
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ETag { get; set; } = string.Empty;
        public DateTimeOffset? LastModifiedDateTime { get; set; }
        public long Size { get; set; }
    }
}

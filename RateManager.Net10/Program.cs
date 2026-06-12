using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RateManager.Net10.Data;
using RateManager.Net10.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "Rate Manager";
});

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.Configure<SharePointExcelSyncOptions>(builder.Configuration.GetSection("SharePointSync"));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is missing from appsettings.json.");

builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(defaultConnection, new SqlServerStorageOptions
    {
        PrepareSchemaIfNecessary = true,
        QueuePollInterval = TimeSpan.FromSeconds(15)
    }));

builder.Services.AddHangfireServer();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "RateManager.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddScoped<IRateCalculationService, RateCalculationService>();
builder.Services.AddScoped<IExcelRateImportService, ExcelRateImportService>();
builder.Services.AddScoped<IRateAuditService, RateAuditService>();
builder.Services.AddScoped<ICurrentRateCalculatorService, CurrentRateCalculatorService>();
builder.Services.AddScoped<IPmsExportService, PmsExportService>();
builder.Services.AddScoped<ISharePointExcelSyncJob, SharePointExcelSyncJob>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAdminAuthorizationFilter() }
});

using (var scope = app.Services.CreateScope())
{
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var autoCreate = configuration.GetValue<bool>("AppSettings:AutoCreateDatabase");

    if (autoCreate)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        SeedData.Initialize(db);
    }

    var checkEveryMinutes = Math.Max(1, configuration.GetValue<int?>("SharePointSync:CheckEveryMinutes") ?? 5);

    RecurringJob.AddOrUpdate<ISharePointExcelSyncJob>(
        "sharepoint-excel-sync",
        job => job.RunAsync(),
        Cron.MinuteInterval(checkEveryMinutes));
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
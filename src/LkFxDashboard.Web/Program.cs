using LkFxDashboard.Api.Endpoints;
using LkFxDashboard.Api.Services;
using LkFxDashboard.Data;
using LkFxDashboard.Data.Extensions;
using LkFxDashboard.Scrapers.Extensions;
using LkFxDashboard.Web.Components;
using LkFxDashboard.Web.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console());

// Aspire service defaults
builder.AddServiceDefaults();

// Aspire PostgreSQL (connection auto-injected by AppHost)
builder.AddNpgsqlDbContext<AppDbContext>("lkfxdb");

// Register IDbContextFactory for Blazor Server concurrency safety.
// Cannot use AddDbContextFactory here — it conflicts with Aspire's AddDbContextPool
// (registers scoped config services that the singleton pool can't resolve).
// Instead, use a simple factory that creates contexts with the Npgsql-configured options.
builder.Services.AddSingleton<IDbContextFactory<AppDbContext>>(sp =>
{
    var options = sp.GetRequiredService<DbContextOptions<AppDbContext>>();
    return new AppDbContextFactory(options);
});

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Data
builder.Services.AddDataServices();

// Scrapers
builder.Services.AddScrapers(builder.Configuration);

// Background service
builder.Services.AddHostedService<RateScrapingBackgroundService>();

// Rate service for Blazor components
builder.Services.AddScoped<IRateService, ServerRateService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// Apply migrations on startup (skip for non-relational providers like InMemory)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
        Log.Information("Database migrations applied successfully");
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }

    // Verify factory-created contexts also work
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var testDb = await factory.CreateDbContextAsync();
    var count = await testDb.ExchangeRates.CountAsync();
    Log.Information("Database check: {Count} exchange rates in DB (via factory)", count);
}

app.MapDefaultEndpoints();
app.MapRateEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;

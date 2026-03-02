using Microsoft.EntityFrameworkCore;

namespace LkFxDashboard.Data;

/// <summary>
/// Simple IDbContextFactory implementation for Blazor Server concurrency safety.
/// Creates fresh AppDbContext instances using the Npgsql-configured options
/// registered by Aspire's AddNpgsqlDbContext.
/// </summary>
public class AppDbContextFactory(DbContextOptions<AppDbContext> options)
    : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(options);
}

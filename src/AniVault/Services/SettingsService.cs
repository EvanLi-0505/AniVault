using System.Threading;
using System.Threading.Tasks;
using AniVault.Data;
using AniVault.Models;
using Microsoft.EntityFrameworkCore;

namespace AniVault.Services;

/// <summary>
/// Reads and writes application settings stored as key/value rows in the library database.
/// (The data directory location is the one exception and lives in the bootstrap file.)
/// </summary>
public interface ISettingsService
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<string> GetAsync(string key, string defaultValue, CancellationToken cancellationToken = default);

    Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken cancellationToken = default);

    Task SetAsync(string key, string? value, CancellationToken cancellationToken = default);
}

public sealed class SettingsService : ISettingsService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public SettingsService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        return setting?.Value;
    }

    public async Task<string> GetAsync(string key, string defaultValue, CancellationToken cancellationToken = default)
        => await GetAsync(key, cancellationToken) ?? defaultValue;

    public async Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken cancellationToken = default)
    {
        var raw = await GetAsync(key, cancellationToken);
        return bool.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    public async Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting is null)
        {
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

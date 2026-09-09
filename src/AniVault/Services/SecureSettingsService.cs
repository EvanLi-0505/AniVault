using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AniVault.Services;

/// <summary>
/// Stores small secrets (currently just optional provider API keys) in the settings table,
/// encrypted with Windows DPAPI so they are not readable as plain text and are tied to the
/// current Windows user. Keys are never written to logs.
/// </summary>
public interface ISecureSettingsService
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, string? value, CancellationToken cancellationToken = default);
}

public sealed class SecureSettingsService : ISecureSettingsService
{
    private const string Prefix = "enc:v1:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AniVault.SecureSettings");

    private readonly ISettingsService _settings;

    public SecureSettingsService(ISettingsService settings)
    {
        _settings = settings;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var stored = await _settings.GetAsync(key, cancellationToken);
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var cipher = Convert.FromBase64String(stored[Prefix.Length..]);
            var plain = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception)
        {
            // Corrupt or from another user/machine — treat as unset.
            return null;
        }
    }

    public async Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            await _settings.SetAsync(key, null, cancellationToken);
            return;
        }

        var cipher = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(value.Trim()), Entropy, DataProtectionScope.CurrentUser);
        await _settings.SetAsync(key, Prefix + Convert.ToBase64String(cipher), cancellationToken);
    }
}

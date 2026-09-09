using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AniVault.Metadata.Providers;
using Microsoft.Extensions.Logging;

namespace AniVault.Services.Artwork;

/// <summary>
/// Downloads a remote image to a temporary file. Only ever called for a URL the user
/// explicitly chose to download (poster / backdrop import). Enforces a size cap and a
/// basic content-type check so a bad URL cannot flood the disk.
/// </summary>
public interface IImageDownloadService
{
    /// <summary>Downloads <paramref name="url"/> to a temp file and returns its path, or null on failure.</summary>
    Task<string?> DownloadToTempAsync(string url, CancellationToken cancellationToken = default);
}

public sealed class ImageDownloadService : IImageDownloadService
{
    private const long MaxBytes = 15 * 1024 * 1024;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImageDownloadService> _logger;

    public ImageDownloadService(IHttpClientFactory httpClientFactory, ILogger<ImageDownloadService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string?> DownloadToTempAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(ProviderHttp.HttpClientName);
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (response.Content.Headers.ContentLength is > MaxBytes)
            {
                return null;
            }

            var extension = mediaType.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/bmp" => ".bmp",
                _ => ".jpg",
            };

            var tempPath = Path.Combine(Path.GetTempPath(), $"anivault-dl-{Guid.NewGuid():N}{extension}");

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = new FileStream(tempPath, FileMode.CreateNew);

            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > MaxBytes)
                {
                    target.Close();
                    File.Delete(tempPath);
                    return null;
                }

                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            return tempPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download image from {Url}.", url);
            return null;
        }
    }
}

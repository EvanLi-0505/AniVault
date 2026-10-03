using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AniVault.Metadata.Providers;

/// <summary>
/// Shared HTTP plumbing for metadata providers: one configured <see cref="HttpClient"/>,
/// JSON parsing, and translation of network / status failures into a single
/// <see cref="MetadataProviderException"/> with a user-friendly message.
/// </summary>
internal sealed class ProviderHttp
{
    public const string HttpClientName = "metadata";

    private readonly HttpClient _client;
    private readonly string _providerName;

    public ProviderHttp(IHttpClientFactory httpClientFactory, string providerName)
    {
        _client = httpClientFactory.CreateClient(HttpClientName);
        _providerName = providerName;
    }

    public Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
        => SendAsync(new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);

    public Task<JsonDocument> PostJsonAsync(string url, object body, CancellationToken cancellationToken)
        => SendAsync(new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) }, cancellationToken);

    public HttpRequestMessage NewRequest(HttpMethod method, string url) => new(method, url);

    public async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Every caller hands its request over for good, so it is released here once the
        // response has been read.
        using var ownedRequest = request;

        HttpResponseMessage response;
        try
        {
            response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MetadataProviderException($"{_providerName} did not respond in time. Check your connection and try again.");
        }
        catch (HttpRequestException ex)
        {
            throw new MetadataProviderException(
                $"Unable to reach {_providerName}. Your local library is still fully available offline.", ex);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.TooManyRequests)
            {
                throw new MetadataProviderException($"{_providerName} is rate-limiting requests. Please wait a moment and try again.");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new MetadataProviderException(
                    $"{_providerName} refused the request. It may be temporarily unavailable, blocked on your network, "
                    + "or (for TMDB) missing its API key. Try another provider in Settings.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new MetadataProviderException($"{_providerName} returned an error ({(int)response.StatusCode}).");
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }
            catch (JsonException ex)
            {
                throw new MetadataProviderException($"{_providerName} returned a response AniVault could not understand.", ex);
            }
        }
    }
}

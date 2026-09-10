using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AniVault.Tests;

/// <summary>An <see cref="IHttpClientFactory"/> that returns canned responses, keyed by URL substring.</summary>
public sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly List<(string Match, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _rules = new();

    public FakeHttpClientFactory On(string urlContains, string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _rules.Add((urlContains, _ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        }));
        return this;
    }

    /// <summary>Like <see cref="On(string,string,HttpStatusCode)"/> but the response body can inspect the request.</summary>
    public FakeHttpClientFactory OnRequest(string urlContains, Func<HttpRequestMessage, string> respond)
    {
        _rules.Add((urlContains, req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(respond(req), System.Text.Encoding.UTF8, "application/json"),
        }));
        return this;
    }

    public HttpClient CreateClient(string name) => new(new Handler(_rules));

    private sealed class Handler : HttpMessageHandler
    {
        private readonly List<(string Match, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _rules;

        public Handler(List<(string, Func<HttpRequestMessage, HttpResponseMessage>)> rules) => _rules = rules;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            foreach (var (match, respond) in _rules)
            {
                if (url.Contains(match, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(respond(request));
                }
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}

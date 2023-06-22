﻿using System;
using System.Net.Http.Json;
using System.Text.Json;
using DarkLink.Web.WebFinger.Shared;

namespace DarkLink.Web.WebFinger.Client;

public class WebFingerClient : IDisposable
{
    private readonly HttpClient httpClient;

    public WebFingerClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public void Dispose() => httpClient.Dispose();

    public Task<JsonResourceDescriptor?> GetResourceDescriptorAsync(Uri server, Uri resource, CancellationToken cancellationToken = default)
        => GetResourceDescriptorAsync(server, resource, Array.Empty<string>(), cancellationToken);

    public async Task<JsonResourceDescriptor?> GetResourceDescriptorAsync(Uri server, Uri resource, IReadOnlyList<string> relations, CancellationToken cancellationToken = default)
    {
        var queryString = $"{Constants.QUERY_RESOURCE}={Uri.EscapeDataString(resource.ToString())}";
        queryString += string.Concat(relations.Select(relation => $"&{Constants.QUERY_RELATION}={Uri.EscapeDataString(relation)}"));

        var requestUri = new UriBuilder(server.Scheme, server.Host, server.Port, Constants.HTTP_PATH)
        {
            Query = queryString,
        }.Uri;
        var descriptor = await httpClient.GetFromJsonAsync<JsonResourceDescriptor>(
            requestUri,
            new JsonSerializerOptions
            {
                Converters =
                {
                    new JsonResourceDescriptorConverter(),
                },
            },
            cancellationToken);
        return descriptor;
    }
}

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WasabiBot.Api.Features.CaptionThis.Abstractions;

namespace WasabiBot.Api.Features.CaptionThis.Services;

internal sealed class HttpClientImageRetrievalService : IImageRetrievalService
{
    private readonly HttpClient _httpClient;

    public HttpClientImageRetrievalService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<byte[]> GetImageBytesAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        return _httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
    }
}

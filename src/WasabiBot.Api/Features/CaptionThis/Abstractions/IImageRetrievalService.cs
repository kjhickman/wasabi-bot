namespace WasabiBot.Api.Features.CaptionThis.Abstractions;

public interface IImageRetrievalService
{
    Task<byte[]> GetImageBytesAsync(string imageUrl, CancellationToken cancellationToken = default);
}

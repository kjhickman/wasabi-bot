using Microsoft.Extensions.AI;
using NetCord;
using OpenTelemetry.Trace;
using WasabiBot.Api.Core.Extensions;
using WasabiBot.Api.Features.CaptionThis.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.CaptionThis;

[CommandHandler("caption", "Generate a funny caption for an image.", nameof(ExecuteAsync))]
internal sealed class CaptionThisCommand
{
    private readonly IChatClient _chatClient;
    private readonly IImageRetrievalService _imageRetrievalService;
    private readonly Tracer _tracer;
    private readonly ILogger<CaptionThisCommand> _logger;

    public CaptionThisCommand(IChatClient chatClient, IImageRetrievalService imageRetrievalService, Tracer tracer,
        ILogger<CaptionThisCommand> logger)
    {
        _chatClient = chatClient;
        _imageRetrievalService = imageRetrievalService;
        _tracer = tracer;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        ICommandContext ctx,
        Attachment image)
    {
        using var span = _tracer.StartActiveSpan("caption.generate");

        _logger.LogInformation(
            "Caption command invoked by user {Username} in channel {ChannelId} with attachment {FileName}",
            ctx.UserDisplayName,
            ctx.ChannelId,
            image.FileName);

        if (!IsImageAttachment(image.ContentType))
        {
            _logger.LogWarning(
                "Unsupported attachment type {ContentType} provided by user {Username} for caption command",
                image.ContentType,
                ctx.UserDisplayName);
            await ctx.SendEphemeralAsync("Please provide a valid image file (jpg, jpeg, png, gif, webp).");
            return;
        }

        if (image.Size > 10 * 1024 * 1024) // 10MB limit
        {
            _logger.LogWarning(
                "Attachment {FileName} rejected due to size {SizeBytes} bytes from user {Username}",
                image.FileName,
                image.Size,
                ctx.UserDisplayName);
            await ctx.SendEphemeralAsync("Image is too large. Please provide an image smaller than 10MB.");
            return;
        }

        try
        {
            const string prompt = "Look at this image and create a memey caption for it: " +
                                  "Keep it concise but entertaining. Don't describe what you see, just provide the caption.";

            var imageBytes = await _imageRetrievalService.GetImageBytesAsync(image.Url);
            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, [
                    new TextContent(prompt),
                    new DataContent(imageBytes, image.ContentType!)
                ])
            };

            var captionResponse = await _chatClient.GetResponseAsync(messages);
            var captionText = captionResponse.Text;
            var response = image.Url + "\n" + captionText;
            _logger.LogInformation(
                "Caption generated successfully for user {Username}",
                ctx.UserDisplayName);
            await ctx.RespondAsync(response);
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            _logger.LogError(
                ex,
                "Failed to generate caption for user {Username} with attachment {FileName}",
                ctx.UserDisplayName,
                image.FileName);
            await ctx.SendEphemeralAsync("Sorry, I had trouble processing that image. Please try again with a different image.");
        }
    }

    private static bool IsImageAttachment(string? contentType)
    {
        return !contentType.IsNullOrWhiteSpace() &&
               AllowedImageContentTypes.Contains(contentType!);
    }

    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/gif",
        "image/webp"
    };
}

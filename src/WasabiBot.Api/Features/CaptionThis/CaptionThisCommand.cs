using Microsoft.Extensions.AI;
using NetCord;
using NetCord.Services.ApplicationCommands;
using OpenTelemetry.Trace;
using WasabiBot.Api.Core.Extensions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;
using System.Diagnostics;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.ServiceDefaults;

namespace WasabiBot.Api.Features.CaptionThis;

internal class CaptionThisCommand
{
    public const string Name = "caption";
    public const string Description = "Generate a funny caption for an image.";

    public static async Task ExecuteAsync(
        IChatClient chat,
        HttpClient httpClient,
        Tracer tracer,
        ILogger<CaptionThisCommand> logger,
        ApplicationCommandContext ctx,
        [SlashCommandParameter(Name = "image", Description = "Interesting image")] Attachment image)
    {
        using var span = tracer.StartActiveSpan("caption.generate");
        await using var responder = InteractionResponder.Create(ctx);

        logger.LogInformation(
            "Caption command invoked by user {Username} in channel {ChannelId} with attachment {FileName}",
            ctx.Interaction.User.Username,
            ctx.Interaction.Channel.Id,
            image.FileName);

        if (!IsImageAttachment(image))
        {
            logger.LogWarning(
                "Unsupported attachment type {ContentType} provided by user {Username} for caption command",
                image.ContentType,
                ctx.Interaction.User.Username);
            await responder.SendEphemeralAsync("Please provide a valid image file (jpg, jpeg, png, gif, webp).");
            return;
        }

        if (image.Size > 10 * 1024 * 1024) // 10MB limit
        {
            logger.LogWarning(
                "Attachment {FileName} rejected due to size {SizeBytes} bytes from user {Username}",
                image.FileName,
                image.Size,
                ctx.Interaction.User.Username);
            await responder.SendEphemeralAsync("Image is too large. Please provide an image smaller than 10MB.");
            return;
        }

        try
        {
            const string prompt = "Look at this image and create a memey caption for it: " +
                                  "Keep it concise but entertaining. Don't describe what you see, just provide the caption.";

            var imageBytes = await httpClient.GetByteArrayAsync(image.Url);
            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, [
                    new TextContent(prompt),
                    new DataContent(imageBytes, image.ContentType!)
                ])
            };

            var llmStart = Stopwatch.GetTimestamp();
            var captionResponse = await chat.GetResponseAsync(messages);
            var elapsed = Stopwatch.GetElapsedTime(llmStart).TotalSeconds;
            LlmMetrics.LlmResponseLatency.Record(elapsed, new TagList
            {
                {"command", Name},
                {"status", "ok"}
            });

            var captionText = captionResponse.Text;
            var response = image.Url + "\n" + captionText;
            logger.LogInformation(
                "Caption generated successfully for user {Username}",
                ctx.Interaction.User.Username);
            await responder.SendAsync(response);
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            logger.LogError(
                ex,
                "Failed to generate caption for user {Username} with attachment {FileName}",
                ctx.Interaction.User.Username,
                image.FileName);
            await responder.SendEphemeralAsync("Sorry, I had trouble processing that image. Please try again with a different image.");
        }
    }

    private static bool IsImageAttachment(Attachment attachment)
    {
        return !attachment.ContentType.IsNullOrWhiteSpace() &&
               AllowedImageContentTypes.Contains(attachment.ContentType!);
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

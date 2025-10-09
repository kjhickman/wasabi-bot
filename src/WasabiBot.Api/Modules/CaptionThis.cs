using Microsoft.Extensions.AI;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Hosting.Services.ApplicationCommands;
using OpenTelemetry.Trace;
using WasabiBot.Api.Services;

namespace WasabiBot.Api.Modules;

internal sealed class CaptionThisCommand : ISlashCommand
{
    public string Name => "caption";
    public string Description => "Generate a funny caption for an image.";

    public void Register(WebApplication app)
    {
        app.AddSlashCommand(Name, Description, ExecuteAsync);
    }

    private async Task ExecuteAsync(IChatClient chat, HttpClient httpClient, Tracer tracer, ApplicationCommandContext ctx, Attachment image)
    {
        using var span = tracer.StartActiveSpan($"{nameof(CaptionThisCommand)}.{nameof(ExecuteAsync)}");

        await using var responder = new AutoResponder(
            threshold: TimeSpan.FromMilliseconds(2300),
            defer: _ => ctx.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage()),
            respond: (text, ephemeral) => ctx.Interaction.SendResponseAsync(InteractionCallback.Message(InteractionMessageFactory.Create(text, ephemeral))),
            followup: (text, ephemeral) => ctx.Interaction.SendFollowupMessageAsync(InteractionMessageFactory.Create(text, ephemeral)));

        if (!IsImageAttachment(image))
        {
            await responder.SendAsync("Please provide a valid image file (jpg, jpeg, png, gif, webp).", ephemeral: true);
            return;
        }

        if (image.Size > 10 * 1024 * 1024) // 10MB limit
        {
            await responder.SendAsync("Image is too large. Please provide an image smaller than 10MB.", ephemeral: true);
            return;
        }

        try
        {
            const string prompt = "Look at this image and create a funny, witty caption for it: " +
                                  "Keep it concise but entertaining. Don't describe what you see, just provide the caption.";

            var imageBytes = await httpClient.GetByteArrayAsync(image.Url);
            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, [
                    new TextContent(prompt),
                    new DataContent(imageBytes, image.ContentType!)
                ])
            };

            var caption = await chat.GetResponseAsync(messages);
            var response = image.Url + "\n" + caption;
            await responder.SendAsync(response);
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            await responder.SendAsync("Sorry, I had trouble processing that image. Please try again with a different image.", ephemeral: true);
        }
    }

    private static bool IsImageAttachment(Attachment attachment)
    {
        if (string.IsNullOrEmpty(attachment.ContentType))
            return false;

        var imageTypes = new[]
        {
            "image/jpeg",
            "image/jpg",
            "image/png",
            "image/gif",
            "image/webp"
        };

        return imageTypes.Contains(attachment.ContentType.ToLowerInvariant());
    }
}

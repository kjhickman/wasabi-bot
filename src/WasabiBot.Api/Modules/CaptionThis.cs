using Microsoft.Extensions.AI;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using OpenTelemetry.Trace;

namespace WasabiBot.Api.Modules;

internal static class CaptionThis
{
    public const string CommandName = "caption";
    public const string CommandDescription = "Generate a funny caption for an image.";

    public static async Task Command(IChatClient chat, Tracer tracer, ApplicationCommandContext ctx, Attachment image)
    {
        using var span = tracer.StartActiveSpan($"{nameof(CaptionThis)}.{nameof(Command)}");

        await ctx.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());
        if (!IsImageAttachment(image))
        {
            await ctx.Interaction.SendFollowupMessageAsync("Please provide a valid image file (jpg, jpeg, png, gif, webp).");
            return;
        }

        if (image.Size > 10 * 1024 * 1024) // 10MB limit
        {
            await ctx.Interaction.SendFollowupMessageAsync("Image is too large. Please provide an image smaller than 10MB.");
            return;
        }

        try
        {
            const string prompt = "Look at this image and create a funny, witty caption for it. " +
                                  "Keep it concise but entertaining. Don't describe what you see, just provide the caption.";

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, [
                    new TextContent(prompt),
                    new UriContent(image.Url, image.ContentType!)
                ])
            };

            var response = await chat.GetResponseAsync(messages);
            await ctx.Interaction.SendFollowupMessageAsync(response.Text);
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            await ctx.Interaction.SendFollowupMessageAsync("Sorry, I had trouble processing that image. Please try again with a different image.");
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

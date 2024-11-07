namespace WasabiBot.Core.Discord;

public static class Format
{
    public static string Bold(string text) => $"**{text}**";
    public static string Italics(string text) => $"*{text}*";
    public static string Underline(string text) => $"__{text}__";
    public static string Strikethrough(string text) => $"~~{text}~~";
    public static string Code(string text) => $"`{text}`";
    public static string Mention(string userId) => $"<@{userId}>";
}
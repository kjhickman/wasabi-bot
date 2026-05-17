using System.Diagnostics.CodeAnalysis;

namespace WasabiBot.Api.Core.Extensions;

public static class StringExtensions
{
    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? str)
    {
        return string.IsNullOrWhiteSpace(str);
    }
}

namespace WasabiBot.Api.Features.Auth;

public sealed record CurrentUserResponse(
    string UserId,
    string? Username,
    string? GlobalName,
    string? Discriminator,
    string? AvatarUrl
);

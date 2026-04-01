namespace WasabiBot.Api.Infrastructure.Auth;

public sealed class ApiCredentialSecretOptions(string pepper)
{
    public string Pepper { get; } = pepper;
}

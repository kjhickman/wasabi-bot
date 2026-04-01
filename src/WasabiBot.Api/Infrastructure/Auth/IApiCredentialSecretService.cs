namespace WasabiBot.Api.Infrastructure.Auth;

public interface IApiCredentialSecretService
{
    string CreateClientId();
    string CreateClientSecret();
    string HashSecret(string secret);
    bool VerifySecret(string secret, string secretHash);
}

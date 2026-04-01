using System.Security.Cryptography;
using System.Text;

namespace WasabiBot.Api.Infrastructure.Auth;

public sealed class ApiCredentialSecretService(ApiCredentialSecretOptions options) : IApiCredentialSecretService
{
    private readonly byte[] _pepperBytes = Encoding.UTF8.GetBytes(options.Pepper);

    public string CreateClientId() => $"wb_{CreateHexToken(byteCount: 10)}";

    public string CreateClientSecret() => $"wb_secret_{CreateHexToken(byteCount: 32)}";

    public string HashSecret(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        using var hmac = new HMACSHA256(_pepperBytes);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hashBytes);
    }

    public bool VerifySecret(string secret, string secretHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretHash);

        byte[] expectedHashBytes;
        try
        {
            expectedHashBytes = Convert.FromBase64String(secretHash);
        }
        catch (FormatException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(_pepperBytes);
        var actualHashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(secret));
        return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
    }

    private static string CreateHexToken(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

using System.Text;
using NSec.Cryptography;

namespace WasabiBot.Tests.Integration.Web;

/// <summary>
/// A test client for creating signed Discord interactions.
/// This client simulates Discord's request signing process for testing purposes.
/// </summary>
public class DiscordInteractionTestClient
{
    private readonly Key _privateKey;
    private const string InteractionPath = "v1/interaction";

    /// <summary>
    /// Initializes a new instance of the Discord test client.
    /// </summary>
    /// <param name="privateKeyHex">The Ed25519 private key in hexadecimal format used for signing requests.</param>
    /// <exception cref="FormatException">Thrown when the provided hex string is invalid.</exception>
    /// <exception cref="ArgumentException">Thrown when the provided key data is invalid.</exception>
    public DiscordInteractionTestClient(string privateKeyHex)
    {
        // Convert hex private key to bytes and create NSec Key
        var privateKeyBytes = Convert.FromHexString(privateKeyHex);
        _privateKey = Key.Import(SignatureAlgorithm.Ed25519, privateKeyBytes, KeyBlobFormat.RawPrivateKey);
    }

    /// <summary>
    /// Sends a signed interaction request to the specified HTTP client.
    /// </summary>
    /// <param name="httpClient">The HTTP client to send the request through.</param>
    /// <param name="jsonBody">The JSON body of the interaction.</param>
    /// <returns>The HTTP response from the server.</returns>
    /// <remarks>
    /// This method:
    /// 1. Generates a Unix timestamp
    /// 2. Signs the request using the Ed25519 private key
    /// 3. Adds the required Discord headers (X-Signature-Ed25519 and X-Signature-Timestamp)
    /// 4. Sends the request to the v1/interaction endpoint
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when httpClient or jsonBody is null.</exception>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    public async Task<HttpResponseMessage> SendInteractionAsync(HttpClient httpClient, string jsonBody)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = SignRequest(timestamp, jsonBody);
        
        using var request = new HttpRequestMessage(HttpMethod.Post, InteractionPath);
        request.Headers.Add("X-Signature-Ed25519", signature);
        request.Headers.Add("X-Signature-Timestamp", timestamp);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        
        return await httpClient.SendAsync(request);
    }

    private string SignRequest(string timestamp, string body)
    {
        // Combine timestamp and body as Discord does
        var message = Encoding.UTF8.GetBytes(timestamp + body);
        
        var algorithm = SignatureAlgorithm.Ed25519;
        var signature = algorithm.Sign(_privateKey, message);
        
        return Convert.ToHexString(signature).ToLower();
    }
}

public static class SignatureUtility
{
    /// <summary>
    /// Generates a new Ed25519 key pair for testing purposes.
    /// </summary>
    /// <returns>
    /// A tuple containing:
    /// - privateKey: The private key in lowercase hexadecimal format
    /// - publicKey: The public key in lowercase hexadecimal format
    /// </returns>
    /// <remarks>
    /// The generated key pair is suitable for testing Discord interactions.
    /// The public key should be configured in your application settings,
    /// while the private key should be used to instantiate the DiscordTestClient.
    /// </remarks>
    public static (string privateKey, string publicKey) GenerateKeyPair()
    {
        var algorithm = SignatureAlgorithm.Ed25519;
        var keyCreationParameters = new KeyCreationParameters 
        { 
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport 
        };
        
        using var key = Key.Create(algorithm, keyCreationParameters);
        
        var privateKeyBytes = key.Export(KeyBlobFormat.RawPrivateKey);
        var publicKeyBytes = key.Export(KeyBlobFormat.RawPublicKey);
        
        return (
            Convert.ToHexString(privateKeyBytes).ToLower(),
            Convert.ToHexString(publicKeyBytes).ToLower()
        );
    }
}
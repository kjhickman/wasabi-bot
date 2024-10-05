using System.Text;
using Sodium;

namespace WasabiBot.Discord;

public static class Signature
{
    public static bool Verify(string publicKey, string signature, string timestamp, string requestBody)
    {
        var sig = Convert.FromHexString(signature);
        var ts = Encoding.UTF8.GetBytes(timestamp);
        var pk = Convert.FromHexString(publicKey);
        var body = Encoding.UTF8.GetBytes(requestBody);
        var msg = ts.Concat(body).ToArray();
        return PublicKeyAuth.VerifyDetached(sig, msg, pk);
    }
}

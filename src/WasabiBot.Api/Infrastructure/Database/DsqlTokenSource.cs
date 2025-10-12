using Amazon;
using Amazon.DSQL.Util;
using Amazon.Runtime;
using Microsoft.Extensions.Options;

namespace WasabiBot.Api.Infrastructure.Database;

/// <summary>
/// Supplies short-lived Aurora DSQL auth tokens (password equivalents) for admin and custom role logins.
/// Tokens are ~15 minute lifetime; we cache until 1 minute before expiry.
/// </summary>
public sealed class DsqlTokenSource
{
    private readonly RegionEndpoint _region;
    private readonly string _endpoint;
    private readonly AWSCredentials _credentials;

    private string? _cachedAdminToken;
    private DateTimeOffset _adminExpiryUtc;

    private string? _cachedRoleToken;
    private DateTimeOffset _roleExpiryUtc;

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15); // SDK default
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(1);

    public DsqlTokenSource(AWSCredentials credentials)
    {
        _region = RegionEndpoint.GetBySystemName("us-east-1");
        _endpoint = "dvthki5gk26y76agneiy6wcn64.dsql.us-east-1.on.aws";
        _credentials = credentials;
    }

    public string GetAdminToken()
    {
        if (_cachedAdminToken is { Length: > 0 } token && _adminExpiryUtc - DateTimeOffset.UtcNow > RefreshSkew)
        {
            return token;
        }

        var newToken = DSQLAuthTokenGenerator.GenerateDbConnectAdminAuthToken(_credentials, _region, _endpoint);
        _cachedAdminToken = newToken;
        _adminExpiryUtc = DateTimeOffset.UtcNow + TokenLifetime;
        return newToken;
    }

    public string GetRoleToken()
    {
        if (_cachedRoleToken is { Length: > 0 } token && _roleExpiryUtc - DateTimeOffset.UtcNow > RefreshSkew)
        {
            return token;
        }

        var newToken = DSQLAuthTokenGenerator.GenerateDbConnectAuthToken(_credentials, _region, _endpoint);
        _cachedRoleToken = newToken;
        _roleExpiryUtc = DateTimeOffset.UtcNow + TokenLifetime;
        return newToken;
    }
}

using System.Data;
using Npgsql;

namespace WasabiBot.Api.Infrastructure.Database;

internal sealed class NpgsqlConnectionFactory(NpgsqlDataSource dataSource) : IDbConnectionFactory
{
    public async ValueTask<IDbConnection> CreateConnection(CancellationToken cancellationToken = default)
    {
        return await dataSource.OpenConnectionAsync(cancellationToken);
    }
}

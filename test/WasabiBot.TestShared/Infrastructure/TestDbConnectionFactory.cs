using System.Data;
using Npgsql;
using WasabiBot.Api.Infrastructure.Database;

namespace WasabiBot.TestShared.Infrastructure;

public sealed class TestDbConnectionFactory(NpgsqlDataSource dataSource) : IDbConnectionFactory
{
    public async ValueTask<IDbConnection> CreateConnection(CancellationToken cancellationToken = default)
    {
        return await dataSource.OpenConnectionAsync(cancellationToken);
    }
}

using System.Data;

namespace WasabiBot.Api.Infrastructure.Database;

public interface IDbConnectionFactory
{
    ValueTask<IDbConnection> CreateConnection(CancellationToken cancellationToken = default);
}

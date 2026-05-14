using System.Runtime.CompilerServices;
using Dapper;

[module: DapperAot]
[assembly: InternalsVisibleTo("WasabiBot.UnitTests")]
[assembly: InternalsVisibleTo("WasabiBot.IntegrationTests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

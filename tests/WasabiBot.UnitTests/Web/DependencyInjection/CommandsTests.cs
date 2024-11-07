using Microsoft.Extensions.DependencyInjection;
using WasabiBot.DataAccess.Commands;
using WasabiBot.Web.DependencyInjection;

namespace WasabiBot.UnitTests.Web.DependencyInjection;

public class ApplicationCommandsTests
{
    [Test]
    public async Task AddCommands_Adds_AllCommands()
    {
        var services = new ServiceCollection().AddCommands();

        await Assert.That(services.Count).IsEqualTo(ApplicationCommands.Definitions.Length);
    }
}
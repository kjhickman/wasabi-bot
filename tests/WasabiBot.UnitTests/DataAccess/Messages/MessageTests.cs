using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess;

namespace WasabiBot.UnitTests.DataAccess.Messages;

public class MessageTests
{
    [Test]
    public void DataAccessJsonContext_Contains_AllMessageTypes()
    {
        var assemblies = System.Reflection.Assembly.Load("WasabiBot.DataAccess");
        var handlerTypes = assemblies.GetTypes()
            .Where(t => typeof(IMessage).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });
        foreach (var handlerType in handlerTypes)
        {
            var handlerJsonTypeInfo = DataAccessJsonContext.Default.GetTypeInfo(handlerType);
            if (handlerJsonTypeInfo is null)
            {
                Assert.Fail($"Type {handlerType.Name} was not added to the JsonSerializerContext");
            }
        }
    }
}

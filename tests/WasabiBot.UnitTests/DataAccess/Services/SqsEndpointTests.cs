using WasabiBot.DataAccess;
using WasabiBot.DataAccess.Interfaces;
using Assembly = System.Reflection.Assembly;

namespace WasabiBot.UnitTests.DataAccess.Services;

public class SqsEndpointTests
{
    [Test]
    public async Task JsonTypeInfo_Resolves_ForEveryMessage()
    {
        var messageTypes = GetAllMessageTypes();
        foreach (var messageType in messageTypes)
        {
            var result = DataAccessJsonContext.Default.GetTypeInfo(messageType);
            await Assert.That(result).IsNotNull();
        }
    }
    
    private static IEnumerable<Type> GetAllMessageTypes()
    {
        return Assembly.GetExecutingAssembly().GetTypes()
            .SelectMany(type => type.GetInterfaces()
                .Where(i => i.IsGenericType && 
                            i.GetGenericTypeDefinition() == typeof(IMessageHandler<>))
                .Select(i => i.GetGenericArguments()[0]))
            .Distinct();
    }
}
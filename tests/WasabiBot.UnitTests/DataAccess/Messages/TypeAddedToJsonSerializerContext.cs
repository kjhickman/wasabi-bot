using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess;

namespace WasabiBot.UnitTests.DataAccess.Messages;

public class TypeAddedToJsonSerializerContext
{
    [Test]
    public void MyTest()
    {
        var handlerTypes = FindTypesImplementingInterface(typeof(IMessageHandler<>));
        foreach (var handlerType in handlerTypes)
        {
            var handlerJsonTypeInfo = DataAccessJsonContext.Default.GetTypeInfo(handlerType);
            if (handlerJsonTypeInfo is null)
            {
                Assert.Fail($"Type {handlerType.Name} was not added to the JsonSerializerContext");
            }
        }
    }
    
    private IEnumerable<Type> FindTypesImplementingInterface(Type interfaceType)
    {
        var assemblies = System.Reflection.Assembly.Load("WasabiBot.DataAccess");
        
        return assemblies.GetTypes().Where(t => interfaceType.IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });
    }
}
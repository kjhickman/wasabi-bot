using System.Reflection;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Infrastructure.Discord.Abstractions;

public abstract class CommandBase
{
    public abstract string Command { get; }
    public abstract string Description { get; }
    public Delegate Handler => _handler ??= CreateHandler();

    private Delegate? _handler;

    private Delegate CreateHandler()
    {
        var method = GetType()
                         .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .FirstOrDefault(m => m.GetCustomAttribute<CommandEntryAttribute>() is not null)
                     ?? throw new InvalidOperationException($"No [CommandEntryAttribute] on {GetType().Name}.");

        var paramTypes = method.GetParameters().Select(p => p.ParameterType).ToList();
        var types = paramTypes.Append(method.ReturnType).ToArray();
        var delegateType = System.Linq.Expressions.Expression.GetDelegateType(types);

        return method.CreateDelegate(delegateType, this);
    }
}

namespace WasabiBot.Api.Infrastructure.Discord.Interactions;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MessageCommandHandlerAttribute(string name, string entryPoint = "ExecuteAsync") : Attribute
{
    public string Name { get; } = name;
    public string EntryPoint { get; } = entryPoint;
}

namespace WasabiBot.Api.Infrastructure.Discord.Interactions;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CommandHandlerAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }
    public string EntryPoint { get; }

    public CommandHandlerAttribute(string name, string description, string entryPoint = "ExecuteAsync")
    {
        Name = name;
        Description = description;
        EntryPoint = entryPoint;
    }
}

namespace WasabiBot.Api.Modules;

internal interface ISlashCommand
{
    string Name { get; }
    string Description { get; }
    void Register(WebApplication app);
}


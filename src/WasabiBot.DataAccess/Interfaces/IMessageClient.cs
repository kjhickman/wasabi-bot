namespace WasabiBot.DataAccess.Interfaces;

public interface IMessageClient
{
    Task SendAsync<T>(T message) where T : class;
}

namespace WasabiBot.DataAccess.Interfaces;

public interface IMessageHandler<in T>
{
    Task HandleAsync(T message, CancellationToken cancellationToken);
}

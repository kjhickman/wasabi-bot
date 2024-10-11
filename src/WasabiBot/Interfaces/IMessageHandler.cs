using WasabiBot.Core;

namespace WasabiBot.Interfaces;

public interface IMessageHandler<T> where T : IMessage
{
    Type MessageType  => typeof(T);
    Task<Result> Handle(IMessage message, CancellationToken ct = default);
}

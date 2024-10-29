using FluentResults;

namespace WasabiBot.Core.Interfaces;

public interface IMessageHandler<T> where T : IMessage
{
    Type MessageType  => typeof(T);
    Task<Result> Handle(IMessage message, CancellationToken ct = default);
}

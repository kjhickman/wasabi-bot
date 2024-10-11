using WasabiBot.Core;

namespace WasabiBot.Interfaces;

public interface IMessageClient
{
    Task<Result> SendMessage<T>(T message) where T : IMessage;
}

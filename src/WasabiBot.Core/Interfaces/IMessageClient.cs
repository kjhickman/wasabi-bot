using WasabiBot.Core.Models;

namespace WasabiBot.Core.Interfaces;

public interface IMessageClient
{
    Task<Result> SendMessage<T>(T message) where T : IMessage;
}

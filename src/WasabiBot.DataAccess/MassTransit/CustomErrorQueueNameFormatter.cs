using MassTransit;

namespace WasabiBot.DataAccess.MassTransit;

public class CustomErrorQueueNameFormatter : IErrorQueueNameFormatter
{
    public string FormatErrorQueueName(string queueName)
    {
        return queueName + "-error";
    }
}
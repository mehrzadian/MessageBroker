using System;

namespace MessageBroker.Core
{
    public interface IMessageConsumer
    {
        Guid ConsumerId { get; }
        string ConsumerName { get; }
        string DllPath { get; }
        string Topic { get; } 
        void ConsumeMessage(string topic, string message);
        int ThreadCount { get; }
        int RateLimit { get; }
    }
}
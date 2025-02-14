using System;

namespace MessageBroker.Core
{
    public interface IMessageProducer
    {
        Guid ProducerId { get; }
        string ProducerName { get; }
        string DllPath { get; }
        string Topic { get; } 
        void PublishMessage(string topic, string message);
        int RetryCount { get; }
        int ThreadCount { get; }
        int RateLimit { get; }
    }
}
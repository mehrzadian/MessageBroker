using MessageBroker.Core;
using System;
using System.Collections.Concurrent;

namespace MessageBroker.API.Services
{
    public class IdempotencyService
    {
        public readonly ConcurrentDictionary<Guid, bool> _processedRequests = new ConcurrentDictionary<Guid, bool>();
        public readonly List<IMessageConsumer> _consumers = new List<IMessageConsumer>();
        public bool IsProcessed(Guid idempotencyKey)
        {
            return _processedRequests.ContainsKey(idempotencyKey);
        }

        public void MarkProcessed(Guid idempotencyKey)
        {
            _processedRequests.TryAdd(idempotencyKey, true);
        }
    }
}
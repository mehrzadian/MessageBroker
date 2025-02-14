using System;

namespace MessageBroker.Core
{
    public class MessageMetadata
    {
        public Guid MessageId { get; set; }
        public string Topic { get; set; }
        public string Payload { get; set; }
        public long SequenceNumber { get; set; }
        public DateTime Timestamp { get; set; }
        public Guid? ProducerId { get; set; }
    }
}
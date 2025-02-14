using System;

namespace MessageBroker.API.Models
{
    public class ConsumerRegistrationModel
    {
        public Guid ConsumerId { get; set; }
        public string Topic { get; set; }
    }
}
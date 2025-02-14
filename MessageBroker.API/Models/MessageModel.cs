using System;
using System.ComponentModel.DataAnnotations;

namespace MessageBroker.API.Models
{
    public class MessageModel
    {
        [Required]
        public string Message { get; set; }

        [Required]
        public Guid IdempotencyKey { get; set; }
    }
}
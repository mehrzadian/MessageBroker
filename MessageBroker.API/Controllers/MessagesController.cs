using Microsoft.AspNetCore.Mvc;
using MessageBroker.Broker;
using Microsoft.Extensions.Logging;
using MessageBroker.API.Models;
using MessageBroker.Core; 
using System.Reflection; 
using System.IO; 
using System.Collections.Generic; 
using System;
using System.Threading;
using MessageBroker.API.Services;

namespace MessageBroker.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MessagesController : ControllerBase
    {
        private readonly BrokerService _brokerService;
        private readonly ILogger<MessagesController> _logger;
        private readonly IdempotencyService _idempotencyService;

        public MessagesController(BrokerService brokerService, ILogger<MessagesController> logger, IdempotencyService idempotencyService)
        {
            _brokerService = brokerService;
            _logger = logger;
            _idempotencyService = idempotencyService;
        }

        [HttpPost]
        public IActionResult PublishMessage([FromQuery] string topic, [FromBody] MessageModel messageModel)
        {
            if (_idempotencyService.IsProcessed(messageModel.IdempotencyKey))
            {
                _logger.LogInformation($"Duplicate message received for topic {topic} with idempotency key {messageModel.IdempotencyKey}.");
                return Accepted(); 
            }

            _logger.LogInformation($"Received message for topic {topic} via API");
            _brokerService.Publish(topic, messageModel.Message);
            _idempotencyService.MarkProcessed(messageModel.IdempotencyKey);

            return Accepted();
        }

        
        [HttpPost("RegisterConsumer")]
        public IActionResult RegisterConsumer([FromBody] ConsumerRegistrationModel registration)
        {
            _logger.LogInformation($"Received consumer registration request for ConsumerId: {registration.ConsumerId}, Topic: {registration.Topic}");

            bool _isAdded = false;

            
            List<IMessageConsumer> _consumerPlugins = new List<IMessageConsumer>();
            string pluginDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..", "Plugins");

            string[] pluginFiles = Directory.GetFiles(pluginDirectory, "*.dll");

            
            foreach (string pluginFile in pluginFiles)
            {
                try
                {
                    Assembly pluginAssembly = Assembly.LoadFrom(pluginFile);
                    Type[] types = pluginAssembly.GetTypes();

                    foreach (Type type in types)
                    {
                        if (typeof(IMessageConsumer).IsAssignableFrom(type) && !type.IsInterface)
                        {
                            
                            IMessageConsumer consumer = (IMessageConsumer)Activator.CreateInstance(type) as IMessageConsumer;

                            if (consumer != null)
                            {
                                
                                _logger.LogInformation($"Add {type.Name} to consumer list");

                                
                                if (!_brokerService._consumers.Contains(consumer))
                                {
                                    _brokerService.Subscribe(registration.Topic, consumer);
                                    _isAdded = true;
                                }
                            }
                            else
                            {
                                _logger.LogError($"Could not create instance of {type.Name} from {pluginFile}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error loading plugin {pluginFile}");
                }
            }

            if (!_isAdded)
            {
                _logger.LogWarning($"Could not find the consumer");
                return BadRequest();
            }

            return Ok();
        }
    }
}
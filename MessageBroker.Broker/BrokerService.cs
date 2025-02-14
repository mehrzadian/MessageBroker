using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MessageBroker.Core;
using Microsoft.Extensions.Logging;

namespace MessageBroker.Broker
{
    public class BrokerService
    {
        private static readonly string DataFile = "messages.txt";
        private static readonly string SubscriptionFile = "subscriptions.txt"; 
        private readonly ConcurrentDictionary<string, BlockingCollection<MessageMetadata>> _topics = new ConcurrentDictionary<string, BlockingCollection<MessageMetadata>>();
        public readonly List<IMessageConsumer> _consumers = new List<IMessageConsumer>();
        private readonly List<IMessageProducer> _producers = new List<IMessageProducer>();
        private long _sequenceNumberCounter = 0;
        private readonly string _pluginsDirectory;
        private readonly ILogger<BrokerService> _logger;
        
        private readonly ConcurrentDictionary<Guid, List<string>> _consumerSubscriptions = new ConcurrentDictionary<Guid, List<string>>();

        public BrokerService(ILogger<BrokerService> logger)
        {
            _logger = logger;
            _pluginsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..", "Plugins");
            LoadPlugins();
            LoadMessagesFromDataFile();
            LoadSubscriptionsFromFile(); 
        }

        public void Publish(string topic, string message, Guid? producerId = null)
        {
            BlockingCollection<MessageMetadata> queue = _topics.GetOrAdd(topic, (t) => new BlockingCollection<MessageMetadata>());

            var messageData = new MessageMetadata()
            {
                MessageId = Guid.NewGuid(),
                Topic = topic,
                Payload = message,
                SequenceNumber = Interlocked.Increment(ref _sequenceNumberCounter),
                Timestamp = DateTime.UtcNow,
                ProducerId = producerId
            };

            queue.Add(messageData);
            PersistMessageToFile(messageData);
            _logger.LogInformation($"Published message to topic: {topic}, MessageId: {messageData.MessageId}");
        }

        private void LoadMessagesFromDataFile()
        {
            if (!File.Exists(DataFile)) return;

            foreach (string line in File.ReadLines(DataFile))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] parts = line.Split('|');

                if (parts.Length != 6) continue;

                try
                {
                    MessageMetadata message = new MessageMetadata
                    {
                        MessageId = Guid.Parse(parts[0]),
                        Topic = parts[1],
                        Payload = parts[2],
                        SequenceNumber = long.Parse(parts[3]),
                        Timestamp = DateTime.Parse(parts[4]),
                        ProducerId = string.IsNullOrEmpty(parts[5]) ? null : Guid.Parse(parts[5]),
                    };

                    BlockingCollection<MessageMetadata> queue = _topics.GetOrAdd(message.Topic, (t) => new BlockingCollection<MessageMetadata>());
                    queue.Add(message);

                    _sequenceNumberCounter = Math.Max(_sequenceNumberCounter, message.SequenceNumber);
                    _logger.LogInformation($"Loaded message from file: {message.MessageId}, Topic: {message.Topic}");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Error loading message from file: {line}");
                }
            }
        }

        private void StartConsumerForTopic(string topic, IMessageConsumer consumer)
        {
            Task.Run(() =>
            {
                try
                {
                    if (!_topics.ContainsKey(topic))
                    {
                        _logger.LogWarning($"Topic {topic} does not exist. Consumer {consumer.ConsumerId} not started.");
                        return;
                    }
                    BlockingCollection<MessageMetadata> queue = _topics[topic];
                    _logger.LogInformation($"Consumer {consumer.ConsumerId} starting to listen to {topic}");
                    foreach (var message in queue.GetConsumingEnumerable())
                    {
                        try
                        {
                            Thread.Sleep(consumer.RateLimit);
                            consumer.ConsumeMessage(topic, message.Payload);
                            _logger.LogInformation($"Consumer {consumer.ConsumerId} consumed message {message.MessageId} from topic {topic}");
                            AckMessage(message.MessageId); 
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, $"Consumer {consumer.ConsumerId} failed to process message {message.MessageId} from topic {topic}");
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Error consuming from topic {topic}");
                }
            });
        }

        private async void PersistMessageToFile(MessageMetadata message)
        {
            string line = $"{message.MessageId}|{message.Topic}|{message.Payload}|{message.SequenceNumber}|{message.Timestamp}|{message.ProducerId}";
            await File.AppendAllTextAsync(DataFile, line + Environment.NewLine);
        }

        private void LoadPlugins()
        {
            if (!Directory.Exists(_pluginsDirectory))
            {
                _logger.LogWarning($"Plugins directory {_pluginsDirectory} does not exist.");
                return;
            }

            string[] pluginFiles = Directory.GetFiles(_pluginsDirectory, "*.dll");

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
                                
                                _consumers.Add(consumer);
                                _logger.LogInformation($"Loaded Consumer: {type.Name} from {pluginFile}");
                            }
                            else
                            {
                                _logger.LogError($"Could not create instance of {type.Name} from {pluginFile}");
                            }
                        }
                        else if (typeof(IMessageProducer).IsAssignableFrom(type) && !type.IsInterface)
                        {
                            
                            IMessageProducer producer = (IMessageProducer)Activator.CreateInstance(type) as IMessageProducer;
                            if (producer != null)
                            {
                                _producers.Add(producer);
                                _logger.LogInformation($"Loaded Producer: {type.Name} from {pluginFile}");
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
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        public void Subscribe(string topic, IMessageConsumer consumer)
        {
            if (!_consumers.Contains(consumer))
                _consumers.Add(consumer);

            _logger.LogInformation($"Adding subscriber. {topic}");
            //Add subscription
            _consumerSubscriptions.AddOrUpdate(consumer.ConsumerId, new List<string>() { topic }, (key, val) => {
                val.Add(topic);
                return val;
            });
            PersistSubscriptionsToFile();
            StartConsumerForTopic(topic, consumer);
        }

        public void AckMessage(Guid messageId)
        {
            try
            {

                var lines = File.ReadLines(DataFile).ToList();


                var lineToRemove = lines.FirstOrDefault(line => line.StartsWith(messageId.ToString()));

                if (lineToRemove != null)
                {

                    lines.Remove(lineToRemove);


                    File.WriteAllLines(DataFile, lines);
                    _logger.LogInformation($"Removed message with ID {messageId} from file.");
                }
                else
                {
                    _logger.LogWarning($"Message with ID {messageId} not found in file.");
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error acknowledging message with ID {messageId}.");
            }
        }

        private void LoadSubscriptionsFromFile()
        {
            if (!File.Exists(SubscriptionFile)) return;

            foreach (string line in File.ReadLines(SubscriptionFile))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] parts = line.Split('|');
                if (parts.Length != 2)
                {
                    _logger.LogWarning($"Invalid subscription line: {line}");
                    continue;
                }

                if (Guid.TryParse(parts[0], out Guid consumerId))
                {
                    string topic = parts[1];
                    
                    if (_consumers.Any(t => t.ConsumerId == consumerId))
                    {
                        StartConsumerForTopic(topic, _consumers.First(t => t.ConsumerId == consumerId));
                        
                        _consumerSubscriptions.AddOrUpdate(consumerId, new List<string>() { topic }, (key, val) => {
                            val.Add(topic);
                            return val;
                        });
                    }
                    else
                    {
                        _logger.LogWarning($"Invalid consumer name.");
                    }
                }
                else
                {
                    _logger.LogWarning($"Invalid subscription line.");
                }

            }
        }

        private void PersistSubscriptionsToFile()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (var subscription in _consumerSubscriptions)
                {
                    foreach (var topic in subscription.Value)
                        lines.Add($"{subscription.Key}|{topic}");
                }
                File.WriteAllLines(SubscriptionFile, lines);
                _logger.LogInformation($"Wrote all subscriptions");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error persisting subscriptions to file.");
            }
        }
    }
}
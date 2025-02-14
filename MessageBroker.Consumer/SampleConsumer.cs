using System;
using MessageBroker.Core;
using Newtonsoft.Json;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace MessageBroker.Consumer
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ConsumerImplementationData : Attribute
    {
        public ConsumerImplementationData(string topic, int threadCount, int rateLimit, int interval)
        {
            Topic = topic;
            ThreadCount = threadCount;
            RateLimit = rateLimit;
            Interval = interval;
        }

        public string Topic { get; }
        public int ThreadCount { get; }
        public int RateLimit { get; }
        public int Interval { get; }
    }


    [ConsumerImplementationData(topic: "TestTopic", threadCount: 4, rateLimit: 100, interval: 4000)] //Set the threadCount here
    [BrokerPlugin(PluginName = "SampleConsumer")]
    public class SampleConsumer : IMessageConsumer, IDisposable
    {
        public Guid ConsumerId { get; } = Guid.NewGuid();
        public string ConsumerName { get; } = "SampleConsumer";
        public string DllPath { get; } = "MessageBroker.Consumer.dll";

        private Timer _timer;
        private readonly BlockingCollection<string> _messageQueue = new BlockingCollection<string>();
        private int _messagesConsumed = 0;

        public SampleConsumer()
        {
            
            _timer = new Timer(ConsumeMessages, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(Interval));
        }

        public string Topic
        {
            get
            {
                var attribute = (ConsumerImplementationData)Attribute.GetCustomAttribute(this.GetType(), typeof(ConsumerImplementationData));
                return attribute?.Topic ?? "DefaultTopic";
            }
        }

        public int ThreadCount
        {
            get
            {
                var attribute = (ConsumerImplementationData)Attribute.GetCustomAttribute(this.GetType(), typeof(ConsumerImplementationData));
                return attribute?.ThreadCount ?? 0;
            }
        }

        public int RateLimit
        {
            get
            {
                var attribute = (ConsumerImplementationData)Attribute.GetCustomAttribute(this.GetType(), typeof(ConsumerImplementationData));
                return attribute?.RateLimit ?? 0;
            }
        }

        public int Interval
        {
            get
            {
                var attribute = (ConsumerImplementationData)Attribute.GetCustomAttribute(this.GetType(), typeof(ConsumerImplementationData));
                return attribute?.Interval ?? 0;
            }
        }

        
        public void ConsumeMessage(string topic, string message)
        {
            _messageQueue.Add(message);
        }

        private async void ConsumeMessages(object state)
        {
            
            int threadCount = ThreadCount;
            
            threadCount = threadCount > Environment.ProcessorCount ? Environment.ProcessorCount : threadCount;
           
            int rateLimit = RateLimit;

            var tasks = new List<Task>();

            for (int i = 0; i < threadCount && _messagesConsumed < rateLimit; i++)
            {
                
                var task = Task.Run(async () =>
                {
                    try
                    {
                        
                        if (_messageQueue.TryTake(out string message))
                        {
                            
                            Interlocked.Increment(ref _messagesConsumed);
                            Console.WriteLine($"Consumer {ConsumerName} consumed message {message} from topic {Topic} on thread {Thread.CurrentThread.ManagedThreadId}");
                            await Task.Delay(1000); 
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error consuming message: {ex.Message}");
                    }
                });
                tasks.Add(task);
            }
            await Task.WhenAll(tasks);

            
            _messagesConsumed = 0;
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _messageQueue?.Dispose();
        }
    }
}
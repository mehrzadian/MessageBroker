using System;
using MessageBroker.Core;
using Newtonsoft.Json;
using System.Linq;
using System.Threading;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MessageBroker.Producer
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ProducerImplementationData : Attribute
    {
        public ProducerImplementationData(int retryNumber, string topic, int threadCount, int rateLimit, int interval)
        {
            RetryNumber = retryNumber;
            Topic = topic;
            ThreadCount = threadCount;
            RateLimit = rateLimit;
            Interval = interval;
        }
        public int RetryNumber { get; }

        public string Topic { get; } 
        public int ThreadCount { get; }
        public int RateLimit { get; }
        public int Interval { get; }
    }

    [ProducerImplementationData(retryNumber: 3, topic: "TestTopic", threadCount: 3, rateLimit: 100, interval: 4000)] 
    [BrokerPlugin(PluginName = "RandomObjectProducer")]
    public class RandomObjectProducer : IMessageProducer
    {
        public Guid ProducerId { get; } = Guid.NewGuid();
        public string ProducerName { get; } = "RandomObjectProducer";
        public string DllPath { get; } = "MessageBroker.Producer.dll";

        private Timer _timer;
        private readonly Random _random = new Random();

        private static readonly HttpClient client = new HttpClient();

        public RandomObjectProducer()
        {
            
            _timer = new Timer(SendMessage, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(Interval));
        }

        
        public string Topic
        {
            get
            {
                var attribute = (ProducerImplementationData)Attribute.GetCustomAttribute(this.GetType(), typeof(ProducerImplementationData));
                return attribute?.Topic ?? "DefaultTopic"; // Default to 0 if attribute is not found
            }
        }

        
        public int RetryCount
        {
            get
            {
                var attribute = (ProducerImplementationData)Attribute.GetCustomAttribute(this.GetType(), typeof(ProducerImplementationData));
                return attribute?.RetryNumber ?? 0; 
            }
        }

        
        public int ThreadCount
        {
            get
            {
                var attribute = (ProducerImplementationData)Attribute.GetCustomAttribute(this.GetType(), typeof(ProducerImplementationData));
                return attribute?.ThreadCount ?? 0; 
            }
        }

        
        public int RateLimit
        {
            get
            {
                var attribute = (ProducerImplementationData)Attribute.GetCustomAttribute(this.GetType(), typeof(ProducerImplementationData));
                return attribute?.RateLimit ?? 0; 
            }
        }

        
        public int Interval
        {
            get
            {
                var attribute = (ProducerImplementationData)Attribute.GetCustomAttribute(this.GetType(), typeof(ProducerImplementationData));
                return attribute?.Interval ?? 0; 
            }
        }

        
        public void Dispose()
        {
            _timer?.Dispose();
        }

        public void PublishMessage(string topic, string message)
        {
        }


        private async void SendMessage(object state)
        {
            try
            {

                var randomObject = GenerateRandomObject();


                string payload = JsonConvert.SerializeObject(randomObject);

                string APIURL = "https://localhost:7085/api/Messages?topic=" + this.Topic;

                var messageContent = new
                {
                    Message = payload,
                    IdempotencyKey = Guid.NewGuid()
                };

                var content = new StringContent(JsonConvert.SerializeObject(messageContent), Encoding.UTF8, "application/json");

                int retryAttempts = RetryCount;
                for (int i = 0; i < retryAttempts; i++)
                {
                    try
                    {
                        HttpResponseMessage response = await client.PostAsync(APIURL, content);

                        Console.WriteLine($"Producer {ProducerName} sending: {payload} to url " + APIURL + " with code " + response.StatusCode.ToString());

                        if (response.IsSuccessStatusCode)
                        {
                            
                            break;
                        }
                        else
                        {
                            Console.WriteLine($"Failed with code " + response.StatusCode.ToString());
                            string errorContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"API responded with: {response.StatusCode} - {errorContent}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                    
                    Thread.Sleep(i * 1000);
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }


        private object GenerateRandomObject()
        {
            
            var obj = new
            {
                Id = Guid.NewGuid(),
                Value = _random.Next(1, 100),
                Text = GenerateRandomString(10)
            };
            return obj;
        }

        
        private string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[_random.Next(s.Length)]).ToArray());
        }
    }
}
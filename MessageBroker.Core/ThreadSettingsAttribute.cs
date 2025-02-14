using System;

namespace MessageBroker.Core
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ThreadSettingsAttribute : Attribute
    {
        public int ThreadCount { get; }
        public int RateLimit { get; } 

        public ThreadSettingsAttribute(int threadCount, int rateLimit)
        {
            ThreadCount = threadCount;
            RateLimit = rateLimit;
        }
    }
}
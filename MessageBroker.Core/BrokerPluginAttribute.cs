using System;

namespace MessageBroker.Core
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class BrokerPluginAttribute : Attribute
    {
        public string PluginName { get; set; }
    }
}
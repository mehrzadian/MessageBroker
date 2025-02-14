using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MessageBroker.Broker;
using Microsoft.Extensions.Configuration;

public class Program
{
    public static void Main(string[] args)
    {
        
        CreateHostBuilder(args).Build().Run();
        
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddDebug();
                    builder.AddFile("Logs/app.txt"); 
                    builder.AddConfiguration(hostContext.Configuration.GetSection("Logging"));
                });

                services.AddSingleton<BrokerService>(); 
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddDebug();
                logging.AddConfiguration(logging.Services.BuildServiceProvider().GetService<IConfiguration>());

            })
            .UseConsoleLifetime();
            
}
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string filePath)
    {
        builder.Services.AddSingleton<ILoggerProvider>(sp => new FileLoggerProvider(filePath));
        return builder;
    }
}

public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;

    public FileLoggerProvider(string filePath)
    {
        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, _filePath);
    }

    public void Dispose()
    {
        
    }
}

public class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly string _filePath;

    public FileLogger(string categoryName, string filePath)
    {
        _categoryName = categoryName;
        _filePath = filePath;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return null; 
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        
        return true; 
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string logMessage = formatter(state, exception);
        string logLine = $"{DateTime.Now} [{logLevel}] {_categoryName}: {logMessage}{Environment.NewLine}";

        try
        {
            File.AppendAllText(_filePath, logLine);
        }
        catch (Exception ex)
        {
            
            Console.Error.WriteLine($"Error writing to log file: {ex.Message}");
        }
    }
}
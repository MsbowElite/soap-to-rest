using CsharpRest.Application.Interfaces;
using Serilog;

namespace CsharpRest.Infrastructure.Logging;

public class SerilogLoggerService : ILoggerService
{
    public void Information(string messageTemplate, params object?[] propertyValues)
    {
        Log.Information(messageTemplate, propertyValues);
    }

    public void Warning(string messageTemplate, params object?[] propertyValues)
    {
        Log.Warning(messageTemplate, propertyValues);
    }

    public void Error(Exception? exception, string messageTemplate, params object?[] propertyValues)
    {
        if (exception is not null)
        {
            Log.Error(exception, messageTemplate, propertyValues);
        }
        else
        {
            Log.Error(messageTemplate, propertyValues);
        }
    }

    public void Debug(string messageTemplate, params object?[] propertyValues)
    {
        Log.Debug(messageTemplate, propertyValues);
    }
}

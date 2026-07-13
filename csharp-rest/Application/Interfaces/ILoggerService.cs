namespace CsharpRest.Application.Interfaces;

public interface ILoggerService
{
    void Information(string messageTemplate, params object?[] propertyValues);
    void Warning(string messageTemplate, params object?[] propertyValues);
    void Error(Exception? exception, string messageTemplate, params object?[] propertyValues);
    void Debug(string messageTemplate, params object?[] propertyValues);
}

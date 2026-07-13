using System.Diagnostics;

namespace CsharpRest.Infrastructure.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string HeaderName = CsharpRest.Application.Constants.Headers.CorrelationId;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        }

        context.Items[HeaderName] = correlationId.ToString();
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId.ToString();
            return Task.CompletedTask;
        });

        using (var activity = new Activity("CorrelationId"))
        {
            activity.SetIdFormat(ActivityIdFormat.W3C);
            activity.SetParentId(correlationId.ToString());
            activity.Start();
            try
            {
                await _next(context);
            }
            finally
            {
                activity.Stop();
            }
        }
    }
}

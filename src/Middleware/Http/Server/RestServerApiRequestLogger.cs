using Microsoft.AspNetCore.Http;

namespace AKSMiddleware;
public class RestServerApiRequestLogger(RequestDelegate next, Serilog.ILogger logger)
{
    private readonly RequestDelegate _next = next;
    private readonly Serilog.ILogger _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;        
        try 
        {
            // Call the next middleware
            await _next(context); 
        }
        finally
        {
            Logging.LogRequest(new LogRequestParams(
                _logger,
                startTime,
                context.Request,
                context.Response,
                null
            ));
        }
    }
}

using Microsoft.AspNetCore.Http;

namespace AKSMiddleware;

public class RestServerApiRequestLogger(RequestDelegate next, Serilog.ILogger logger)
{
    private readonly RequestDelegate _next = next;
    private readonly Serilog.ILogger _logger = logger;

    // <summary>
    // Middleware to log HTTP requests and responses for Rest servers. 
    // The method gets called when the middleware is registerted and invoked in Startup.
    // </summary>
    // <remarks>
    // This middleware captures the start time of the request, processes the request, and then logs the details of the request and response.
    // It uses the Serilog logger to log the information.
    // </remarks>
    // <param name="next">The next middleware in the pipeline.</param>
    // <param name="logger">The Serilog logger instance.</param>
    // <returns>A task representing the asynchronous operation.</returns>  
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

using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog;
using Serilog.Context;

namespace MiddlewareListInterceptors;

public class CtxLoggerInterceptor : Interceptor
{
    private readonly Serilog.ILogger _logger;

    public CtxLoggerInterceptor(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        // set source; can update with .PushProperty for entire service, or with .ForContext for limited scope
        LogContext.PushProperty("source", "CtxLog");

        // Note: creating a new ctxlogger to add things w/ ForContext (won't propagate to other interceptors)
        var ctxLogger = _logger.ForContext(Constants.MethodFieldKey, context.Method);
        ctxLogger.Information($"API handler logger output. req: {@request}");

        try
        {
            return await continuation(request, context);
        }
        catch (Exception ex)
        {
            ctxLogger.Error(ex, $"Error thrown by {context.Method}.");
            throw;
        }
    }
}

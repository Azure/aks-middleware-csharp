using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog;
using System;
using System.Threading.Tasks;

namespace AKSMiddleware;

// gRPC request log interceptor
public class ClientApiRequestLogger : Interceptor
{
    private readonly Serilog.ILogger _logger;

    public ClientApiRequestLogger(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        DateTime start = DateTime.Now;
        
        string requestId = string.Empty;
        var headers = context.Options.Headers;
        if (headers != null)
        {
            var headerValue = headers.GetValue(Constants.RequestIDMetadataKey);
            if (headerValue != null)
            {
                requestId = headerValue;
            }
        }

        var logger = _logger.WithServiceProperties(context.Method.FullName)
                            .ForContext(Constants.RequestIDLogKey, requestId)
                            .ForContext(Constants.ComponentFieldKey, Constants.ComponentValueClient)
                            .ForContext(Constants.MethodTypeFieldKey, context.Method.Type.ToString().ToLower())
                            .ForContext(Constants.StartTimeKey, start.ToString("yyyy-MM-ddTHH:mm:sszzz"))
                            // readding here since source field seems to get removed, even after adding in interceptor.cs
                            .ForContext("source", "ApiRequestLog");

        var response = continuation(request, context);
        Task.Run(() => HandleResponse(response, start, logger));
        return response;
    }

    private async Task<TResponse> HandleResponse<TResponse>(AsyncUnaryCall<TResponse> call, DateTime start, Serilog.ILogger logger)
    {
        try
        {
            var response = await call.ResponseAsync;
            var duration = DateTime.Now - start;
            var status = call.GetStatus();

            logger = logger.ForContext(Constants.StatusCodeKey, status.StatusCode)
                           .ForContext(Constants.TimeMsKey, duration.TotalMilliseconds);

            logger.Information("finished call");
            return response;
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Call error: {ex.Message}");
            throw;
        }
    }
}
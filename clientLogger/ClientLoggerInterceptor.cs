using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog;
using Serilog.Context;

namespace MiddlewareListInterceptors;

// Client-side interceptor for logging
public class ClientLoggerInterceptor : Interceptor
{
    private readonly Serilog.ILogger _logger;

    public ClientLoggerInterceptor(Serilog.ILogger logger)
    {
        _logger = logger.ForContext("source", "ApiRequestLog");
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        // LogCall<TRequest, TResponse>(context.Method);

        DateTime start = DateTime.Now;
        
        LogContext.PushProperty(Constants.SystemTag[0], Constants.SystemTag[1]);
        LogContext.PushProperty(Constants.ComponentFieldKey, Constants.KindClientFieldValue);
        LogContext.PushProperty(Constants.MethodFieldKey, context.Method.Name);
        LogContext.PushProperty(Constants.MethodTypeFieldKey, context.Method.Type.ToString().ToLower());
        LogContext.PushProperty(Constants.ComponentFieldKey, Constants.KindClientFieldValue);
        
        var headers = context.Options.Headers;
        string requestId = string.Empty;
        if (headers != null)
        {
            var headerValue = headers.GetValue(Constants.RequestIDMetadataKey);
            if (headerValue != null)
            {
                requestId = headerValue;
            }
        }
        LogContext.PushProperty(Constants.RequestIDLogKey, requestId);

        string serviceName = ExtractServiceName(context.Method.FullName);
        LogContext.PushProperty(Constants.ServiceFieldKey, serviceName);

        // // Log the start of the call
        // _logger.Information("Starting call. Type/Method: {Type} / {Method}",
        //     context.Method.Type, context.Method.Name);

        // Continue the call chain
        var response = continuation(request, context);

        // Handle response asynchronously
        Task.Run(() => HandleResponse(response, start));

        return response;
    }

    private async Task<TResponse> HandleResponse<TResponse>(AsyncUnaryCall<TResponse> call, DateTime start)
    {
        try
        {
            var response = await call.ResponseAsync;

            var duration = DateTime.Now - start;
            LogContext.PushProperty(Constants.TimeMsKey, duration.TotalMilliseconds);
            LogContext.PushProperty(Constants.StartTimeKey, start.ToString("yyyy-MM-ddTHH:mm:sszzz"));
            
            var status = call.GetStatus();
            LogContext.PushProperty(Constants.StatusCodeKey, status.StatusCode);

            _logger.Information("finished call");
            return response;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Call error: {ex.Message}");
            throw;
        }
    }

    private void LogCall<TRequest, TResponse>(Method<TRequest, TResponse> method)
        where TRequest : class
        where TResponse : class
    {
        _logger.Information($"Starting call. Name: {method.Name}. Type: {method.Type}. Request: {typeof(TRequest)}. Response: {typeof(TResponse)}");
    }

    private string ExtractServiceName(string fullMethodName)
    {
        var parts = fullMethodName.Split('/');
        if (parts.Length < 2)
        {
            throw new InvalidOperationException("Unexpected gRPC method format.");
        }
        var serviceNameWithPackage = parts[1];
        var serviceParts = serviceNameWithPackage.Split('.');
        return serviceParts[^1]; // Get the last part
    }
}
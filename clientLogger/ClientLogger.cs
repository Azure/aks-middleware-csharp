using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog;
using Serilog.Context;

namespace AKSMiddleware;

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
        DateTime start = DateTime.Now;
        
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

        string serviceName = ExtractServiceName(context.Method.FullName);

        var logger = _logger.ForContext(Constants.ServiceFieldKey, serviceName)
                            .ForContext(Constants.RequestIDLogKey, requestId)
                            .ForContext(Constants.SystemTag[0], Constants.SystemTag[1])
                            .ForContext(Constants.ComponentFieldKey, Constants.KindClientFieldValue)
                            .ForContext(Constants.MethodFieldKey, context.Method.Name)
                            .ForContext(Constants.MethodTypeFieldKey, context.Method.Type.ToString().ToLower())
                            .ForContext(Constants.ComponentFieldKey, Constants.KindClientFieldValue);

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
                           .ForContext(Constants.TimeMsKey, duration.TotalMilliseconds)
                           .ForContext(Constants.StartTimeKey, start.ToString("yyyy-MM-ddTHH:mm:sszzz"));

            logger.Information("finished call");
            return response;
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Call error: {ex.Message}");
            throw;
        }
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
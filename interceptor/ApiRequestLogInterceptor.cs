using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog;
using System;
using Serilog.Context;
using System.Diagnostics;

namespace MiddlewareListInterceptors;

// Server-side interceptor for logging
public class ApiRequestLogInterceptor : Interceptor
{
    private readonly Serilog.ILogger _logger;

    public ApiRequestLogInterceptor(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {

        DateTime start = DateTime.Now;
        string peerAddress = ParsePeerAddress(context.Peer);

        // LogCall<TRequest, TResponse>(MethodType.Unary, context);

        var apiLogger = _logger.ForContext("source", "ApiRequestLog")
                            .ForContext(Constants.SystemTag[0], Constants.SystemTag[1])
                            .ForContext(Constants.ComponentFieldKey, Constants.KindServerFieldValue)
                            .ForContext(Constants.MethodTypeFieldKey, MethodType.Unary.ToString().ToLower())
                            .ForContext(Constants.StartTimeKey, start.ToString("yyyy-MM-ddTHH:mm:sszzz"))
                            .ForContext(Constants.PeerAddressKey, peerAddress);

        SetServiceProperties(context, ref apiLogger);

        try
        {
            return await continuation(request, context);
        }
        catch (Exception ex)
        {
            apiLogger.Error(ex, $"Error thrown by {context.Method}.");
            throw;
        }
        finally
        {
            var duration = DateTime.Now - start;
            apiLogger = apiLogger.ForContext(Constants.StatusCodeKey, context.Status.StatusCode)
                                .ForContext(Constants.TimeMsKey, duration.TotalMilliseconds);
            
            apiLogger.Information("finished call");
        }
    }

    private void LogCall<TRequest, TResponse>(MethodType methodType, ServerCallContext context)
        where TRequest : class
        where TResponse : class
    {
        _logger.Warning($"Starting call. Type: {methodType}. Request: {typeof(TRequest)}. Response: {typeof(TResponse)}");
    }

    private void SetServiceProperties(ServerCallContext context, ref ILogger logger)
    {
        // TODO: This is a hacky way to get the service name and method name.
        // https://grpc.github.io/grpc/csharp-dotnet/api/Grpc.Core.ServerCallContext.html
        
        // Method format: "/package.Service/Method"
        var fullMethodName = context.Method;
        var parts = fullMethodName.Split('/');
        if (parts.Length < 3)
        {
            throw new InvalidOperationException("Unexpected gRPC method format.");
        }

        var serviceNameWithPackage = parts[1];
        var methodName = parts[2];

        // Extract just the service name from package.Service
        var serviceParts = serviceNameWithPackage.Split('.');
        var serviceName = serviceParts[^1]; // Get the last part

        logger = logger.ForContext(Constants.ServiceFieldKey, serviceName)
                        .ForContext(Constants.MethodFieldKey, methodName);
    }

    private string ParsePeerAddress(string peer)
    {
        if (peer.StartsWith("ipv4:"))
        {
            return peer.Substring("ipv4:".Length);
        }
        else if (peer.StartsWith("ipv6:"))
        {
            return peer.Substring("ipv6:".Length);
        }
        return peer;
    }
}
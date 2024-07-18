using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog;
using System;
using Serilog.Context;
using System.Diagnostics;

namespace MiddlewareListInterceptors;


// Server-side interceptor for logging
public class ServerLoggerInterceptor : Interceptor
{
    private readonly Serilog.ILogger _logger;

    public ServerLoggerInterceptor(Serilog.ILogger logger)
    {
        _logger = logger.ForContext("source", "ApiRequestLog");
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {

        DateTime start = DateTime.Now;

        // LogCall<TRequest, TResponse>(MethodType.Unary, context);

        LogContext.PushProperty(Constants.SystemTag[0], Constants.SystemTag[1]);
        LogContext.PushProperty(Constants.ComponentFieldKey, Constants.KindServerFieldValue);
        LogContext.PushProperty(Constants.MethodTypeFieldKey, MethodType.Unary.ToString().ToLower());
        LogContext.PushProperty(Constants.StartTimeKey, start.ToString("yyyy-MM-ddTHH:mm:sszzz"));

        string peerAddress = ParsePeerAddress(context.Peer);
        LogContext.PushProperty(Constants.PeerAddressKey, peerAddress);

        SetServiceProperties(context);

        try
        {
            return await continuation(request, context);
        }
        catch (Exception ex)
        {
            // Note: The gRPC framework also logs exceptions thrown by handlers to .NET Core logging.
            _logger.Error(ex, $"Error thrown by {context.Method}.");
            throw;
        }
        finally
        {
            LogContext.PushProperty(Constants.StatusCodeKey, context.Status.StatusCode);

            var duration = DateTime.Now - start;
            LogContext.PushProperty(Constants.TimeMsKey, duration.TotalMilliseconds);
            
            _logger.Information($"finished call");
        }
    }

    private void LogCall<TRequest, TResponse>(MethodType methodType, ServerCallContext context)
        where TRequest : class
        where TResponse : class
    {
        _logger.Warning($"Starting call. Type: {methodType}. Request: {typeof(TRequest)}. Response: {typeof(TResponse)}");
    }

    private void SetServiceProperties(ServerCallContext context)
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

        LogContext.PushProperty(Constants.ServiceFieldKey, serviceName);
        LogContext.PushProperty(Constants.MethodFieldKey, methodName);
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
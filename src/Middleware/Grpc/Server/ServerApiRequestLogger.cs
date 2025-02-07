using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog;
using System;
using System.Diagnostics;

namespace AKSMiddleware;

// Server-side interceptor for logging
public class ServerApiRequestLogger : Interceptor
{
    private readonly Serilog.ILogger _logger;

    public ServerApiRequestLogger(Serilog.ILogger logger)
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

        var apiRequestLogger = _logger.ForContext(Constants.ComponentFieldKey, Constants.ComponentValueServer)
                            .ForContext(Constants.MethodTypeFieldKey, MethodType.Unary.ToString().ToLower())
                            .ForContext(Constants.RequestIDLogKey, RequestIdInterceptor.GetRequestID(context))
                            .ForContext(Constants.StartTimeKey, start.ToString("yyyy-MM-ddTHH:mm:sszzz"))
                            .ForContext(Constants.PeerAddressKey, peerAddress)
                            .WithServiceProperties(context.Method);

        try
        {
            return await continuation(request, context);
        }
        catch (Exception ex)
        {
            apiRequestLogger.Error(ex, $"Error thrown by {context.Method}.");
            throw;
        }
        finally
        {
            var duration = DateTime.Now - start;
            apiRequestLogger = apiRequestLogger.ForContext(Constants.StatusCodeKey, context.Status.StatusCode)
                                               .ForContext(Constants.TimeMsKey, duration.TotalMilliseconds);

            apiRequestLogger.Information("finished call");
        }
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
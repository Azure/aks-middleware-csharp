using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog.Context;
using Serilog;

namespace MiddlewareListInterceptors;

public class RequestIdInterceptor : Interceptor
{
    private readonly ILogger _logger;

    public RequestIdInterceptor(ILogger logger)
    {
        _logger = logger.ForContext("source", "RequestIdInterceptor");
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        context = GenerateRequestID(context);

        var requestid = GetRequestID(context);

        LogContext.PushProperty(Constants.RequestIDLogKey, requestid);
        
        return await continuation(request, context);

    }

    private static ServerCallContext GenerateRequestID(ServerCallContext context)
    {
        if (context.RequestHeaders.GetValue(Constants.RequestIDMetadataKey) is null)
        {
            string shortId = ShortID();
            context.RequestHeaders.Add(Constants.RequestIDMetadataKey, shortId);
        }
        return context;
    }

    private static string ShortID()
    {
        byte[] buffer = new byte[6];
        Random random = new Random();
        random.NextBytes(buffer);
        return Base64UrlEncode(buffer);
    }

    private static string Base64UrlEncode(byte[] buffer)
    {
        return Convert.ToBase64String(buffer)
                        .TrimEnd('=')
                        .Replace('+', '-')
                        .Replace('/', '_');
    }

    public static string GetRequestID(ServerCallContext context)
    {
        return context.RequestHeaders.GetValue(Constants.RequestIDMetadataKey) 
           ?? context.ResponseTrailers.GetValue(Constants.RequestIDMetadataKey) 
           ?? string.Empty;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace MiddlewareListInterceptors;

public class RequestIdInterceptor : Interceptor
{
    public const string RequestIDMetadataKey = "x-request-id";
    public const string RequestIDLogKey = "request-id";

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        context = GenerateRequestID(context);
        return await continuation(request, context);
    }

    private static ServerCallContext GenerateRequestID(ServerCallContext context)
    {
        if (context.RequestHeaders.GetValue(RequestIDMetadataKey) is null)
        {
            string shortId = ShortID();
            context.ResponseTrailers.Add(RequestIDMetadataKey, shortId);
        }
        return context;
    }

    private static string ShortID()
    {
        byte[] buffer = new byte[6];
        using (var rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(buffer);
        }
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
        return context.RequestHeaders.GetValue(RequestIDMetadataKey) 
           ?? context.ResponseTrailers.GetValue(RequestIDMetadataKey) 
           ?? string.Empty;
    }
}

using System;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Core.Utils;

namespace AKSMiddleware;

public class MdForwardInterceptor : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var incomingMetadata = context.Options.Headers;

        var outgoingMetadata = new Metadata();
        if (incomingMetadata != null)
        {
            foreach (var entry in incomingMetadata)
            {
                outgoingMetadata.Add(entry);
            }
        }

        var newContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            context.Options.WithHeaders(outgoingMetadata)
        );

        return continuation(request, newContext);
    }
}
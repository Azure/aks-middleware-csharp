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
        // Extract incoming metadata
        var incomingMetadata = context.Options.Headers;

        // Create new metadata for the outgoing context
        var outgoingMetadata = new Metadata();
        if (incomingMetadata != null)
        {
            foreach (var entry in incomingMetadata)
            {
                outgoingMetadata.Add(entry);
            }
        }

        // Create new CallOptions with the outgoing metadata
        var newContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            context.Options.WithHeaders(outgoingMetadata)
        );

        // Proceed with the RPC
        return continuation(request, newContext);
    }

}
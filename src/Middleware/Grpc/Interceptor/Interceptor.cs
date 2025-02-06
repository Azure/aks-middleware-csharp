using Grpc.Core;
using Grpc.Core.Interceptors;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Serilog;
using Serilog.Events;
using Serilog.Context;
using Serilog.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AKSMiddleware;

public static class Constants
{
    public const string ProtocolKey = "protocol";
    public const string ProtocolValueGrpc = "grpc";
    public const string ComponentFieldKey = "component";
    public const string ComponentValueServer = "server";
    public const string ComponentValueClient = "client";
    public const string ServiceFieldKey = "service";
    public const string MethodFieldKey = "method";
    public const string MethodTypeFieldKey = "method_type";
    public const string RequestIDMetadataKey = "x-request-id";
    public const string RequestIDLogKey = "request-id";
    public const string StartTimeKey = "start_time";
    public const string TimeMsKey = "time_ms";
    public const string StatusCodeKey = "code";
    public const string PeerAddressKey = "peer_address";
}

public class InterceptorFactory
{
    public static Interceptor[] DefaultClientInterceptors(ILogger logger)
    {
        var clientLogger = logger.ForContext("source", "ApiRequestLog");
        var interceptors = new Interceptor[]
        {
            new RetryInterceptor(),
            new ClientApiRequestLogger(clientLogger)
        };

        return interceptors;
    }

    public static Interceptor[] DefaultServerInterceptors(ILogger logger)
    {
        // Use one enriched logger for API request logging and one for context logging
        var apiLogger = logger.ForContext("source", "ApiRequestLog");
        var ctxLogger = logger.ForContext("source", "CtxLog");

        var interceptors = new Interceptor[]
        {
            new ValidationInterceptor(apiLogger),
            new RequestIdInterceptor(),
            new CtxLoggerInterceptor(ctxLogger),
            new ServerApiRequestLogger(apiLogger)
        };

        return interceptors;
    }
}

using Grpc.Core;
using Grpc.Core.Interceptors;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using ProtoValidate;

namespace MiddlewareListInterceptors;

public class ClientInterceptorLogOptions
{
    public ILogger Logger { get; set; }
    public TextWriter APIOutput { get; set; }
    public List<KeyValuePair<string, object>> Attributes { get; set; }
}

public class ServerInterceptorLogOptions
{
    public ILogger Logger { get; set; }
    public TextWriter APIOutput { get; set; }
    public TextWriter CtxOutput { get; set; }
    public List<KeyValuePair<string, object>> APIAttributes { get; set; }
    public List<KeyValuePair<string, object>> CtxAttributes { get; set; }
}

public static class InterceptorLogOptionsFactory
{
    public static ClientInterceptorLogOptions GetClientInterceptorLogOptions(ILogger logger, List<KeyValuePair<string, object>> attrs)
    {
        return new ClientInterceptorLogOptions
        {
            Logger = logger,
            APIOutput = Console.Out,
            Attributes = attrs
        };
    }

    public static ServerInterceptorLogOptions GetServerInterceptorLogOptions(ILogger logger, List<KeyValuePair<string, object>> attrs)
    {
        return new ServerInterceptorLogOptions
        {
            Logger = logger,
            APIOutput = Console.Out,
            CtxOutput = Console.Out,
            APIAttributes = attrs,
            CtxAttributes = attrs
        };
    }
}

// Client-side interceptor for logging
public class ClientLoggerInterceptor : Interceptor
{
    private readonly Serilog.ILogger _logger;

    public ClientLoggerInterceptor(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        LogCall<TRequest, TResponse>(context.Method);

        // Add caller metadata to headers
        AddCallerMetadata(ref context);

        // Log the start of the call
        _logger.Information("Starting call. Type/Method: {Type} / {Method}",
            context.Method.Type, context.Method.Name);

        // Continue the call chain
        var response = continuation(request, context);

        // Handle response asynchronously
        Task.Run(() => HandleResponse(response));

        return response;
    }

    private async Task<TResponse> HandleResponse<TResponse>(AsyncUnaryCall<TResponse> call)
    {
        try
        {
            var response = await call.ResponseAsync;
            _logger.Information($"Response received: {response}");
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

    private void AddCallerMetadata<TRequest, TResponse>(ref ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        var headers = context.Options.Headers;

        // Create new headers if they don't exist
        if (headers == null)
        {
            headers = new Metadata();
            var options = context.Options.WithHeaders(headers);
            context = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options);
        }

        // Add caller metadata to headers
        headers.Add("caller-user", Environment.UserName);
        headers.Add("caller-machine", Environment.MachineName);
        headers.Add("caller-os", Environment.OSVersion.ToString());
    }

}

// Server-side interceptor for logging
public class ServerLoggerInterceptor : Interceptor
{
    private readonly Serilog.ILogger _logger;

    public ServerLoggerInterceptor(Serilog.ILogger logger)
    {
        _logger = logger.ForContext("SourceContext", "ServerLoggerInterceptor");
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {

        var sw = Stopwatch.StartNew();

        LogCall<TRequest, TResponse>(MethodType.Unary, context);

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
            sw.Stop();
            _logger.Information($"Call duration: {sw.ElapsedMilliseconds} ms");
        }
    }

    private void LogCall<TRequest, TResponse>(MethodType methodType, ServerCallContext context)
        where TRequest : class
        where TResponse : class
    {
        _logger.Warning($"Starting call. Type: {methodType}. Request: {typeof(TRequest)}. Response: {typeof(TResponse)}");
        WriteMetadata(context.RequestHeaders, "caller-user");
        WriteMetadata(context.RequestHeaders, "caller-machine");
        WriteMetadata(context.RequestHeaders, "caller-os");
        WriteMetadata(context.ResponseTrailers, "x-request-id");

        void WriteMetadata(Metadata headers, string key)
        {
            var headerValue = headers.GetValue(key) ?? "(unknown)";
            _logger.Warning($"{key}: {headerValue}");
        }
    }
}


public class InterceptorFactory
{
    private static ProtoValidate.Validator _validator;

    public InterceptorFactory(ProtoValidate.Validator validator)
    {
        _validator = validator;
    }

    public static ClientLoggerInterceptor[] DefaultClientInterceptors(ClientInterceptorLogOptions options)
    {

        var logger = options.Logger;

        var interceptors = new[]
        {
            new ClientLoggerInterceptor(logger),
            new ClientLoggerInterceptor(logger)
        };

        return interceptors;
    }

    public static Interceptor[] DefaultServerInterceptors(ServerInterceptorLogOptions options)
    {

        var logger = options.Logger;

        var interceptors = new Interceptor[]
        {
            new ValidationInterceptor(_validator),
            new ServerLoggerInterceptor(logger)
        };

        return interceptors;
    }
}

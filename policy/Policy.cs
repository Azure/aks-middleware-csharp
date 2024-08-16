using System;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.ResourceManager;
using Serilog;
using Grpc.Core;

namespace AKSMiddleware;

public class LoggingPolicy : HttpPipelinePolicy
{
    private readonly Serilog.ILogger _logger;

    public LoggingPolicy(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        DateTime startTime = DateTime.UtcNow;

        try
        {
            ProcessNext(message, pipeline);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception occurred during HTTP processing.");
            throw;
        }
        finally
        {
            Logging.LogRequest(new LogRequestParams(
                _logger,
                startTime,
                message.Request,
                message.Response,
                null
            ));
        }
    }

    public override async ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        DateTime startTime = DateTime.UtcNow;

        try
        {
            await ProcessNextAsync(message, pipeline).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception occurred during HTTP processing.");
            throw;
        }
        finally
        {
            // Log request details
            Logging.LogRequest(new LogRequestParams(
                _logger,
                startTime,
                message.Request,
                message.Response,
                null
            ));
        }
    }
}


public static class ArmPolicy
{
    public static ArmClientOptions GetDefaultArmClientOptions(Serilog.ILogger logger)
    {
        var clientOptions = new ArmClientOptions
        {
            Retry =
            {
                MaxRetries = 5
            }
        };

        clientOptions.AddPolicy(new LoggingPolicy(logger), HttpPipelinePosition.PerCall);

        return clientOptions;
    }

    // Based off of gRPC standard here: https://chromium.googlesource.com/external/github.com/grpc/grpc/+/refs/tags/v1.21.4-pre1/doc/statuscodes.md
    public static StatusCode ConvertHTTPStatusToGRPCError(int httpStatusCode)
    {
        return httpStatusCode switch
        {
            200 or 201 or 202 => StatusCode.OK,
            400 => StatusCode.InvalidArgument,
            504 => StatusCode.DeadlineExceeded,
            401 => StatusCode.Unauthenticated,
            403 => StatusCode.PermissionDenied,
            404 => StatusCode.NotFound,
            409 => StatusCode.Aborted,
            429 => StatusCode.ResourceExhausted,
            500 => StatusCode.Internal,
            501 => StatusCode.Unimplemented,
            503 => StatusCode.Unavailable,
            _ => StatusCode.Unknown,
        };
    }
}

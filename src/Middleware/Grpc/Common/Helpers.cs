using System;
using System.Linq;
using Serilog;

namespace AKSMiddleware;
public static class Helpers
{
    /// <summary>
    /// Extracts both the service and method names from a gRPC full method name.
    /// Expected format: "/package.Service/Method"
    /// </summary>
    public static (string ServiceName, string MethodName) ExtractServiceAndMethod(string fullMethodName)
    {
        var parts = fullMethodName.Split('/');
        if (parts.Length < 3)
        {
            throw new InvalidOperationException("Unexpected gRPC method format.");
        }

        string serviceName = parts[1].Split('.').Last();
        string methodName = parts[2];
        return (serviceName, methodName);
    }

    /// <summary>
    /// Adds the service, method, and protocol properties to the logger.
    /// Expects the full gRPC method name in the format "/package.Service/Method".
    /// </summary>
    public static ILogger WithServiceProperties(this ILogger logger, string fullMethodName)
    {
        var (serviceName, methodName) = ExtractServiceAndMethod(fullMethodName);
        return logger
                .ForContext(Constants.ServiceFieldKey, serviceName)
                .ForContext(Constants.MethodFieldKey, methodName)
                .ForContext(Constants.ProtocolKey, Constants.ProtocolValueGrpc);
    }
}

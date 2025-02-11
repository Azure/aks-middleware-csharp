using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;
using ServiceHub.LogProto;
using System.Runtime.CompilerServices;

namespace AKSMiddleware;

// Grpc log interceptor for server side operations
public class CtxLoggerInterceptor : Interceptor
{
    private readonly Serilog.ILogger _logger;

    public CtxLoggerInterceptor(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    // Best practices for gRPC interceptors in .NET:
    // https://learn.microsoft.com/en-us/aspnet/core/grpc/interceptors?view=aspnetcore-9.0
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        if (request is IMessage message)
        {
            var req = FilterLogs(message);
            var ctxDictionary = new Dictionary<string, object>
            {
                { Constants.MethodFieldKey, context.Method },
                { "request", req },
                // readding here since source field seems to get removed, even after adding in interceptor.cs
                { "source", "CtxLog" },
                { Constants.RequestIDLogKey, RequestIdInterceptor.GetRequestID(context) }
            };
            // Serialize the dictionary to a JSON string
            var jsonCtxDict = JsonConvert.SerializeObject(ctxDictionary, new JsonSerializerSettings{TypeNameHandling = TypeNameHandling.Auto});
            context.RequestHeaders.Add("ctxlog-data", jsonCtxDict);
        }

        try
        {
            return await continuation(request, context);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Error thrown by {context.Method}.");
            throw;
        }
    }

    public static Dictionary<string, object> FilterLogs(IMessage message)
    {
        var jsonFormatter = new JsonFormatter(new JsonFormatter.Settings(true));
        string jsonMessage = jsonFormatter.Format(message);

        JObject jObjectMessage = JObject.Parse(jsonMessage);
        Dictionary<string, object> fieldMap = JsonHelper.ConvertJObjectToDictionary(jObjectMessage);

        // Suppress the warning using the null-forgiving operator
        return FilterLoggableFields(fieldMap, message.Descriptor) ?? new Dictionary<string, object>();
    }

    public static Dictionary<string, object> FilterLoggableFields(
        Dictionary<string, object> fieldMap, 
        MessageDescriptor descriptor)
    {
        if (fieldMap == null || descriptor == null)
        {
            return fieldMap ?? new Dictionary<string, object>();
        }

        foreach (var fieldName in fieldMap.Keys.ToList())
        {
            var fieldDescriptor = descriptor.FindFieldByName(fieldName);
            if (fieldDescriptor == null)
            {
                continue;
            }

            var options = fieldDescriptor.GetOptions();
            if (options != null)
            {
                bool hasLoggable = options.HasExtension(LogExtensions.Loggable);
                if (hasLoggable && !options.GetExtension(LogExtensions.Loggable))
                {
                    fieldMap.Remove(fieldName);
                    continue;
                } 
            }

            if (fieldMap[fieldName] is Dictionary<string, object> subMap && 
                fieldDescriptor.FieldType == FieldType.Message)
            {
                var subMessageDescriptor = fieldDescriptor.MessageType;
                if (subMessageDescriptor != null)
                {
                    fieldMap[fieldName] = FilterLoggableFields(subMap, subMessageDescriptor);
                }
            }
        }
        return fieldMap;
    }
}

public static class JsonHelper
{
    public static Dictionary<string, object> ConvertJObjectToDictionary(JObject jObject)
    {
        var result = new Dictionary<string, object>();
        foreach (var property in jObject.Properties())
        {
            if (property.Value is JObject nestedJObject)
            {
                result[property.Name] = ConvertJObjectToDictionary(nestedJObject);
            }
            else if (property.Value is JArray nestedArray)
            {
                result[property.Name] = ConvertJArrayToList(nestedArray);
            }
            else
            {
                var value = property.Value?.ToObject<object>();
                if (value != null)
                {
                    result[property.Name] = value;
                }
            }
        }
        return result;
    }

    public static List<object> ConvertJArrayToList(JArray jArray)
    {
        var result = new List<object>();
        foreach (var item in jArray)
        {
            if (item != null)
            {
                result.Add(item.ToObject<object>()!);
            }
        }
        return result;
    }
}

// Using Caller Information Attributes
// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/attributes/caller-information
public static class LoggerExtensions
{
    public static ILogger WithCtx(this ILogger logger, ServerCallContext context,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0,
        [CallerMemberName] string callerMemberName = "")
    {
        // Extract JSON string from request headers
        var ctxLogJson = context.RequestHeaders.GetValue("ctxlog-data");

        // Check if json is null or empty
        if (string.IsNullOrEmpty(ctxLogJson))
        {
            return logger;
        }

        // Deserialize the JSON string back to a dictionary with type information
        var dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(ctxLogJson, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto
        });

        // Add dictionary properties to the log context
        if (dictionary != null)
        {
            foreach (var kvp in dictionary)
            {
                logger = logger.ForContext(kvp.Key, kvp.Value, destructureObjects: true);
            }
        }

        // Add caller information to the log context
        var location = new
        {
            function = callerMemberName,
            file = callerFilePath,
            line = callerLineNumber
        };

        return logger.ForContext("location", location, true);
    }
}
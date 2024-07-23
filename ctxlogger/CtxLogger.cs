using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog;
using System;
using Serilog.Context;
using System.Threading.Tasks;
using ServiceHub.FieldOptions;

namespace MiddlewareListInterceptors;

public class CtxLoggerInterceptor : Interceptor
{
    private readonly Serilog.ILogger _logger;

    public CtxLoggerInterceptor(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        // set source; can update with .PushProperty for entire service, or with .ForContext for limited scope
        LogContext.PushProperty("source", "CtxLog");

        // Note: creating a new ctxlogger to add things w/ ForContext (won't propagate to other interceptors)
        LogContext.PushProperty(Constants.MethodFieldKey, context.Method);
        
        if (request is IMessage message)
        {
            var req = FilterLogs(message);
            string reqJson = JsonConvert.SerializeObject(req);

            LogContext.PushProperty("request", req, destructureObjects: true);
            _logger.Information($"API handler logger output. req: {reqJson}");
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
        string json = jsonFormatter.Format(message);

        JObject jObject = JObject.Parse(json);
        Dictionary<string, object> fieldMap = JsonHelper.ConvertJObjectToDictionary(jObject);

        return FilterLoggableFields(fieldMap, message.Descriptor);
    }


    public static Dictionary<string, object> FilterLoggableFields(
        Dictionary<string, object> fieldMap, 
        MessageDescriptor descriptor)
    {
        if (fieldMap == null || descriptor == null)
        {
            return fieldMap;
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
                fieldMap[fieldName] = FilterLoggableFields(subMap, subMessageDescriptor);
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
                result[property.Name] = property.Value.ToObject<object>();
            }
        }
        return result;
    }

    public static List<object> ConvertJArrayToList(JArray jArray)
    {
        var result = new List<object>();
        foreach (var item in jArray)
        {
            if (item is JObject nestedJObject)
            {
                result.Add(ConvertJObjectToDictionary(nestedJObject));
            }
            else if (item is JArray nestedArray)
            {
                result.Add(ConvertJArrayToList(nestedArray));
            }
            else
            {
                result.Add(item.ToObject<object>());
            }
        }
        return result;
    }
}

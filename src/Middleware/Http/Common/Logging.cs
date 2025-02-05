using Azure.Core;
using Azure.ResourceManager;
using Serilog;
using System;
using System.Web;
using System.Collections.Generic;
using System.Net.Http;

namespace AKSMiddleware;

// Used for AzureSDK logging 
public class LogRequestParams
{
    public Serilog.ILogger Logger { get; set; }
    public DateTime StartTime { get; set; }
    public object Request { get; set; }
    public object Response { get; set; }
    public Exception? Error { get; set; }

    public LogRequestParams(Serilog.ILogger logger, DateTime startTime, object request, object response, Exception? error)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        StartTime = startTime;
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Response = response ?? throw new ArgumentNullException(nameof(response));
        Error = error;
    }
}

public static class Logging
    {
        public static void LogRequest(LogRequestParams parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            string method = string.Empty;
            string service = string.Empty;
            string requestUri = string.Empty;

            switch (parameters.Request)
            {
                case HttpRequestMessage httpRequest:
                    method = httpRequest.Method.Method;
                    service = httpRequest.RequestUri?.Host ?? "unknown";
                    requestUri = httpRequest.RequestUri?.ToString() ?? "unknown";
                    break;

                case Request azcoreRequest:
                    method = azcoreRequest.Method.Method;
                    service = azcoreRequest.Uri?.Host ?? "unknown";
                    requestUri = azcoreRequest.Uri?.ToString() ?? "unknown";
                    break;

                default:
                    return; // Unknown request type, do nothing
            }

            Uri parsedUri;
            try
            {
                parsedUri = new Uri(requestUri);
            }
            catch (UriFormatException ex)
            {
                parameters.Logger.ForContext("source", "ApiRequestLog")
                    .ForContext("protocol", "REST")
                    .ForContext("method_type", "unary")
                    .ForContext("code", "na")
                    .ForContext("component", "client")
                    .ForContext("time_ms", "na")
                    .ForContext("method", method)
                    .ForContext("service", service)
                    .ForContext("url", requestUri)
                    .ForContext("error", ex.Message)
                    .Error("Error parsing request URL");
                return;
            }

            string trimmedUri = TrimUrl(parsedUri);
            string methodInfo = GetMethodInfo(method, trimmedUri);
            double latency = (DateTime.UtcNow - parameters.StartTime).TotalMilliseconds;

            var logEntry = parameters.Logger.ForContext("source", "ApiRequestLog")
                .ForContext("protocol", "REST")
                .ForContext("method_type", "unary")
                .ForContext("component", "client")
                .ForContext("time_ms", latency)
                .ForContext("method", methodInfo)
                .ForContext("service", service)
                .ForContext("url", trimmedUri);

            if (parameters.Error != null || parameters.Response == null)
            {
                logEntry.ForContext("error", parameters.Error?.Message ?? "unknown error")
                        .ForContext("code", "na")
                        .Error("Finished call");
            }
            else
            {
                int statusCode;
                string reasonPhrase;

                if (parameters.Response is HttpResponseMessage httpResponse)
                {
                    statusCode = (int)httpResponse.StatusCode;
                    reasonPhrase = httpResponse.ReasonPhrase ?? "unknown";
                }
                else if (parameters.Response is Azure.Response azureResponse)
                {
                    statusCode = azureResponse.Status;
                    reasonPhrase = azureResponse.ReasonPhrase;
                }
                else
                {
                    statusCode = 0;
                    reasonPhrase = "unknown";
                }

                if (statusCode >= 200 && statusCode < 300)
                {
                    logEntry.ForContext("error", "na")
                            .ForContext("code", statusCode)
                            .Information("finished call");
                }
                else
                {
                    logEntry.ForContext("error", reasonPhrase ?? "Unknown error")
                            .ForContext("code", statusCode)
                            .Error("finished call");
                }
            }
        }

        private static string GetMethodInfo(string method, string url)
        {
            string resourceIdString = TrimToResourceId(url);

            try
            { 
                // Try to create a ResourceIdentifier with the trimmed and cleaned URL.
                var resourceId = new ResourceIdentifier(resourceIdString);
                string resourceType = resourceId.ResourceType.ToString();
                if (!string.IsNullOrEmpty(resourceType))
                {
                    if (method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                    {
                        bool isReadOperation = !string.IsNullOrEmpty(resourceId.Name);
                        resourceType += isReadOperation ? " - READ" : " - LIST";
                    }
                    return $"{method} {resourceType}";
                }
            }
            catch (Exception)
            {
                // If the URL is not a valid resource ID, log the URL as-is.
            }
        
            return $"{method} {url}";
            
        }

        /// <summary>
        /// Trims the URL to resource ID format.
        /// </summary>
        private static string TrimToResourceId(string url)
        {
            // If "/subscriptions/" exists, trim the URL starting from that segment.
            int subscriptionsIndex = url.IndexOf("/subscriptions/", StringComparison.OrdinalIgnoreCase);
            string trimmedUrl = subscriptionsIndex >= 0 ? url.Substring(subscriptionsIndex) : url;

            // Remove any query parameters.
            int queryIndex = trimmedUrl.IndexOf('?');
            if (queryIndex >= 0)
            {
                trimmedUrl = trimmedUrl.Substring(0, queryIndex);
            }

            return trimmedUrl;
        }

        /// <summary>
        /// Trims the trailing api-version so it doesn't pollute the URL when logging.
        /// </summary>
        private static string TrimUrl(Uri uri)
        {
            var queryParams = HttpUtility.ParseQueryString(uri.Query);
            var apiVersion = queryParams["api-version"];

            var baseUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";

            if (!string.IsNullOrEmpty(apiVersion))
            {
                return $"{baseUrl}?api-version={apiVersion}";
            }

            return baseUrl;
        }
    }
using Azure.Core;
using Microsoft.AspNetCore.Http;

namespace AKSMiddleware;

// Used for AzureSDK and Http Server logging 
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
    // TODO (tomabraham): See if we can use ResourceIdentifier to make this data driven for C# and Go middleware
    private static readonly Dictionary<string, bool> ResourceTypes = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
    {
        { "resourcegroups", true },
        { "storageaccounts", true },
        { "operationresults", true },
        { "asyncoperations", true },
        { "checknameavailability", true }
    };

    /// <summary>
    /// Logs the details of an HTTP request/response to the logger.
    /// 
    /// Expected URL format:
    /// - Must start with "https://management.azure.com/".
    /// - Must include the "/subscriptions/{subscriptionId}" segment where {subscriptionId} is a valid GUID.
    /// - For resource group level operations, the URL should contain "/resourceGroups/{resourceGroupName}".
    /// - For listing resources at a top-level collection, the URL ends with the resource type segment (no trailing resource name).
    /// - For read operations, the resource type segment is followed by the resource name.
    /// 
    /// If URL is malformed, the entire URL will be logged as-is.
    /// 
    /// Examples:
    /// - List resource groups:
    ///   https://management.azure.com/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups?api-version=version
    /// - Read a specific resource group:
    ///   https://management.azure.com/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg_name?api-version=version
    /// - Read a specific storage account:
    ///   https://management.azure.com/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg_name/providers/Microsoft.Storage/storageAccounts/account_name?api-version=version
    /// </summary>
    public static void LogRequest(LogRequestParams parameters)
    {
        if (parameters == null) throw new ArgumentNullException(nameof(parameters));

        string method = string.Empty;
        string methodInfo = string.Empty;
        string service = string.Empty;
        string requestUri = string.Empty;
        string component = string.Empty;

        switch (parameters.Request)
        {
            case HttpRequestMessage httpRequest:
                method = httpRequest.Method.Method;
                service = httpRequest.RequestUri?.Host ?? "unknown";
                requestUri = httpRequest.RequestUri?.ToString() ?? "unknown";
                component = "client";
                break;

            case Request azcoreRequest:
                method = azcoreRequest.Method.Method;
                service = azcoreRequest.Uri.Host?? "unknown";
                requestUri = azcoreRequest.Uri.ToString()?? "unknown";
                component = "client";
                break;
            
            case HttpRequest httpRequest:
                method = httpRequest.Method;
                service = httpRequest.Host.ToString();
                requestUri = $"{httpRequest.Scheme}://{service}{httpRequest.Path}{httpRequest.QueryString}";
                component = "server";
                methodInfo = GetMethodInfoForHttpRequest(method, requestUri);
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
                .ForContext("component", component)
                .ForContext("time_ms", "na")
                .ForContext("method", method)
                .ForContext("service", service)
                .ForContext("url", requestUri)
                .ForContext("error", ex.Message)
                .Error("Error parsing request URL");
            return;
        }

        string trimmedUri = TrimUrl(parsedUri);

        if (string.IsNullOrEmpty(methodInfo))
        {
            methodInfo = GetMethodInfo(method, trimmedUri);
        }
        double latency = (DateTime.UtcNow - parameters.StartTime).TotalMilliseconds;

        var logEntry = parameters.Logger.ForContext("source", "ApiRequestLog")
            .ForContext("protocol", "REST")
            .ForContext("method_type", "unary")
            .ForContext("component", component)
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
            string? reasonPhrase;

            if (parameters.Response is HttpResponseMessage httpResponse)
            {
                statusCode = (int)httpResponse.StatusCode;
                reasonPhrase = httpResponse.ReasonPhrase?? "unknown";
            }
            else if (parameters.Response is Azure.Response azureResponse)
            {
                statusCode = azureResponse.Status;
                reasonPhrase = azureResponse.ReasonPhrase;
            }
            else if (parameters.Response is HttpResponse restHttpResponse)
            {
                statusCode = restHttpResponse.StatusCode;
                reasonPhrase = null; // ReasonPhrase is not available in HttpResponse
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
        // TODO: Migrate to using ARM's parseResourceID function to get the resource type.
        var urlParts = url.Split(new[] { "?api-version" }, StringSplitOptions.None);
        
        if (urlParts.Length < 2 && !urlParts[0].Contains("v1"))
        {
            return $"{method} {url}";
        }

        var parts = urlParts[0].Split('/');
        var resource = urlParts[0];
        var counter = 0;

        for (counter = parts.Length - 1; counter >= 0; counter--)
        {
            var currToken = parts[counter].ToLowerInvariant();
            var index = currToken.IndexOfAny(new[] { '?', '/' });
            if (index != -1)
            {
                currToken = currToken.Substring(0, index);
            }
            if (ResourceTypes.ContainsKey(currToken))
            {
                resource = currToken;
                break;
            }
        }

        if (method == "GET")
        {
            if (counter == parts.Length - 1)
            {
                resource += " - LIST";
            }
            else
            {
                resource += " - READ";
            }
        }

        return $"{method} {resource}";
    }

    private static string TrimUrl(Uri uri)
    {
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var apiVersion = queryParams["api-version"];

        var baseUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";

        if (!string.IsNullOrEmpty(apiVersion))
        {
            return $"{baseUrl}?api-version={apiVersion}";
        }

        return baseUrl;
    }

    /// <summary>
    /// Extracts a normalized method name for logging from an HTTP method and URL path for UserRP services.
    /// <para>
    /// Logic:
    /// <list type="bullet">
    /// <item>If the URL ends with 'resourceXXX', extracts the resource name from the segment before the last and returns the singular resource name plus the XXX suffix (e.g., EmployeeReadValidate).</item>
    /// <item>If the URL ends with 'subscriptionLifeCycleNotification', returns the singular resource name plus 'SubscriptionLifeCycleNotification'.</item>
    /// <item>Otherwise, returns the singular resource name plus 'Item' (e.g., EmployeeItem), where the resource name is the segment before the last.</item>
    /// <item>The HTTP method is prepended to the result (e.g., DELETE EmployeeItem).</item>
    /// </list>
    /// </para>
    /// <para>
    /// Examples:
    /// <code>
    /// GetMethodInfoForHttpRequest("DELETE", "/.../Employees/{EmployeeName}") => "DELETE EmployeeItem"
    /// GetMethodInfoForHttpRequest("POST", "/.../Employees/{EmployeeName}/resourceReadValidate") => "POST EmployeeReadValidate"
    /// GetMethodInfoForHttpRequest("POST", "/.../Employees/subscriptionLifeCycleNotification") => "POST EmployeeSubscriptionLifeCycleNotification"
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="method">The HTTP method (e.g., GET, POST, DELETE).</param>
    /// <param name="url">The HTTP URL path.</param>
    /// <returns>A string in the format 'METHOD ResourceOperation'.</returns>
    internal static string GetMethodInfoForHttpRequest(string method, string url)
    {
        // Remove query string
        int queryIndex = url.IndexOf('?');
        string path = queryIndex >= 0 ? url.Substring(0, queryIndex) : url;
        // Split path into segments
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return method;
        }

        // Find the last segment
        string lastSegment = segments[^1];
        string resourceName = string.Empty;
        string methodName = string.Empty;

        // Helper to singularize resource name (naive: remove trailing 's' if present)
        string Singularize(string name) => name.EndsWith("s", StringComparison.OrdinalIgnoreCase) && name.Length > 1 ? name.Substring(0, name.Length - 1) : name;

        // If ends with subscriptionLifeCycleNotification
        if (lastSegment.Equals("subscriptionLifeCycleNotification", StringComparison.OrdinalIgnoreCase))
        {
            // Find the resource name before this segment
            if (segments.Length >= 2)
            {
                resourceName = segments[^2];
                resourceName = Singularize(resourceName);
                methodName = $"{resourceName}SubscriptionLifeCycleNotification";
            }
            else
            {
                methodName = "SubscriptionLifeCycleNotification";
            }
        }
        // If ends with resourceXXX (e.g., resourceReadValidate)
        else if (lastSegment.StartsWith("resource", StringComparison.OrdinalIgnoreCase) && lastSegment.Length > "resource".Length)
        {
            // Find the resource name before this segment
            if (segments.Length >= 3)
            {
                resourceName = segments[^3];
                resourceName = Singularize(resourceName);
                string suffix = lastSegment.Substring("resource".Length);
                // Capitalize first letter of suffix
                if (!string.IsNullOrEmpty(suffix))
                    suffix = char.ToUpperInvariant(suffix[0]) + suffix.Substring(1);
                methodName = $"{resourceName}{suffix}";
            }
            else
            {
                methodName = lastSegment;
            }
        }
        else
        {
            // Default: treat as resource item (use the segment before the last if available)
            if (segments.Length >= 2)
            {
                resourceName = segments[^2];
                resourceName = Singularize(resourceName);
            }
            methodName = $"{resourceName}Item";
        }
        return $"{method} {methodName}";
    }
}
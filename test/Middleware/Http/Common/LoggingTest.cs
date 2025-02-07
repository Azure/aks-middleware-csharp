using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;
using Xunit;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using AKSMiddleware;


public class LoggingTests
{
    private readonly StringWriter logOutput;
    private readonly ILogger logger;

    public LoggingTests()
    {
        var template = "{Timestamp} [{Level}] {Message} {CustomAttributes:lj}{Properties}{NewLine}{Exception}";
        logOutput = new StringWriter();
        logger = new LoggerConfiguration()
            .WriteTo.TextWriter(logOutput, LogEventLevel.Information, template)
            .CreateLogger();
    }

    // This test validates that GET requests with a nested resource are logged as a READ operation.
    [Fact]
    public void Get_WithNestedResourcelogsReadOperation()
    {
        var url = "https://management.azure.com/subscriptions/sub_id/resourceGroups/rg_name/providers/Microsoft.Storage/storageAccounts/account_name?api-version=version";
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var p = new LogRequestParams(logger, DateTime.UtcNow, request, new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.OK }, null!);
        Logging.LogRequest(p);

        var logString = logOutput.ToString();
        Assert.Contains("GET storageaccounts - READ", logString);
    }

    // This test validates that GET requests with a top-level resource are logged as a LIST operation.
    [Fact]
    public void Get_WithTopLevelResourcelogsListOperation()
    {
        var url = "https://management.azure.com/subscriptions/sub_id/resourceGroups?api-version=version";
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var p = new LogRequestParams(logger, DateTime.UtcNow, request, new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.OK }, null!);
        Logging.LogRequest(p);

        var logString = logOutput.ToString();
        Assert.Contains("GET resourcegroups - LIST", logString);
    }

    // // This test validates that non-GET requests do not include any operation type in the log.
    [Fact]
    public void NonGetMethod_NoOperationType()
    {
        var url = "https://management.azure.com/subscriptions/sub_id/resourceGroups/rg_name/providers/Microsoft.Storage/storageAccounts?api-version=version";
        var request = new HttpRequestMessage(HttpMethod.Post, url);

        var p = new LogRequestParams(logger, DateTime.UtcNow, request, new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.OK }, null!);
        Logging.LogRequest(p);

        var logString = logOutput.ToString();
        Assert.Contains("POST storageaccounts", logString);
        Assert.DoesNotContain("- LIST", logString);
        Assert.DoesNotContain("- READ", logString);
    }

    // This test validates that AzCore pipeline requests are logged without an operation type.
    [Fact]
    public void AzCoreRequest_NoOperationType()
    {
        var clientOptions = ArmPolicy.GetDefaultArmClientOptions(logger);
        var pipeline = HttpPipelineBuilder.Build(clientOptions);
        var req = pipeline.CreateRequest();
        req.Method = RequestMethod.Post;
        req.Uri.Reset(new Uri("https://management.azure.com/subscriptions/sub_id/resourceGroups/rg_name/providers/Microsoft.Storage/storageAccounts?api-version=version"));

        var p = new LogRequestParams(logger, DateTime.UtcNow, req, new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.OK }, null!);
        Logging.LogRequest(p);

        var logString = logOutput.ToString();
        Assert.Contains("POST storageaccounts", logString);
    }

    // This test validates that a malformed URL is logged in its entirety.
    [Fact]
    public void MalformedUrllogsEntireUrl()
    {
        var url = "https://management.azure.com/subscriptions/sub_id/resourceGroups/rg_name/providers/Microsoft.Storage/storageAccounts/account_name/api-version=version";
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var p = new LogRequestParams(logger, DateTime.UtcNow, request, new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.OK }, null!);
        Logging.LogRequest(p);

        var logString = logOutput.ToString();
        Assert.Contains("GET https://management.azure.com/subscriptions/sub_id/resourceGroups/rg_name/providers/Microsoft.Storage/storageAccounts/account_name/api-version=version", logString);
    }

    // This test validates that an unrecognized resource type is logged in its entirety.
    [Fact]
    public void ResourceTypeNotFoundlogsEntireUrl()
    {
        var url = "https://management.azure.com/subscriptions/sub_id/customResourceGroup/resource_name/providers/Microsoft.Storage/customResource/resource_name?api-version=version";
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var p = new LogRequestParams(logger, DateTime.UtcNow, request, new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.OK }, null!);
        Logging.LogRequest(p);

        var logString = logOutput.ToString();
        Assert.Contains("GET https://management.azure.com/subscriptions/sub_id/customResourceGroup/resource_name/providers/Microsoft.Storage/customResource/resource_name - READ", logString);
    }

    // This test validates that multiple query parameters are recognized and logged properly.
    [Fact]
    public void MultipleQueryParameterslogsProperly()
    {
        var url = "https://management.azure.com/subscriptions/sub_id/resourceGroups/rg_name/providers/Microsoft.Storage/storageAccounts?api-version=version&param1=value1&param2=value2";
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var p = new LogRequestParams(logger, DateTime.UtcNow, request, new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.OK }, null!);
        Logging.LogRequest(p);

        var logString = logOutput.ToString();
        Assert.Contains("GET storageaccounts - LIST", logString);
    }

    // This test validates that when no query parameters are present, the entire URL is logged.
    [Fact]
    public void ZeroQueryParameterslogsEntireUrl()
    {
        var url = "https://management.azure.com/subscriptions/sub_id/resourceGroups/rg_name/providers/Microsoft.Storage/storageAccounts";
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var p = new LogRequestParams(logger, DateTime.UtcNow, request, new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.OK }, null!);
        Logging.LogRequest(p);

        var logString = logOutput.ToString();
        Assert.Contains("GET https://management.azure.com/subscriptions/sub_id/resourceGroups/rg_name/providers/Microsoft.Storage/storageAccounts", logString);
    }
}
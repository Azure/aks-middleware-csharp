using AKSMiddleware;
using Microsoft.AspNetCore.Http;
using Moq;
using Serilog;
using Serilog.Events;

public class RestServerApiRequestLoggerTests
{
    private readonly StringWriter _logOutput;
    private readonly ILogger _logger;
    private readonly Mock<RequestDelegate> _requestDelegateMock;

    private readonly RestServerApiRequestLogger _restServerApiRequestLogger;

    public RestServerApiRequestLoggerTests()
    {
        var template = "{Timestamp} [{Level}] {Message} {CustomAttributes:lj}{Properties}{NewLine}{Exception}";
        _logOutput = new StringWriter();
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.TextWriter(_logOutput, LogEventLevel.Information, template)
            .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        _requestDelegateMock = new Mock<RequestDelegate>();
        _restServerApiRequestLogger = new RestServerApiRequestLogger(_requestDelegateMock.Object, _logger);
        _requestDelegateMock.Setup(next => next(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnSuccessfulLogs()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethod.Post.Method;
        context.Request.Path = new PathString("/subscriptions/sub_id/resourceGroups/rg_name/providers/Microsoft.ServicehubUserRp/Databases/database/resourcePatchCompleted?api-version=version");
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("my.userrp.com");
        context.Response.StatusCode = StatusCodes.Status200OK;

        await _restServerApiRequestLogger.InvokeAsync(context);
        var logString = _logOutput.ToString();
        Assert.Contains("code: 200", logString);
        Assert.Contains("POST - DatabasePatchCompleted", logString);
        Assert.Contains("component: \"server\"", logString);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnErrorLogs()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethod.Post.Method;
        context.Request.Path = new PathString("/subscriptions/sub_id/resourceGroups/rg_name/providers/Microsoft.ServicehubUserRp/Databases/database/resourceCreationValidate?api-version=version");
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("my.userrp.com");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        await _restServerApiRequestLogger.InvokeAsync(context);
        var logString = _logOutput.ToString();
        Assert.Contains("code: 500", logString);
        Assert.Contains("POST - DatabaseCreationValidate", logString);
        Assert.Contains("component: \"server\"", logString);
    }
}
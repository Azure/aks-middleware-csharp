using System.Net;
using AKSMiddleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

public class RestServerApiRequestLoggerIntegrationTests
{
    private readonly StringWriter _logOutput;
    private readonly ILogger _logger;

    public RestServerApiRequestLoggerIntegrationTests()
    {
        var template = "{Timestamp} [{Level}] {Message} {CustomAttributes:lj}{Properties}{NewLine}{Exception}";
        _logOutput = new StringWriter();
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.TextWriter(_logOutput, LogEventLevel.Information, template)
            .CreateLogger();
    }

    [Fact]
    public async Task RestServerApiRequestLogger_ShouldReturnSuccessfulLogs()
    {
        // Arrange
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(_logger);
            })
            .Configure(app =>
            {
                app.UseMiddleware<RestServerApiRequestLogger>();
                app.Run(async context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    await context.Response.WriteAsync("Hello, World!");
                });
            });

        var testServer = new TestServer(builder);
        var client = testServer.CreateClient();

        // Act
        var response = await client.GetAsync("/test");

        // Assert
        var logString = _logOutput.ToString();
        Assert.Contains("[Information] finished call", logString);
        Assert.Contains("code: 200", logString);
        Assert.Contains("source: \"ApiRequestLog\"", logString);
        Assert.Contains("component: \"server\"", logString);
    }

    [Fact]
    public async Task RestServerApiRequestLogger_ShouldReturnErrorLogs()
    {
        // Arrange
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(_logger);
            })
            .Configure(app =>
            {
                app.UseMiddleware<RestServerApiRequestLogger>();
                app.Run(context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    return Task.CompletedTask;
                });
            });

        var testServer = new TestServer(builder);
        var client = testServer.CreateClient();

        // Act
        var response = await client.GetAsync("/error");

        // Assert
        var logString = _logOutput.ToString();
        Assert.Contains("[Error] finished call", logString);
        Assert.Contains("code: 500", logString);
        Assert.Contains("source: \"ApiRequestLog\"", logString);
        Assert.Contains("component: \"server\"", logString);
    }
}
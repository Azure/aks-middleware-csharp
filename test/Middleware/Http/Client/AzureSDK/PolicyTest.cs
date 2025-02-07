using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.ResourceManager;
using Moq;
using Serilog;
using Serilog.Core;
using Serilog.Expressions;
using Serilog.Templates;
using Serilog.Formatting;
using Serilog.Formatting.Compact;
using Serilog.Events;
using AKSMiddleware;
using Xunit;

public class LoggingPolicyTests
{
    private const string RequestPath = "/api/test";
    private const string RequestHost = "localhost";
    private const string RequestScheme = "https";

    private (ILogger Logger, StringWriter LogOutput) CreateLoggerAndOutput()
    {
        var template = "{Timestamp} [{Level}] {Message} {CustomAttributes:lj}{Properties}{NewLine}{Exception}";
        var logOutput = new StringWriter();
        var logger = new LoggerConfiguration()
            .WriteTo.TextWriter(logOutput, LogEventLevel.Information, template)
            .CreateLogger();
        return (logger, logOutput);
    }

    private Mock<Response> CreateMockResponse()
    {
        var mockResponse = new Mock<Response>();
        mockResponse.Setup(r => r.Status).Returns(200);
        mockResponse.Setup(r => r.ReasonPhrase).Returns("OK");
        mockResponse.Setup(r => r.ContentStream).Returns(new MemoryStream());
        return mockResponse;
    }

    private Mock<Request> CreateMockRequest()
    {
        var mockRequest = new Mock<Request>();
        mockRequest.Setup(r => r.Uri).Returns(new RequestUriBuilder { Scheme = RequestScheme, Host = RequestHost, Path = RequestPath });
        mockRequest.Setup(r => r.Method).Returns(RequestMethod.Get);
        return mockRequest;
    }

    private HttpPipeline BuildPipeline(ILogger logger, Mock<HttpPipelineTransport> transport)
    {
        var clientOptions = ArmPolicy.GetDefaultArmClientOptions(logger);
        clientOptions.Transport = transport.Object;
        return HttpPipelineBuilder.Build(clientOptions);
    }

    // Test the asynchrnous Process method
    [Fact]
    public async Task Logs_Request_And_Response_On_Successful_Request()
    {
        var (logger, logOutput) = CreateLoggerAndOutput();
        var mockResponse = CreateMockResponse();

        var mockTransport = new Mock<HttpPipelineTransport>();
        mockTransport
            .Setup(t => t.ProcessAsync(It.IsAny<HttpMessage>()))
            .Callback<HttpMessage>(message =>
            {
                // Simulate a 200 OK response.
                message.Response = mockResponse.Object;
            })
            .Returns(new ValueTask(Task.CompletedTask));

        var mockRequest = CreateMockRequest();
        mockTransport
            .Setup(t => t.CreateRequest())
            .Returns(mockRequest.Object);

        var pipeline = BuildPipeline(logger, mockTransport);

        // Create and validate a request.
        var request = pipeline.CreateRequest();
        Assert.NotNull(request);
        Assert.Equal(RequestScheme, request.Uri.Scheme);
        Assert.Equal(RequestHost, request.Uri.Host);
        Assert.Equal(RequestPath, request.Uri.Path);
        Assert.Equal(RequestMethod.Get, request.Method);

        // Create a message using the request.
        var message = new HttpMessage(request, new ResponseClassifier());

        // Act.
        await pipeline.SendAsync(message, CancellationToken.None);

        Assert.Equal(200, message.Response.Status);

        var logs = logOutput.ToString();
        Assert.Contains("finished call", logs);
        Assert.Contains("code: 200", logs);
        Assert.Contains($"method: \"GET {RequestScheme}://{RequestHost}{RequestPath}\"", logs);
        Assert.Contains("time_ms", logs);

        // Verify the mock transport was called.
        mockTransport.Verify(t => t.ProcessAsync(It.IsAny<HttpMessage>()), Times.Once());
    }

    // Test the synchronous Process method
    [Fact]
    public void Logs_Request_And_Response_On_Successful_Sync_Process()
    {
        var (logger, logOutput) = CreateLoggerAndOutput();
        var mockResponse = CreateMockResponse();

        var mockTransport = new Mock<HttpPipelineTransport>();
        mockTransport
            .Setup(t => t.Process(It.IsAny<HttpMessage>()))
            .Callback<HttpMessage>(message =>
            {
                // Simulate a 200 OK response.
                message.Response = mockResponse.Object;
            });

        var mockRequest = CreateMockRequest();
        mockTransport
            .Setup(t => t.CreateRequest())
            .Returns(mockRequest.Object);

        var pipeline = BuildPipeline(logger, mockTransport);

        // Create and validate a request.
        var request = pipeline.CreateRequest();
        Assert.NotNull(request);
        Assert.Equal(RequestScheme, request.Uri.Scheme);
        Assert.Equal(RequestHost, request.Uri.Host);
        Assert.Equal(RequestPath, request.Uri.Path);
        Assert.Equal(RequestMethod.Get, request.Method);

        // Create a message using the request.
        var message = new HttpMessage(request, new ResponseClassifier());

        // Act: Send synchronously.
        pipeline.Send(message, CancellationToken.None);

        Assert.Equal(200, message.Response.Status);

        var logs = logOutput.ToString();
        Assert.Contains("finished call", logs);
        Assert.Contains("code: 200", logs);
        Assert.Contains($"method: \"GET {RequestScheme}://{RequestHost}{RequestPath}\"", logs);
        Assert.Contains("time_ms", logs);

        // Verify that the sync transport Process method was invoked once.
        mockTransport.Verify(t => t.Process(It.IsAny<HttpMessage>()), Times.Once());
    }
}
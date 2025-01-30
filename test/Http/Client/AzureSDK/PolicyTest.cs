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
    [Fact]
    public async Task Logs_Request_And_Response_On_Successful_Request()
    {
        var template = "{Timestamp} [{Level}] {Message} {CustomAttributes:lj}{Properties}{NewLine}{Exception}";

        var logOutput = new StringWriter();
        var logger = new LoggerConfiguration()
            .WriteTo.TextWriter(logOutput, LogEventLevel.Information, template) 
            .CreateLogger();

        // Mock transport to simulate HTTP response
        var mockTransport = new Mock<HttpPipelineTransport>();

        var mockResponse = new Mock<Response>();
        mockResponse.Setup(r => r.Status).Returns(200);
        mockResponse.Setup(r => r.ReasonPhrase).Returns("OK");
        mockResponse.Setup(r => r.ContentStream).Returns(new MemoryStream());

        mockTransport
            .Setup(t => t.ProcessAsync(It.IsAny<HttpMessage>()))
            .Callback<HttpMessage>((message) =>
            {
                // Simulate a 200 OK response
                message.Response = mockResponse.Object;
            })
            .Returns(new ValueTask(Task.CompletedTask));

        // Mock the CreateRequest method to return a valid Request object
        var mockRequest = new Mock<Request>();
        mockRequest.Setup(r => r.Uri).Returns(new RequestUriBuilder { Scheme = "https", Host = "localhost", Path = "/api/test" });
        mockRequest.Setup(r => r.Method).Returns(RequestMethod.Get);

        mockTransport
            .Setup(t => t.CreateRequest())
            .Returns(mockRequest.Object);

        // Arrange pipeline options with mocked transport
        var clientOptions = ArmPolicy.GetDefaultArmClientOptions(logger);
        clientOptions.Transport = mockTransport.Object;

        var pipeline = HttpPipelineBuilder.Build(clientOptions);

        // Create a request
        var request = pipeline.CreateRequest();
        Assert.NotNull(request);

        // Additional assertions to ensure the request is what we want
        Assert.Equal("https", request.Uri.Scheme);
        Assert.Equal("localhost", request.Uri.Host);
        Assert.Equal("/api/test", request.Uri.Path);
        Assert.Equal(RequestMethod.Get, request.Method);

        // Create a message using the request
        var message = new HttpMessage(request, new ResponseClassifier());

        // Act
        await pipeline.SendAsync(message, CancellationToken.None);

        Assert.Equal(200, message.Response.Status);

        // Assert logs contain expected entries
        var logs = logOutput.ToString();
        Assert.Contains("finished call", logs); 
        Assert.Contains("code: 200", logs);
        Assert.Contains("method: \"GET https://localhost/api/test\"", logs);
        Assert.Contains("time_ms", logs);

        // Verify the mock transport was called
        mockTransport.Verify(
            t => t.ProcessAsync(It.IsAny<HttpMessage>()),
            Times.Once()
        );
    }
}
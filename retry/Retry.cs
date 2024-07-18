using Grpc.Core;
using Grpc.Core.Interceptors;
using Polly;
using Polly.Retry;
using System;
using System.Threading.Tasks;

namespace MiddlewareListInterceptors;

public class RetryInterceptor : Interceptor
{
    private readonly AsyncRetryPolicy _retryPolicy;

    public RetryInterceptor()
    {
        _retryPolicy = Policy
            .Handle<RpcException>(ex =>
                ex.Status.StatusCode == StatusCode.Aborted ||
                ex.Status.StatusCode == StatusCode.Unavailable)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * 100));
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
    TRequest request,
    ClientInterceptorContext<TRequest, TResponse> context,
    AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        return _retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                // Call the continuation function to proceed with the call
                var call = continuation(request, context);

                // Await the result of the call
                var response = await call.ResponseAsync;
                var responseHeaders = await call.ResponseHeadersAsync;
                var status = call.GetStatus();
                var trailers = call.GetTrailers();
                var dispose = call.Dispose;

                // Return a new AsyncUnaryCall with the awaited results
                return new AsyncUnaryCall<TResponse>(
                    Task.FromResult(response),
                    Task.FromResult(responseHeaders),
                    () => status,
                    () => trailers,
                    dispose);
            }
            catch (RpcException ex)
            {
                Console.WriteLine($"RpcException occurred: {ex.Status}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");
                throw;
            }
        }).Result; // Use .Result to unwrap the Task and return the AsyncUnaryCall object
    }
}

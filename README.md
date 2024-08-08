# Overview
 
This directory is the root of aks-middleware-csharp. It implements interceptors to aid common service tasks. 

In `Interceptor.cs`, the `InterceptorFactory` class provides two functions, `DefaultClientInterceptors` and `DefaultServerInterceptors`, which return lists of interceptors to be registered by a client/server.

# Usage

To build the project, run ``dotnet build``. To create a NuGet package that can be published or used by other projects, run ``dotnet pack``. Be sure to update the version each successive publish.

## Requestid

ServerInterceptor. Adds `x-request-id` to the `ServerCallContext` if it does not already exist. This interceptor needs to be registered first so `GetRequestId()` can be used by `CtxLogger` and `ApiRequestLogger`. 

## MdForward

ClientInterceptor. Propagates MD from incoming to outgoing. Only need to be used in servers. No need to be used in a pure client app.

## ApiRequestLogger

ServerInterceptor. Provides similar functionality to the go-grpc-middleware logging package (for the server). Adds fields to the `apiLogger` such as service, method, requestId. Tracks the start time and duration of the call, and logs "finished call" when the asynchronous call returns.

## ClientLogger

ClientInterceptor. Provides similar functionality to the go-grpc-middleware logging package (for the client).  Adds fields to the `apiLogger` such as service, method, requestId. Tracks the start time and duration of the call, and logs "finished call" when the asynchronous call returns.

## CtxLogger

ServerInterceptor. Filters loggable fields from gRPC messages (see Log Filtering), creates a dictionary with important information (request ID, method name, source, request), and adds it to the `ServerCallContext`. The `LoggerExtensions` class extends a Serilog logger instance to include context information in logs, enabling users to log with detailed caller information.

### Log filtering

This is a feature that allows the user to annotate within `api.proto` what request payload variables should be logged or not. Logic in the `CtxLoggerInterceptor` decides what to log based off the annotation. The "loggable" annotation is true by default, so the user is only required to annotate the variables that should not be logged.

Ancestors need to be loggable in order to examine the annotations on the leaf nodes. For example, if loggable value for "address" is false, it will not look for the loggable values of "city", "state", "zipcode", etc.

The source code for the external loggable field option is located in this repository, within the `LogProto` subdirectory. This code is published as a NuGet package at TODO: LINK. Whenever the code is updated, it needs to be packed and published to Nuget.org, and all uses of this package need to import the most recent version.

## Retry

ClientInterceptor. Resends a request based on the gRPC code returned from the server. All options for the interceptor (i.e. max retries, codes to retry on, type of backoff) are defined in the `_retryPolicy` in `RetryInterceptor`.

## Protovalidate

ServerInterceptor. Validates incoming client requests using the ProtoValidate package. Utilizes the `FileDescriptor` from the request's protobuf message to validate the message against the rules defined in `api.proto`.
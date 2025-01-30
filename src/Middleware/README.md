<!-- markdownlint-disable MD004 -->

# AKS middleware for C#

<!-- vscode-markdown-toc -->
* 1. [Usage](#Usage)
* 2. [gRPC server](#gRPCserver)
 	* 2.1. [requestid](#requestid)
 	* 2.2. [ctxlogger (applogger)](#ctxloggerapplogger)
 	* 2.3. [ApiRequestLogger (api request/response logger)](#autologgerapirequestresponselogger)
 	* 2.4. [protovalidate](#protovalidate)
* 3. [gRPC client](#gRPCclient)
 	* 3.1. [mdforward](#mdforward)
 	* 3.2. [ClientLogger (api request/response logger)](#clientloggerapirequestresponselogger-1)
 	* 3.3. [retry](#retry)

* 4. [HTTP client via Azure SDK](#HTTPclientviaAzureSDK)
 	* 4.1. [mdforward](#mdforward-1)
 	* 4.2. [policy (api request/response logger)](#policyapirequestresponselogger)
 	* 4.3. [retry](#retry-1)
* 5. [HTTP client via Direct HTTP request](#HTTPclientviaDirectHTTPrequest)
 	* 5.1. [mdforward](#mdforward-1)
 	* 5.2. [Restlogger (api request/response logger)](#Restloggerapirequestresponselogger)
 	* 5.3. [retry](#retry-1)
* 6. [Project](#Project)
 	* 6.1. [Contributing](#Contributing)
 	* 6.2. [Trademarks](#Trademarks)

<!-- vscode-markdown-toc-config
	numbering=true
	autoSave=true
	/vscode-markdown-toc-config -->
<!-- /vscode-markdown-toc -->

This directory is the root of the aks-middleware module. It implements interceptors to aid common service tasks. See the list below for details.

## 1. <a id='Usage'></a>Usage

To build the project, run ```dotnet build```. To create a NuGet package that can be published or used by other projects, run ```dotnet pack```. Be sure to update the version each successive publish.

## 2. <a id='gRPCserver'></a>gRPC server

The following gRPC server interceptors are used by default. Some interceptors are implemented in this repo. Some are implemented in existing open source projects and are used by this repo.

### 2.1. <a id='requestid'></a>requestid

Adds x-request-id to the ServerCallContext if it does not already exist. This interceptor needs to be registered first so GetRequestId() can be used by CtxLogger and ApiRequestLogger.


### 2.2. <a id='ctxloggerapplogger'></a>ctxlogger (applogger)

Filters loggable fields from gRPC messages (see Log Filtering), creates a dictionary with important information (request ID, method name, source, request), and adds it to the ServerCallContext. 

The LoggerExtensions class extends a Serilog logger instance to include context information in logs, enabling users to log with detailed caller information.



##### <a id='logfiltering'></a>log filtering

This is a feature that allows the user to annotate within api.proto what request payload variables should be logged or not. Logic in the CtxLoggerInterceptor decides what to log based off the annotation. The "loggable" annotation is true by default, so the user is only required to annotate the variables that should not be logged.


Ancestors need to be loggable in order to examine the annotations on the leaf nodes. For example, if loggable value for "address" is false, it will not look for the loggable values of "city", "state", "zipcode", etc.


The source code for the external loggable field option is located in this repository, within the LogProto subdirectory. This code is published as a NuGet package at TODO: LINK. Whenever the code is updated, it needs to be packed and published to Nuget.org, and all uses of this package need to import the most recent version.

### 2.3. <a id='autologgerapirequestresponselogger'></a>autologger (api request/response logger)

Provides similar functionality to the go-grpc-middleware logging package (for the server). Adds fields to the apiLogger such as service, method, requestId. Tracks the start time and duration of the call, and logs "finished call" when the asynchronous call returns.

### 2.4. <a id='protovalidate'></a>protovalidate

Validates incoming client requests using the ProtoValidate package. Utilizes the FileDescriptor from the request's protobuf message to validate the message against the rules defined in api.proto.

## 3. <a id='gRPCclient'></a>gRPC client

The following gRPC client interceptors are used by default.

### 3.1. <a id='mdforward'></a>mdforward

Propagates MD from incoming to outgoing. Only need to be used in servers. No need to be used in a pure client app.


### 3.2. <a id='clientloggerapirequestresponselogger-1'></a>ClientLogger (api request/response logger)

Provides similar functionality to the go-grpc-middleware logging package (for the client). Adds fields to the apiLogger such as service, method, requestId. Tracks the start time and duration of the call, and logs "finished call" when the asynchronous call returns.


### 3.3. <a id='retry'></a>retry

Resends a request based on the gRPC code returned from the server. All options for the interceptor (i.e. max retries, codes to retry on, type of backoff) are defined in the _retryPolicy in RetryInterceptor.

## 4. <a id='HTTPclientviaAzureSDK'></a>HTTP client via Azure SDK

### 4.1. <a id='mdforward-1'></a>mdforward

Strictly speaking, it is not metadata forwarding. But it serves the same purpose: instead of propagating the id from the incoming context to the outgoing context, the id is propagated from incoming context to Azure HTTP request header.

If we choose to not implement it, we can let Azure SDK to decide the request id. The mapping between the Azure request id and the opertion/correlation id will be logged by the policy middleware below.

### 4.2. <a id='policyapirequestresponselogger'></a>policy (api request/response logger)

The `policy` package provides a logging policy for HTTP requests made via the Azure SDK for Go.

The logging policy logs details about each HTTP request and response, including the request method, URL, status code, and duration.

##### <a id='Usage-1'></a>Usage

To use the logging policy, you need to create a logger and then apply the policy to your HTTP client.

Code example is included in the test code

### 4.3. <a id='retry-1'></a>retry

By default, we set the ARMClientOptions MaxRetries variable to 5.
## 5. <a id='HTTPclientviaDirectHTTPrequest'></a>HTTP client via Direct HTTP request

### 5.1. <a id='mdforward-1'></a>mdforward

Missing.

### 5.2. <a id='Restloggerapirequestresponselogger'></a>Restlogger (api request/response logger)

missing

### 5.3. <a id='retry-1'></a>retry

Missing.

## 6. <a id='Project'></a>Project

> This repo has been populated by an initial template to help get you started. Please
> make sure to update the content to build a great experience for community-building.

As the maintainer of this project, please make a few updates:

- Improving this README.MD file to provide a great experience
- Updating SUPPORT.MD with content about this project's support experience
- Understanding the security reporting process in SECURITY.MD
- Remove this section from the README

### 6.1. <a id='Contributing'></a>Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit <https://cla.opensource.microsoft.com>.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

### 6.2. <a id='Trademarks'></a>Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft
trademarks or logos is subject to and must follow
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.

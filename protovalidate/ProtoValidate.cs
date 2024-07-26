using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Core.Interceptors;
using ProtoValidate;
using System;
using Serilog;
using System.Threading.Tasks;

namespace MiddlewareListInterceptors
{
    public class ValidationInterceptor : Interceptor
    {
        private readonly bool _failFast;
        private readonly ILogger _logger;

        public ValidationInterceptor(ILogger logger, bool failFast = false)
        {
            _logger = logger;
            _failFast = failFast;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                // _logger.Information("Starting validation for request of type {RequestType}", typeof(TRequest).Name);

                if (!ValidateMsg(request, out var error))
                {
                    var status = new Status(StatusCode.InvalidArgument, error);
                    _logger.Warning("Validation failed for request of type {RequestType}: {Error}", typeof(TRequest).Name, error);
                    throw new RpcException(status);
                }

                // _logger.Information("Validation successful for request of type {RequestType}", typeof(TRequest).Name);
                return await continuation(request, context);
            }
            catch (RpcException ex)
            {
                _logger.Error(ex, "Validation error for request of type {RequestType}: {ErrorDetail}", typeof(TRequest).Name, ex.Status.Detail);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error during validation for request of type {RequestType}", typeof(TRequest).Name);
                throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
            }
        }

        public bool ValidateMsg<TRequest>(TRequest request, out string error)
        {
            if (request is not IMessage message)
            {
                throw new ArgumentException("Unsupported message type");
            }
            
            var descriptor = message.Descriptor.File;

            var validatorOptions = new ProtoValidate.ValidatorOptions()
            {
                PreLoadDescriptors = false,
                DisableLazy = false,
                FileDescriptors = new List<FileDescriptor>{descriptor}
            };

            var validator = new ProtoValidate.Validator(validatorOptions);

            var violations = validator.Validate(message, _failFast);
            if (violations.Violations.Count > 0)
            {
                error = string.Join(", ", violations.Violations);
                return false;
            }
            error = string.Empty; // Assign an empty string instead of null
            return true;
        }
    }
}

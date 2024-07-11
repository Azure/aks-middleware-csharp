using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Interceptors;
using ProtoValidate;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MiddlewareListInterceptors;

public class ValidationInterceptor : Interceptor
{
    private readonly ProtoValidate.Validator _validator;
    private readonly bool _failFast;

    public ValidationInterceptor(ProtoValidate.Validator validator, bool failFast = false)
    {
        _validator = validator;
        _failFast = failFast;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {

        if (!ValidateMsg(request, out var error))
        {
            var status = new Status(StatusCode.InvalidArgument, error);
            throw new RpcException(status);
        }

        return await continuation(request, context);
    }

    public bool ValidateMsg<TRequest>(TRequest request, out string error)
    {
        if (request is not IMessage message)
        {
            throw new ArgumentException("Unsupported message type");
        }
        var violations = _validator.Validate(message, _failFast);
        if (violations.Violations.Count > 0)
        {
            error = string.Join(", ", violations.Violations);
            return false;
        }
        error = null;
        return true;
    }
}
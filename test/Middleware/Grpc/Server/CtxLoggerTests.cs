using System;
using System.Collections.Generic;
using AKSMiddleware;
// TODO (tomabraham): Create a publicly accessible test api instead of using MyGreeterCsharp.Api.V1
using MyGreeterCsharp.Api.V1;
using ServiceHub.LogProto;

using Google.Protobuf;

namespace Server.Tests
{
    public class CtxLoggerTests
    {
        [Fact]
        public void FilterLogs_WithPopulatedNameAndAddress_ShouldReturnFilteredFields()
        {
            var request = new HelloRequest
            {
                Name = "TestName",
                Age = 53,
                Email = "test@test.com",
                Address = new Address
                {
                    Street = "123 Main St",
                    City = "Seattle",
                    State = "WA",
                    Zipcode = 98101
                }
            };

            var result = CtxLoggerInterceptor.FilterLogs(request as IMessage) as Dictionary<string, object>;
            Assert.NotNull(result);
            Assert.Equal("TestName", result["name"]);

            var addressDict = result["address"] as Dictionary<string, object>;
            Assert.NotNull(addressDict);
            Assert.Equal("Seattle", addressDict["city"]);
            Assert.Equal((long)98101, addressDict["zipcode"]);
            Assert.False(addressDict.ContainsKey("street"));
            Assert.False(addressDict.ContainsKey("state"));
        }

        [Fact]
        public void FilterLogs_WithNameAndEmptyAddress_ShouldReturnOnlyNameAndEmptyAddress()
        {
            var request = new HelloRequest
            {
                Name = "TestName",
                Age = 53,
                Email = "test@test.com",
                Address = new Address() // Empty address
            };

            var result = CtxLoggerInterceptor.FilterLogs(request as IMessage) as Dictionary<string, object>;
            Assert.NotNull(result);
            Assert.Equal("TestName", result["name"]);

            var addressDict = result["address"] as Dictionary<string, object>;
            Assert.NotNull(addressDict);
            Assert.Equal("", addressDict["city"]);
            Assert.Equal(0L, addressDict["zipcode"]); // Compare as long
        }
    }
}
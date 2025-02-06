# Overview

This project defines the "loggable" field in `proto/log.proto`, allowing users to annotate a proto file to specify which payload variables should be logged or not.

## servicehub.logproto.props
- `Protobuf_ServiceHubImportsPath` specifies the path at which log.proto will be found within this package (`/native/include/`)

## servicehub.logproto.targets
- Adds an `<AdditionalImportDirs>` property to Protobuf compilation with the `Protobuf_ServiceHubImportsPath` filepath
- Ensures that `log.proto` gets included in protobuf compilation in `api/v1`
- Some additional context: https://blog.markvincze.com/include-multi-file-protobuf-package-in-dotnet/#:~:text=Luckily%2C%20there%20is%20a%20simple%20solution%2C%20we%20can,we%20do%20this%2C%20the%20build%20will%20work%20correctly


## LogProto.csproj
- Includes the .props and .targets files
- Ensures that `log.proto` will be found at the proper path using `PackagePath`

## Publishing/Usage

To publish a new version of this package, you just need to run 

```dotnet pack```

Once the .nupkg file is created you need to run

```dotnet nuget push /path/to/<file>.nupkg --api-key <key> --source https://api.nuget.org/v3/index.json```

More information available here: https://learn.microsoft.com/en-us/nuget/quickstart/create-and-publish-a-package-using-the-dotnet-cli


To use this log.proto file in a protofile within your service, you need to add the package to your .csproj file using 

```dotnet add package ServiceHub.LogProto```

From there, you can simply import it like so in your proto file:

```import "proto/log.proto";```
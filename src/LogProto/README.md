# Overview

This project defines the "loggable" field in `proto/log.proto`, allowing users to annotate a proto file to specify which payload variables should be logged or not.

## servicehub.logproto.props
- `Protobuf_ServiceHubImportsPath` specifies the path at which log.proto will be found within this package (`/native/include/`)

## servicehub.logproto.targets
- Adds an `<AdditionalImportDirs>` property to Protobuf compilation with the `Protobuf_ServiceHubImportsPath` filepath
- Ensures that `log.proto` gets included in protobuf compilation in `api/v1`


## LogProto.csproj
- Includes the .props and .targets files
- Ensures that `log.proto` will be found at the proper path using `PackagePath`

# Chondrite.Store

Shared storage interfaces and canonical binary serialization for Chondrite.

This package is in early development. Its public API may change between releases.

## Development

Requires the .NET 10 SDK.

```sh
dotnet build Chondrite.Store.slnx
dotnet test --solution Chondrite.Store.slnx
dotnet pack src/Chondrite.Store/Chondrite.Store.csproj --configuration Release
```


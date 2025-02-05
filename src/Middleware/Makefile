build:
	dotnet build

pack:
	# Be sure to update the version in the .csproj file before running this
	dotnet pack --configuration Release

push:
	dotnet nuget push bin/Release/*.nupkg --source https://api.nuget.org/v3/index.json --api-key $(API_KEY)

clean:
	dotnet clean

# Update version:
```
./ThingsDB/ThingsDB.csproj
```

# List outdated packages:
```
dotnet list package --outdate
```

# Update
In each project, `./ThingsDB` and `./Tests` update the packages. For example:
```
dotnet add package MessagePack
```

# Build
```
dotnet build
```

# Test
```
dotnet test
```

# Pack
```
dotnet pack
```

# Push
```
dotnet nuget push \
    ./ThingsDB/bin/Release/ThingsDB.1.0.3.nupkg \
    --api-key <API-KEY> \
    --source https://api.nuget.org/v3/index.json
```
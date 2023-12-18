
pushd CoreHost
dotnet publish -c release -p:PublishProfile=Properties\PublishProfiles\linux64
popd
pushd CachalotMonitor
dotnet publish -c release -p:PublishProfile=Properties\PublishProfiles\linux64
popd

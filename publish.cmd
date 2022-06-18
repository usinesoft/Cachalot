pushd Cachalot
dotnet publish -c release -o ..\bin\Release\net6.0\
popd
pushd CoreHost
dotnet publish -c release -o ..\bin\Release\net6.0\
popd
pushd WindowsService
dotnet publish -c release -o ..\bin\Release\net6.0\
popd
pushd AdminConsole
dotnet publish -c release -f net6.0 -o ..\bin\Release\net6.0\
popd
pushd StorageAnalyzer
dotnet publish -c release -f net6.0 -o ..\bin\Release\net6.0\
popd
pushd DemoClients\AccountsCore
dotnet publish -c release -f net6.0 -o ..\..\bin\Release\DemoClients\net6.0\
popd
pushd DemoClients\BookingMarketplaceCore
dotnet publish -c release -f net6.0 -o ..\..\bin\Release\DemoClients\net6.0\
popd

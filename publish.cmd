pushd CoreHost
dotnet publish -c release -o ..\bin\Release\DotnetCoreServer\netcoreapp2.1\
popd
pushd AdminConsole
dotnet publish -c release -f netcoreapp2.1 -o ..\bin\Release\AdminConsole\netcoreapp2.1\
popd
pushd DemoClients\AccountsCore
dotnet publish -c release -f netcoreapp2.1 -o ..\..\bin\Release\DemoClients\Accounts\netcoreapp2.1\
popd
pushd DemoClients\BookingMarketplaceCore
dotnet publish -c release -f netcoreapp2.1 -o ..\..\bin\Release\DemoClients\BookingMarketplace\netcoreapp2.1\
popd
pushd Cachalot
nuget pack Cachalot.csproj -IncludeReferencedProjects -Prop Configuration=Release -Symbols
move *.nupkg ..\bin\Release
popd

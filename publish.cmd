pushd CoreHost
dotnet publish -c release -o ..\bin\Release\Server\netcoreapp3.1\
popd
pushd WindowsService
dotnet publish -c release -o ..\bin\Release\WindowsService\netcoreapp3.1\
popd
pushd AdminConsole
dotnet publish -c release -f netcoreapp3.1 -o ..\bin\Release\AdminConsole\netcoreapp3.1\
popd
pushd DemoClients\AccountsCore
dotnet publish -c release -f netcoreapp3.1 -o ..\..\bin\Release\DemoClients\Accounts\netcoreapp3.1\
popd
pushd DemoClients\BookingMarketplaceCore
dotnet publish -c release -f netcoreapp3.1 -o ..\..\bin\Release\DemoClients\BookingMarketplace\netcoreapp3.1\
popd
pushd Cachalot
nuget pack Cachalot.csproj -IncludeReferencedProjects -Prop Configuration=Release -Symbols
move *.nupkg ..\bin\Release
popd

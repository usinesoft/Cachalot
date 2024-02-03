
###############################################################
# Build pipeline for Cachalot-DB
###############################################################
$releasePath="d:\release"
$version="2.5.8"

# clean the output directory
Remove-Item -Force -Recurse -Path "$releasePath\*"

# get current master version from git
git clone https://github.com/usinesoft/Cachalot.git "$releasePath\repo"

###############################################################
# generate framework-dependent binaries

dotnet publish "$releasePath\repo\Cachalot\Cachalot.csproj" -c release
dotnet pack "$releasePath\repo\Cachalot\Cachalot.csproj" -c release -o "$releasePath\portable\nuget"

dotnet nuget add source "$releasePath\portable\nuget" > $null

dotnet publish "$releasePath\repo\DemoClients\BookingMarketplaceCore\BookingMarketplace.csproj" -c release --packages "$releasePath\portable\nuget" -o "$releasePath\portable\demo"
dotnet publish "$releasePath\repo\DemoClients\AccountsCore\Accounts.csproj" -c release --packages "$releasePath\portable\nuget" -o "$releasePath\portable\demo"


dotnet publish "$releasePath\repo\CoreHost\CoreHost.csproj" -c release -o "$releasePath\portable\server"
dotnet publish "$releasePath\repo\CsvImport\CsvImport.csproj" -c release -o "$releasePath\portable\server"
dotnet publish "$releasePath\repo\CachalotMonitor\CachalotMonitor.csproj" -c release -o "$releasePath\portable\monitor"

New-Item -ItemType Directory -Force -Path "$releasePath\portable\doc"
Copy-Item "$releasePath\repo\doc\*.pdf" "$releasePath\portable\doc"

Compress-Archive "$releasePath\portable\*" "$releasePath\v${version}_portable.zip"



###############################################################
# generate Windows stand-alone binaries

dotnet publish "$releasePath\repo\CoreHost\CoreHost.csproj" -c release -r win-x64 --self-contained -o "$releasePath\win_x64\server"
dotnet publish "$releasePath\repo\CoreHost\CoreHost8.csproj" -c release -r win-x64 --self-contained -o "$releasePath\win_x64\server_aot"
dotnet publish "$releasePath\repo\CsvImport\CsvImport.csproj" -c release  -r win-x64 --self-contained -o "$releasePath\win_x64\server"
dotnet publish "$releasePath\repo\WindowsService\WindowsService.csproj" -c release  -r win-x64 --self-contained -o "$releasePath\win_x64\server"
dotnet publish "$releasePath\repo\CachalotMonitor\CachalotMonitor.csproj" -c release -r win-x64 --self-contained -o "$releasePath\win_x64\monitor"

New-Item -ItemType Directory -Force -Path "$releasePath\win_x64\doc"
Copy-Item "$releasePath\repo\doc\*.pdf" "$releasePath\win_x64\doc"

Compress-Archive "$releasePath\win_x64\*" "$releasePath\v${version}_winx64.zip"



###############################################################
# generate Linux stand-alone binaries

dotnet publish "$releasePath\repo\CoreHost\CoreHost.csproj" -c release -r linux-x64 --self-contained -o "$releasePath\linux_x64\server"
dotnet publish "$releasePath\repo\CsvImport\CsvImport.csproj" -c release  -r linux-x64 --self-contained -o "$releasePath\linux_x64\server"
dotnet publish "$releasePath\repo\CachalotMonitor\CachalotMonitor.csproj" -c release -r linux-x64 --self-contained -o "$releasePath\linux_x64\monitor"

New-Item -ItemType Directory -Force -Path "$releasePath\linux_x64\doc"
Copy-Item "$releasePath\repo\doc\*.pdf" "$releasePath\linux_x64\doc"

Compress-Archive "$releasePath\linux_x64\*" "$releasePath\v${version}_linux64.zip"


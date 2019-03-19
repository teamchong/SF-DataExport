((Get-Content -Path SF-DataExport.csproj -Raw) -Replace '<PackAsTool>true</PackAsTool>','') | Set-Content -Path SF-DataExport.self-contained.csproj
dotnet build -c Release -r win10-x64 SF-DataExport.self-contained.csproj
dotnet publish -c Release -r win10-x64 --self-contained true --no-build SF-DataExport.self-contained.csproj
dotnet build -c Release -r osx.10.11-x64 SF-DataExport.self-contained.csproj
dotnet publish -c Release -r osx.10.11-x64 --self-contained true --no-build SF-DataExport.self-contained.csproj
dotnet build -c Release -r ubuntu.16.04-x64 SF-DataExport.self-contained.csproj
dotnet publish -c Release -r ubuntu.16.04-x64 --self-contained true --no-build SF-DataExport.self-contained.csproj
dotnet build -c Release -r linux-x64 SF-DataExport.self-contained.csproj
dotnet publish -c Release -r linux-x64 --self-contained true --no-build SF-DataExport.self-contained.csproj
Remove-Item -Path SF-DataExport.self-contained.csproj
New-Item -ItemType Directory -Force -Path Self-Contained| Out-Null
Compress-Archive -Path bin\Release\netcoreapp2.1\win10-x64 -DestinationPath Self-Contained\win10-x64.zip
Compress-Archive -Path bin\Release\netcoreapp2.1\osx.10.11-x64 -DestinationPath Self-Contained\osx.10.11-x64.zip
Compress-Archive -Path bin\Release\netcoreapp2.1\ubuntu.16.04-x64 -DestinationPath Self-Contained\ubuntu.16.04-x64.zip
Compress-Archive -Path bin\Release\netcoreapp2.1\linux-x64 -DestinationPath Self-Contained\linux-x64.zip
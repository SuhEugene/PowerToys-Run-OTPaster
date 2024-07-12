$ErrorActionPreference = "Stop"

$projectDirectory = "$PSScriptRoot\Community.PowerToys.Run.Plugin.OTPaster"
[xml]$xml = Get-Content -Path "$projectDirectory\Community.PowerToys.Run.Plugin.OTPaster.csproj"

$platforms = "ARM64", "x64"

foreach ($platform in $platforms)
{
    if (Test-Path -Path "$projectDirectory\bin\$platform")
    {
        Remove-Item -Path "$projectDirectory\bin\$platform" -Recurse
    }
    if (Test-Path -Path "$projectDirectory\obj\$platform")
    {
        Remove-Item -Path "$projectDirectory\obj\$platform" -Recurse
    }
}

dotnet restore

foreach ($platform in $platforms)
{
    dotnet build --no-restore $projectDirectory.sln -c Release /p:Platform=$platform /p:EnableWindowsTargeting=true

    $releaseDirectory = "$projectDirectory\bin\$platform\Release"
    Remove-Item -Path "$projectDirectory\bin\$platform" -Recurse -Include *.xml, *.pdb, PowerToys.*, Wox.*
    New-Item -ItemType Directory -Force -Path $releaseDirectory
    Rename-Item -Path $releaseDirectory -NewName "OTPaster"

    Compress-Archive -Update -Path "$projectDirectory\bin\$platform\OTPaster" -DestinationPath "$PSScriptRoot\OTPaster-$platform.zip"
}

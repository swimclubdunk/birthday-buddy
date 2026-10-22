# Builds dist\BirthdayBuddy.exe: one self-contained file that runs on any Windows 10/11 x64 PC,
# with or without .NET installed.
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet test BirthdayBuddy.slnx -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

    if (Test-Path dist) { Remove-Item dist -Recurse -Force }
    dotnet publish src/BirthdayBuddy/BirthdayBuddy.csproj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o dist
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

    $exe = Get-Item dist/BirthdayBuddy.exe
    'Built {0} ({1:N1} MB)' -f $exe.FullName, ($exe.Length / 1MB)
}
finally {
    Pop-Location
}

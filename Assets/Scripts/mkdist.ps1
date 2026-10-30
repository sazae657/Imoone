param (
    [switch]$KeepPdb = $false
)
$ErrorActionPreference = 'Stop'
$route=Get-Item -Path "${PSScriptRoot}\..\.."
$binaries=[System.IO.Path]::Combine($route, "Binaries\ImooneWPF")
if (Test-Path $binaries){
    Remove-Item -Recurse $binaries
}

$proj = [System.IO.Path]::Combine($route, "ImooneWPF\ImooneWPF.csproj")
dotnet publish $proj -r win-x64 -p:PublishProfile=FolderProfile -c Release
if ($LASTEXITCODE -ne 0) {
   throw "dotnetが [${LASTEXITCODE}] で終了"
}
if (!${KeepPdb}) {
    Get-ChildItem -Path $binaries -Filter "*.pdb" -Recurse | ForEach-Object {
        Remove-Item -Path $_
    }
}

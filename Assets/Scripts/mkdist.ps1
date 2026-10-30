param (
    [switch]$KeepPdb = $false,
    [switch]$Archive = $false
)
$ErrorActionPreference = 'Stop'

$tag=(git describe --tags --abbrev=0)
$route=Get-Item -Path "${PSScriptRoot}\..\.."
$workDir = Join-Path ${route} "Binaries"
$binaries=[System.IO.Path]::Combine($workDir, "ImooneWPF-${tag}")
if (Test-Path $binaries){
    Remove-Item -Recurse $binaries
}

$proj = [System.IO.Path]::Combine($route, "ImooneWPF\ImooneWPF.csproj")
dotnet publish $proj -r win-x64 -p:PublishProfile=FolderProfile -c Release -o ${binaries}
if ($LASTEXITCODE -ne 0) {
   throw "dotnetが [${LASTEXITCODE}] で終了"
}
if (!${KeepPdb}) {
    Get-ChildItem -Path $binaries -Filter "*.pdb" -Recurse | ForEach-Object {
        Remove-Item -Path $_
    }
}
if (!${Archive}){
    exit 0
}
Compress-Archive -Path ${binaries} -DestinationPath (Join-Path ${workDir} "ImooneWPF-${tag}.zip") -CompressionLevel Optimal
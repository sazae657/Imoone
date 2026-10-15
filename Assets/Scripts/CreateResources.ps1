$ErrorActionPreference = 'Stop'
$ProzektRoot=(Get-Item "${PSScriptRoot}\..\..").FullName

function Generate {
    param (
        [string]$Bsv2Resx,
        [string]$Partition,
        [string]$Container = ${ProzektRoot},
        [string]$Target
    )
    $bsv="${ProzektRoot}\Assets\Strings\${Partition}"
    .$Bsv2Resx resx `
        --bsv "${bsv}\${Target}.bsv" `
        --resx "${Container}\${Target}\Properties\${Partition}.resx"
    if ($LASTEXITCODE -ne 0) {
        throw "${exePath} [${LASTEXITCODE}] で終了しました。"
    }
}
function GenerateEmbedContent {
        param (
        [string]$Bsv2Resx
    )
    .$Bsv2Resx mdconv `
        --md "${ProzektRoot}\THIRD-PARTY-LICENSES.md" `
        --insertion "${ProzektRoot}\LICENSE" `
        --res "${ProzektRoot}\ImogeIndex\Properties\Resources\Licenses.xml"
    if ($LASTEXITCODE -ne 0) {
        throw "${Bsv2Resx} [${LASTEXITCODE}] で終了しました。"
    }
}

$proj="${ProzektRoot}\Assets\Editor\Bsv2Resx\Bsv2Resx.csproj"
$dllPath =(dotnet msbuild -getProperty:TargetPath -p:Platform=x64 $proj)
$exePath = [System.IO.Path]::ChangeExtension($dllPath, ".exe")
Write-Output $exePath
#dotnet build -v quiet -p:Platform=x64  ${proj}

GenerateEmbedContent -Bsv2Resx $exePath
Get-ChildItem "${ProzektRoot}\Assets\Strings" -Directory | ForEach-Object {
    $par = $_.Name
    Generate -Bsv2Resx $exePath -Partition $par -Target ImogeIndex
    Generate -Bsv2Resx $exePath -Partition $par -Target ImogeIndex.Web3
    Generate -Bsv2Resx $exePath -Partition $par -Target ImogeIndex.Web6
    Generate -Bsv2Resx $exePath -Partition $par -Target ImogeIndexWPF
    Generate -Bsv2Resx $exePath -Partition $par -Target ImooneWPF
    Generate -Bsv2Resx $exePath -Partition $par -Target ImogeMerge -Container "${ProzektRoot}\Assets\Editor"
}

param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '0.1.1'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot 'publish\win-x64'))
$packageName = "GaugeTrail-Desktop-v$Version-win-x64"
$stagingDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot $packageName))
$zipPath = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot "$packageName.zip"))
$checksumPath = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot 'SHA256SUMS.txt'))
$releaseNotesPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "docs\RELEASE_NOTES_v$Version.md"))

function Assert-ArtifactChild {
    param([Parameter(Mandatory)][string]$Path)

    $prefix = $artifactsRoot.TrimEnd('\') + '\'
    if (-not $Path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "拒绝操作 artifacts 目录之外的路径：$Path"
    }
}

Assert-ArtifactChild -Path $publishDirectory
Assert-ArtifactChild -Path $stagingDirectory
Assert-ArtifactChild -Path $zipPath
Assert-ArtifactChild -Path $checksumPath

if (-not (Test-Path -LiteralPath $releaseNotesPath -PathType Leaf)) {
    throw "缺少版本说明文件：$releaseNotesPath"
}

New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null

foreach ($path in @($publishDirectory, $stagingDirectory)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

if (Test-Path -LiteralPath $checksumPath) {
    Remove-Item -LiteralPath $checksumPath -Force
}

dotnet publish (Join-Path $repositoryRoot 'src\GaugeTrail.Desktop\GaugeTrail.Desktop.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败，退出码：$LASTEXITCODE"
}

$executable = Join-Path $publishDirectory 'GaugeTrail.Desktop.exe'
if (-not (Test-Path -LiteralPath $executable)) {
    throw "发布目录缺少 GaugeTrail.Desktop.exe"
}

New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
Copy-Item -LiteralPath $executable -Destination $stagingDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.md') -Destination $stagingDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $stagingDirectory

$stagingDocs = Join-Path $stagingDirectory 'docs'
$stagingExamples = Join-Path $stagingDirectory 'examples'
New-Item -ItemType Directory -Path $stagingDocs, $stagingExamples -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\QUICK_START.md') -Destination $stagingDocs
Copy-Item -LiteralPath $releaseNotesPath -Destination $stagingDocs
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'examples\sample-line.csv') -Destination $stagingExamples

$exeHash = (Get-FileHash -LiteralPath (Join-Path $stagingDirectory 'GaugeTrail.Desktop.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest = [ordered]@{
    product = 'GaugeTrail Desktop'
    version = $Version
    author = 'KBT096'
    license = 'MIT'
    platform = 'win-x64'
    executable = 'GaugeTrail.Desktop.exe'
    executableSha256 = $exeHash
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stagingDirectory 'release-manifest.json') -Encoding utf8NoBOM

Compress-Archive -LiteralPath $stagingDirectory -DestinationPath $zipPath -CompressionLevel Optimal
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$zipHash  $packageName.zip`r`n" | Set-Content -LiteralPath $checksumPath -Encoding ascii -NoNewline

[pscustomobject]@{
    Version = $Version
    Executable = $executable
    ExecutableSha256 = $exeHash
    Zip = $zipPath
    ZipSha256 = $zipHash
    ZipBytes = (Get-Item -LiteralPath $zipPath).Length
    Checksums = $checksumPath
}

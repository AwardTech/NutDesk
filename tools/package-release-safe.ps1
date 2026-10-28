# Safely package the current NutDesk Release build into a portable ZIP.
# By default this script does NOT rebuild the solution. Build and test first.
# Run from any directory: .\tools\package-release-safe.ps1

[CmdletBinding()]
param(
    # Explicitly rebuild the solution before packaging. Omit to package the
    # existing Release output that has already been built and tested.
    [switch]$Build
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ProjectVersion {
    param([Parameter(Mandatory)][string]$PropsPath)

    [xml]$props = Get-Content -LiteralPath $PropsPath -Raw
    $versionNode = $props.SelectSingleNode("//Version")
    if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
        throw "Could not find a non-empty <Version> in '$PropsPath'."
    }

    $version = $versionNode.InnerText.Trim()
    if ($version -notmatch '^\d+(\.\d+){1,3}([.-][A-Za-z0-9.-]+)?$') {
        throw "Version '$version' contains unexpected characters; refusing to use it in paths."
    }
    return $version
}

function Get-Sha256 {
    param([Parameter(Mandatory)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$propsPath = Join-Path $repoRoot 'Directory.Build.props'
$solutionPath = Join-Path $repoRoot 'NutDesk.sln'
$distPath = Join-Path $repoRoot 'dist'
$releasePath = Join-Path $repoRoot 'src\NutDesk\bin\Release'

if (-not (Test-Path -LiteralPath $propsPath -PathType Leaf)) {
    throw "Version file not found: $propsPath"
}
$version = Get-ProjectVersion -PropsPath $propsPath
$packageName = "NutDesk-$version"
$zipName = "$packageName-portable.zip"

if ($Build) {
    if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
        throw "Solution not found: $solutionPath"
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
        throw "vswhere.exe not found. Build the solution in Visual Studio, or run this script without -Build to package an existing Release build."
    }

    $msbuildDir = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($msbuildDir) -or -not (Test-Path -LiteralPath $msbuildDir -PathType Leaf)) {
        throw 'Could not locate MSBuild. Build the solution in Visual Studio, or run without -Build.'
    }

    Write-Host "Building $solutionPath ..."
    & $msbuildDir $solutionPath -restore -p:Configuration=Release -p:UseSharedCompilation=false -m:1 -nr:false -nologo -verbosity:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed with exit code $LASTEXITCODE. Existing package has not been changed."
    }
}

foreach ($required in @('NutDesk.exe', 'NutDesk.Common.dll', 'NutDesk.exe.config')) {
    if (-not (Test-Path -LiteralPath (Join-Path $releasePath $required) -PathType Leaf)) {
        throw "Required Release output is missing: $(Join-Path $releasePath $required). Build the Release configuration first."
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'LICENSE') -PathType Leaf)) {
    throw "Repository LICENSE file not found: $(Join-Path $repoRoot 'LICENSE')"
}

New-Item -ItemType Directory -Path $distPath -Force | Out-Null
$runId = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), ([guid]::NewGuid().ToString('N').Substring(0, 8))
$stagePath = Join-Path $distPath "$packageName-stage-$runId"
$checkPath = Join-Path $distPath "$packageName-check-$runId"
$tempZip = Join-Path $distPath "$zipName.$runId.tmp"
$tempHash = "$tempZip.sha256"
$finalZip = Join-Path $distPath $zipName
$finalHash = "$finalZip.sha256"
$backupZip = "$finalZip.$runId.bak"
$backupHash = "$finalHash.$runId.bak"
$zipExisted = Test-Path -LiteralPath $finalZip -PathType Leaf
$hashExisted = Test-Path -LiteralPath $finalHash -PathType Leaf
$promoting = $false

try {
    New-Item -ItemType Directory -Path $stagePath | Out-Null

    # Copy top-level runtime files, excluding debug symbols and XML docs.
    Get-ChildItem -LiteralPath $releasePath -File | Where-Object {
        $_.Extension -notin @('.pdb', '.xml')
    } | Copy-Item -Destination $stagePath

    # Preserve culture/resource folders and their contents.
    Get-ChildItem -LiteralPath $releasePath -Directory | ForEach-Object {
        $sourceDirectory = $_.FullName
        $targetDirectory = Join-Path $stagePath $_.Name
        New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
        Get-ChildItem -LiteralPath $sourceDirectory -Recurse -File | Where-Object {
            $_.Extension -notin @('.pdb', '.xml')
        } | ForEach-Object {
            $relative = $_.FullName.Substring($sourceDirectory.Length).TrimStart([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
            $target = Join-Path $targetDirectory $relative
            $targetParent = Split-Path -Parent $target
            New-Item -ItemType Directory -Path $targetParent -Force | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $target
        }
    }

    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $stagePath 'LICENSE.txt')

    # Create a temporary ZIP with a stable root folder and forward-slash paths.
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::Open($tempZip, [IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -LiteralPath $stagePath -Recurse -File | ForEach-Object {
            $relative = $_.FullName.Substring($stagePath.Length + 1).Replace('\', '/')
            $entryName = "$packageName/$relative"
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $_.FullName, $entryName, [IO.Compression.CompressionLevel]::Optimal
            )
        }
    }
    finally {
        $archive.Dispose()
    }

    # Verify archive root, required files, exclusions, and staged-file count.
    $archive = [IO.Compression.ZipFile]::OpenRead($tempZip)
    try {
        $entries = @($archive.Entries)
        if ($entries.Count -eq 0) { throw 'The temporary ZIP is empty.' }
        $badEntries = @($entries | Where-Object { -not $_.FullName.StartsWith("$packageName/") })
        if ($badEntries.Count -gt 0) { throw "Archive contains entries outside '$packageName/'." }
        $badExtensions = @($entries | Where-Object { [IO.Path]::GetExtension($_.FullName) -in @('.pdb', '.xml') })
        if ($badExtensions.Count -gt 0) { throw 'Archive unexpectedly contains .pdb or .xml files.' }

        $entryNames = @($entries | ForEach-Object { $_.FullName })
        foreach ($required in @("$packageName/NutDesk.exe", "$packageName/NutDesk.Common.dll", "$packageName/NutDesk.exe.config", "$packageName/LICENSE.txt")) {
            if ($entryNames -notcontains $required) { throw "Required archive entry missing: $required" }
        }

        $cultureDirectories = @(Get-ChildItem -LiteralPath $stagePath -Directory)
        foreach ($culture in $cultureDirectories) {
            $prefix = "$packageName/$($culture.Name)/"
            if (@($entryNames | Where-Object { $_.StartsWith($prefix) }).Count -eq 0) {
                throw "Culture/resource directory '$($culture.Name)' has no files in the archive."
            }
        }

        $stagedFileCount = @(Get-ChildItem -LiteralPath $stagePath -Recurse -File).Count
        if ($entries.Count -ne $stagedFileCount) {
            throw "Archive entry count ($($entries.Count)) does not match staged file count ($stagedFileCount)."
        }
    }
    finally {
        $archive.Dispose()
    }

    # Extract and compare every packaged file against staging, then verify key
    # binaries against the current Release output.
    [IO.Compression.ZipFile]::ExtractToDirectory($tempZip, $checkPath)
    $extractedRoot = Join-Path $checkPath $packageName
    $stageFiles = @(Get-ChildItem -LiteralPath $stagePath -Recurse -File)
    foreach ($file in $stageFiles) {
        $relative = $file.FullName.Substring($stagePath.Length + 1)
        $extractedFile = Join-Path $extractedRoot $relative
        if (-not (Test-Path -LiteralPath $extractedFile -PathType Leaf)) {
            throw "Extracted package is missing '$relative'."
        }
        if ((Get-Sha256 $file.FullName) -ne (Get-Sha256 $extractedFile)) {
            throw "Extracted file differs from staging: $relative"
        }
    }
    foreach ($binary in @('NutDesk.exe', 'NutDesk.Common.dll')) {
        if ((Get-Sha256 (Join-Path $releasePath $binary)) -ne (Get-Sha256 (Join-Path $extractedRoot $binary))) {
            throw "Packaged binary does not match the Release output: $binary"
        }
    }

    $zipHash = Get-Sha256 $tempZip
    [IO.File]::WriteAllText($tempHash, "$zipHash  $zipName`n", [Text.Encoding]::ASCII)
    $sidecarHash = ((Get-Content -LiteralPath $tempHash -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
    if ($sidecarHash -ne $zipHash) { throw 'Temporary ZIP checksum verification failed.' }

    # Preserve existing artifacts before promotion. Never delete the old ZIP
    # until the replacement has been fully created and validated.
    if ($zipExisted) { Copy-Item -LiteralPath $finalZip -Destination $backupZip }
    if ($hashExisted) { Copy-Item -LiteralPath $finalHash -Destination $backupHash }

    $promoting = $true
    Move-Item -LiteralPath $tempZip -Destination $finalZip -Force
    Move-Item -LiteralPath $tempHash -Destination $finalHash -Force

    $finalActualHash = Get-Sha256 $finalZip
    $finalExpectedHash = ((Get-Content -LiteralPath $finalHash -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
    if ($finalActualHash -ne $finalExpectedHash) {
        throw 'Final ZIP checksum does not match its checksum file.'
    }
    $promoting = $false

    Write-Host ''
    Write-Host 'Package created and verified successfully.' -ForegroundColor Green
    Write-Host "Version:  $version"
    Write-Host "ZIP:      $finalZip"
    Write-Host "Size:     $((Get-Item -LiteralPath $finalZip).Length) bytes"
    Write-Host "SHA-256:  $finalActualHash"
    if ($zipExisted) { Write-Host "ZIP backup: $backupZip" }
    if ($hashExisted) { Write-Host "Checksum backup: $backupHash" }
    Write-Host "Files packaged: $stagedFileCount"
}
catch {
    if ($promoting) {
        # Restore previous artifacts if present; remove newly-created artifacts
        # if no previous version existed.
        if ($zipExisted -and (Test-Path -LiteralPath $backupZip -PathType Leaf)) {
            Copy-Item -LiteralPath $backupZip -Destination $finalZip -Force
        } elseif (-not $zipExisted) {
            Remove-Item -LiteralPath $finalZip -Force -ErrorAction SilentlyContinue
        }
        if ($hashExisted -and (Test-Path -LiteralPath $backupHash -PathType Leaf)) {
            Copy-Item -LiteralPath $backupHash -Destination $finalHash -Force
        } elseif (-not $hashExisted) {
            Remove-Item -LiteralPath $finalHash -Force -ErrorAction SilentlyContinue
        }
        Write-Warning 'Package promotion failed; previous package artifacts were restored where applicable.'
    }
    throw
}
finally {
    # Temporary working folders/files are safe to remove after success or failure.
    Remove-Item -LiteralPath $stagePath -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $checkPath -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $tempZip, $tempHash -Force -ErrorAction SilentlyContinue
}

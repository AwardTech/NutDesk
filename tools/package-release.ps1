<#
.SYNOPSIS
    Builds NutDesk in Release and packages it as the portable ZIP attached to a GitHub release.

.DESCRIPTION
    The version comes from Directory.Build.props, so the archive name, the folder inside it and the version the
    application reports can never disagree. The output goes to dist\ at the repository root:
        dist\NutDesk-<version>-portable.zip
        dist\NutDesk-<version>-portable.zip.sha256
    Attach both to the GitHub release: the in-app updater installs only a ZIP whose published SHA-256 it can check.

.PARAMETER MSBuild
    Path to MSBuild.exe. By default it is located with vswhere, which ships with Visual Studio and Build Tools.
#>
param(
    [string]$MSBuild = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if (-not $MSBuild) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) { throw "vswhere.exe not found; pass -MSBuild <path to MSBuild.exe>." }
    $MSBuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" |
               Select-Object -First 1
    if (-not $MSBuild) { throw "MSBuild not found; pass -MSBuild <path to MSBuild.exe>." }
}

[xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) { throw "No <Version> in Directory.Build.props." }

Write-Host "Building NutDesk $version (Release)..."
& $MSBuild (Join-Path $root "NutDesk.sln") -restore -p:Configuration=Release -v:minimal -nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$bin = Join-Path $root "src\NutDesk\bin\Release"
$name = "NutDesk-$version"
$dist = Join-Path $root "dist"
$stage = Join-Path $dist $name
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null

# Everything the application loads, and nothing a user has no use for: no symbols, no XML API docs.
Get-ChildItem $bin -File | Where-Object { $_.Extension -notin ".pdb", ".xml" } | Copy-Item -Destination $stage
Get-ChildItem $bin -Directory | ForEach-Object {
    $target = Join-Path $stage $_.Name
    New-Item -ItemType Directory -Force $target | Out-Null
    Get-ChildItem $_.FullName -File | Where-Object { $_.Extension -notin ".pdb", ".xml" } | Copy-Item -Destination $target
}
Copy-Item (Join-Path $root "LICENSE") (Join-Path $stage "LICENSE.txt")

$zip = Join-Path $dist "$name-portable.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
# The folder itself goes into the archive, so extracting it never scatters files into the Downloads folder.
# The entries are written one by one: ZipFile.CreateFromDirectory in Windows PowerShell stores "\" as the path
# separator, which the ZIP format does not allow and which other tools extract as file names with backslashes.
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem $stage -Recurse -File | ForEach-Object {
        $entry = $name + "/" + $_.FullName.Substring($stage.Length + 1).Replace("\", "/")
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entry,
                                                                      [IO.Compression.CompressionLevel]::Optimal)
    }
}
finally {
    $archive.Dispose()
}

# The in-app updater relies on both the layout above and this file. It installs from the folder that holds
# NutDesk.exe (the NutDesk-<version> folder, although a flat archive would do), and it refuses a package unless the
# release also carries "<zip name>.sha256" with the package's hash in it. The file is one line, "<hex>  <zip name>",
# the format sha256sum writes, ended with LF alone so that "sha256sum -c" accepts it on Linux as well.
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$zip.sha256", "$hash  $name-portable.zip`n", [Text.Encoding]::ASCII)

Write-Host ""
Write-Host "Created $zip"
Write-Host "SHA-256 $hash"

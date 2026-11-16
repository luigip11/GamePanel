param(
    [Parameter(Mandatory = $true)][string]$PackageDirectory,
    [string]$OutputDirectory = './artifacts/msix',
    [string]$IdentityName = 'GamePanel.Compatibility',
    [string]$Publisher = 'CN=GamePanelCompatibility',
    [string]$PublisherDisplayName = 'GamePanel contributors',
    [string]$PackageVersion = '0.1.16.0',
    [string]$ProbeDirectory = '',
    [string]$MakeAppxPath = ''
)
$ErrorActionPreference = 'Stop'
if ($IdentityName -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]{2,49}$') { throw 'Invalid MSIX identity name.' }
if ($PackageVersion -notmatch '^\d+\.\d+\.\d+\.0$') { throw 'Use a four-part Store version ending in .0.' }
foreach ($part in $PackageVersion.Split('.')) { if ([uint32]$part -gt 65535) { throw 'MSIX version component too large.' } }
if ([string]::IsNullOrWhiteSpace($Publisher) -or [string]::IsNullOrWhiteSpace($PublisherDisplayName)) { throw 'Publisher is required.' }
$source = (Resolve-Path -LiteralPath $PackageDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $source 'GamePanel.exe'))) { throw 'Complete published GamePanel package required.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ($output.StartsWith($source.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be outside the source package.' }
[IO.Directory]::CreateDirectory($output) | Out-Null
$stage = Join-Path $output ('stage-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($stage) | Out-Null
try {
    $app = Join-Path $stage 'App'
    [IO.Directory]::CreateDirectory($app) | Out-Null
    if (Get-ChildItem -LiteralPath $source -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Package source contains links.' }
    Copy-Item -Path (Join-Path $source '*') -Destination $app -Recurse
    # Only generated staging files are removed; the portable artifact is not changed.
    Get-ChildItem -LiteralPath $app -File -Filter 'GamePanel.Updater*' | Remove-Item -Force
    foreach ($name in @('FILES-SHA256SUMS.txt', 'LEGGIMI.txt')) {
        $generated = Join-Path $app $name
        if (Test-Path -LiteralPath $generated) { Remove-Item -LiteralPath $generated }
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../docs/MSIX.md') -Destination (Join-Path $app 'MSIX.md')
    $assets = Join-Path $stage 'PackageAssets'
    [IO.Directory]::CreateDirectory($assets) | Out-Null
    Add-Type -AssemblyName System.Drawing
    $icon = [Drawing.Icon]::new((Join-Path $PSScriptRoot '../src/GamePanel.Desktop/Assets/GamePanel.ico'), 256, 256)
    $bitmap = $icon.ToBitmap()
    try {
        foreach ($entry in @(@('StoreLogo.png', 50), @('Square44x44Logo.png', 44), @('Square150x150Logo.png', 150))) {
            $image = [Drawing.Bitmap]::new([int]$entry[1], [int]$entry[1])
            $graphics = [Drawing.Graphics]::FromImage($image)
            try {
                $graphics.Clear([Drawing.Color]::Transparent)
                $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.DrawImage($bitmap, 0, 0, [int]$entry[1], [int]$entry[1])
                $image.Save((Join-Path $assets $entry[0]), [Drawing.Imaging.ImageFormat]::Png)
            } finally { $graphics.Dispose(); $image.Dispose() }
        }
    } finally { $bitmap.Dispose(); $icon.Dispose() }
    [xml]$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../packaging/msix/AppxManifest.xml') -Raw
    $identity = $manifest.Package.Identity
    $identity.SetAttribute('Name', $IdentityName)
    $identity.SetAttribute('Publisher', $Publisher)
    $identity.SetAttribute('Version', $PackageVersion)
    $manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName
    if ($ProbeDirectory) {
        if ($IdentityName -ne 'GamePanel.CompatibilityTest') { throw 'The test probe may only be included in the compatibility-test identity.' }
        $probeSource = (Resolve-Path -LiteralPath $ProbeDirectory).Path
        if (-not (Test-Path -LiteralPath (Join-Path $probeSource 'GamePanel.DesktopChecks.exe'))) { throw 'Published desktop checks required.' }
        if (Get-ChildItem -LiteralPath $probeSource -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Probe source contains links.' }
        $probe = Join-Path $stage 'Probe'
        [IO.Directory]::CreateDirectory($probe) | Out-Null
        Copy-Item -Path (Join-Path $probeSource '*') -Destination $probe -Recurse
        $application = $manifest.Package.Applications.Application.CloneNode($true)
        $application.SetAttribute('Id', 'CompatibilityProbe')
        $application.SetAttribute('Executable', 'Probe\GamePanel.DesktopChecks.exe')
        $manifest.Package.Applications.AppendChild($application) | Out-Null
    }
    $manifest.Save((Join-Path $stage 'AppxManifest.xml'))
    if (-not $MakeAppxPath) {
        $kits = Get-ItemPropertyValue 'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots' -Name KitsRoot10
        $MakeAppxPath = Get-ChildItem -Path (Join-Path $kits 'bin/*/x64/makeappx.exe') |
            Sort-Object { [version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $MakeAppxPath -or -not (Test-Path -LiteralPath $MakeAppxPath)) { throw 'Windows SDK MakeAppx.exe is required.' }
    $package = Join-Path $output ($IdentityName + '-' + $PackageVersion + '-x64-unsigned.msix')
    if (Test-Path -LiteralPath $package) { throw 'Output already exists; choose a new output directory or version.' }
    & $MakeAppxPath pack /d $stage /p $package /o
    if ($LASTEXITCODE -ne 0) { throw 'MSIX schema/content validation failed.' }
    Copy-Item -LiteralPath (Join-Path $stage 'AppxManifest.xml') -Destination (Join-Path $output ($IdentityName + '-AppxManifest.xml'))
    $result = [ordered]@{
        SchemaVersion = 1; Package = $package; Sha256 = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
        IdentityName = $IdentityName; Publisher = $Publisher; Version = $PackageVersion; Architecture = 'x64'
        IncludesTestProbe = [bool]$ProbeDirectory; Signed = $false; StoreApproved = $false
    }
    $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output ($IdentityName + '-package.json')) -Encoding utf8
    Write-Host "PASS validated unsigned MSIX: $package"
} finally {
    # stage was created above with a random name under the generated output directory.
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
}

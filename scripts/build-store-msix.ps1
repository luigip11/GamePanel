param(
    [Parameter(Mandatory = $true)][string]$PackageDirectory,
    [string]$OutputDirectory = './artifacts/msix-store',
    [string]$PackageVersion = '1.0.17.0'
)
$ErrorActionPreference = 'Stop'
if (([version]$PackageVersion).Major -lt 1) { throw 'The Store package version must have a nonzero major component.' }
$identity = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../packaging/msix/StoreIdentity.json') -Raw | ConvertFrom-Json
& (Join-Path $PSScriptRoot 'build-msix.ps1') -PackageDirectory $PackageDirectory -OutputDirectory $OutputDirectory `
    -IdentityName $identity.IdentityName -Publisher $identity.Publisher -PublisherDisplayName $identity.PublisherDisplayName -PackageVersion $PackageVersion
$manifestPath = Join-Path $OutputDirectory ($identity.IdentityName + '-AppxManifest.xml')
[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
$languages = @($manifest.Package.Resources.Resource | ForEach-Object { $_.Language })
if ($manifest.Package.Identity.Name -cne $identity.IdentityName -or
    $manifest.Package.Identity.Publisher -cne $identity.Publisher -or
    $manifest.Package.Properties.PublisherDisplayName -cne $identity.PublisherDisplayName -or
    @($manifest.Package.Applications.Application).Count -ne 1 -or
    $manifest.Package.Applications.Application.Id -cne 'GamePanel' -or
    $manifest.Package.Properties.DisplayName -cne 'GamePanel' -or
    $languages.Count -ne 1 -or $languages[0] -cne 'it-IT') {
    throw 'Store package identity, application list or actual UI language does not match the product.'
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../packaging/msix/StoreIdentity.json') -Destination $OutputDirectory
@'
GamePanel - Partner Center identity preparation

This MSIX is UNSIGNED and is NOT intended for local installation.
Do not install the compatibility-test certificate or disable Windows security protections.
The package uses the reserved Partner Center identity but has NOT been submitted or approved.
Declared UI language: Italian (it-IT). Publisher display name: VirtualLP.
No CompatibilityProbe is included. The build and compatibility tests do not replace WACK or Store review.

You may upload the MSIX to the Packages section of a Partner Center DRAFT to validate its metadata.
Uploading a draft is separate from Submit for certification.
This alpha.17 replacement includes verified data export/import, portable-data migration and
external recovery archives updated after backups and on normal application close.
Run the final reviewer checks and keep manual publishing enabled. Certification of version
1.0.16.0 does not cover this new 1.0.17.0 package; it must be submitted for certification again.
The CI proof uses a synthetic game and is not a graphics-driver or real-game stability test.

See docs/MSIX.md and docs/STORE-SUBMISSION.md in the repository.
'@ | Set-Content -LiteralPath (Join-Path $OutputDirectory 'READ-BEFORE-UPLOAD.txt') -Encoding utf8
Write-Host 'PASS reserved Store identity, Italian language and single application; unsigned alpha.17 replacement.'

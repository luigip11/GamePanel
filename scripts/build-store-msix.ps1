param(
    [Parameter(Mandatory = $true)][string]$PackageDirectory,
    [string]$OutputDirectory = './artifacts/msix-store',
    [string]$PackageVersion = '0.1.16.0'
)
$ErrorActionPreference = 'Stop'
$identity = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../packaging/msix/StoreIdentity.json') -Raw | ConvertFrom-Json
& (Join-Path $PSScriptRoot 'build-msix.ps1') -PackageDirectory $PackageDirectory -OutputDirectory $OutputDirectory `
    -IdentityName $identity.IdentityName -Publisher $identity.Publisher -PublisherDisplayName $identity.PublisherDisplayName -PackageVersion $PackageVersion
$manifestPath = Join-Path $OutputDirectory ($identity.IdentityName + '-AppxManifest.xml')
[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
if ($manifest.Package.Identity.Name -cne $identity.IdentityName -or
    $manifest.Package.Identity.Publisher -cne $identity.Publisher -or
    $manifest.Package.Properties.PublisherDisplayName -cne $identity.PublisherDisplayName -or
    @($manifest.Package.Applications.Application).Count -ne 1 -or
    $manifest.Package.Applications.Application.Id -cne 'GamePanel') {
    throw 'Store package identity or application list does not match the reserved product.'
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../packaging/msix/StoreIdentity.json') -Destination $OutputDirectory
@'
GamePanel - Partner Center identity preparation

This MSIX is UNSIGNED and is NOT intended for local installation.
Do not install the compatibility-test certificate or disable Windows security protections.
The package uses the reserved Partner Center identity but has NOT been submitted or approved.
No CompatibilityProbe is included. The build and compatibility tests do not replace WACK or Store review.

SUBMISSION NOT READY: data export/migration and backup preservation, final privacy policy,
final-package certification testing, third-party integration/license review and reviewer test
instructions remain to be completed. Keep the Partner Center submission in Draft.

See docs/MSIX.md and docs/STORE-SUBMISSION.md in the repository.
'@ | Set-Content -LiteralPath (Join-Path $OutputDirectory 'READ-BEFORE-UPLOAD.txt') -Encoding utf8
Write-Host 'PASS reserved Store identity and single production application; unsigned, not submission-ready.'

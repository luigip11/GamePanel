param(
    [Parameter(Mandatory = $true)][string]$MsixPath,
    [Parameter(Mandatory = $true)][string]$GraphicsFixturePath,
    [string]$OutputDirectory = './artifacts/msix-proof',
    [string]$RecoveryProbePath = './artifacts/msix-probe/GamePanel.DesktopChecks.exe'
)
$ErrorActionPreference = 'Stop'
# Test certificate trust is confined to the disposable CI runner and removed in finally.
# This script is not an installation workaround for the user's Smart App Control block.
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'This test installs temporary trust only on a disposable GitHub Actions runner.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($output) | Out-Null
$msix = (Resolve-Path -LiteralPath $MsixPath).Path
$fixture = (Resolve-Path -LiteralPath $GraphicsFixturePath).Path
$hash = (Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash
$recoveryProbe = (Resolve-Path -LiteralPath $RecoveryProbePath).Path
$testRoot = Join-Path $env:RUNNER_TEMP ('GamePanelMsixProof-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($testRoot) | Out-Null
$external = Join-Path $testRoot 'speed2.exe'
Copy-Item -LiteralPath $fixture -Destination $external
$result = Join-Path $testRoot 'result.json'
$restoredData = Join-Path $env:RUNNER_TEMP ('GamePanelAfterUninstall-' + [Guid]::NewGuid().ToString('N'))
$certificate = $null; $trusted = $null; $installed = $null; $activated = @()
$oldData = [Environment]::GetEnvironmentVariable('GAMEPANEL_DATA_ROOT')
[Environment]::SetEnvironmentVariable('GAMEPANEL_DATA_ROOT', $null)
try {
    $sdk = Get-ItemPropertyValue 'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots' -Name KitsRoot10
    $signtool = Get-ChildItem -Path (Join-Path $sdk 'bin/*/x64/signtool.exe') |
        Sort-Object { [version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $signtool) { throw 'Windows SDK SignTool.exe required.' }
    $certificate = New-SelfSignedCertificate -Type Custom -Subject 'CN=GamePanelCompatibility' -KeyUsage DigitalSignature -FriendlyName 'GamePanel CI MSIX test only' -CertStoreLocation 'Cert:\CurrentUser\My' -NotAfter (Get-Date).AddDays(2) -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    $cer = Join-Path $testRoot 'test-only.cer'
    Export-Certificate -Cert $certificate -FilePath $cer | Out-Null
    $trusted = Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople'
    $signed = Join-Path $testRoot 'test-only.msix'
    Copy-Item -LiteralPath $msix -Destination $signed
    & $signtool sign /fd SHA256 /sha1 $certificate.Thumbprint $signed
    if ($LASTEXITCODE -ne 0) { throw 'CI-only test signing failed.' }
    Add-AppxPackage -Path $signed
    $installed = Get-AppxPackage -Name 'GamePanel.CompatibilityTest'
    if (-not $installed) { throw 'Compatibility package was not registered.' }

    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class MsixActivation {
  [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
  [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
  private class ActivationManager {}
  [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  private interface IActivationManager {
    void ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string id, [MarshalAs(UnmanagedType.LPWStr)] string args, uint options, out uint pid);
    void ActivateForFile(IntPtr id, IntPtr items, IntPtr verb, out uint pid);
    void ActivateForProtocol(IntPtr id, IntPtr items, out uint pid);
  }
  public static uint Start(string id, string args) {
    object instance = new ActivationManager();
    try { uint pid; ((IActivationManager)instance).ActivateApplication(id, args, 0, out pid); return pid; }
    finally { Marshal.ReleaseComObject(instance); }
  }
}
'@
    if ([MsixActivation]::GetShellWindow() -eq [IntPtr]::Zero) {
        Start-Process explorer.exe
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        while ([MsixActivation]::GetShellWindow() -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200 }
    }
    if ([MsixActivation]::GetShellWindow() -eq [IntPtr]::Zero) { throw 'Desktop shell unavailable for the packaged launch proof.' }
    $arguments = '--msix-probe "{0}" "{1}" "{2}"' -f $result, $external, $hash
    $probePid = [MsixActivation]::Start(($installed.PackageFamilyName + '!CompatibilityProbe'), $arguments)
    $activated += $probePid
    $deadline = [DateTime]::UtcNow.AddSeconds(150)
    while (-not (Test-Path -LiteralPath $result) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 250 }
    if (-not (Test-Path -LiteralPath $result)) { throw 'Packaged compatibility probe did not produce its report.' }
    Copy-Item -LiteralPath $result -Destination (Join-Path $output 'compatibility.json')
    $proof = Get-Content -LiteralPath $result -Raw | ConvertFrom-Json
    if (-not $proof.Success) { throw ('Packaged compatibility checks failed: ' + $proof.Error) }
    if ((Get-Content -LiteralPath (Join-Path $testRoot 'msix-external-write.txt') -Raw) -ne 'GamePanel MSIX external file verified') { throw 'External writes were virtualized away from the fixture.' }

    $appPid = [MsixActivation]::Start(($installed.PackageFamilyName + '!GamePanel'), '')
    $activated += $appPid
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    $process = $null
    do {
        $process = Get-Process -Id $appPid -ErrorAction SilentlyContinue
        if ($process -and $process.MainWindowHandle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $process -or $process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Packaged GamePanel application window did not open.' }
    $window = [Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    $button = $window.FindFirst([Windows.Automation.TreeScope]::Descendants,
        [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty, 'CheckSoftwareUpdates'))
    if (-not $button) { throw 'Packaged real UI update action not found.' }
    [ordered]@{ SchemaVersion = 1; Success = $true; PackageFamilyName = $installed.PackageFamilyName;
        ApplicationWindowOpened = $true; UpdateButtonFound = $true; ExternalWriteVisibleOutsidePackage = $true;
        SyntheticFixtureSha256 = $hash; ProductionSigned = $false; SmartAppControlTested = $false;
        Note = 'Temporary test trust on disposable CI runner; no real games or Store certification.' } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'installation-ui.json') -Encoding utf8

    # Verify the real data controls in the installed application.
    $advanced = $window.FindFirst([Windows.Automation.TreeScope]::Descendants,
        [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty, 'AdvancedNavigation'))
    if (-not $advanced) { throw 'Advanced navigation missing from real packaged UI.' }
    $advanced.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $backupNav = $window.FindFirst([Windows.Automation.TreeScope]::Descendants,
            [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty, 'BackupsPage'))
        if ($backupNav) { break }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    $backupNav = $window.FindFirst([Windows.Automation.TreeScope]::Descendants,
        [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty, 'BackupsPage'))
    if (-not $backupNav) { throw 'Backup navigation missing from real packaged UI.' }
    $backupNav.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $exportData = $window.FindFirst([Windows.Automation.TreeScope]::Descendants,
            [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty, 'ExportUserData'))
        if ($exportData) { break }
        Start-Sleep -Milliseconds 200
    } while ([DateTime]::UtcNow -lt $deadline)
    foreach ($automationId in @('ExportUserData', 'ImportUserData', 'MigratePortableData', 'ConfigureExternalRecovery')) {
        $control = $window.FindFirst([Windows.Automation.TreeScope]::Descendants,
            [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId))
        if (-not $control) { throw ('Packaged data transfer control missing: ' + $automationId) }
    }
    $process = Get-Process -Id $appPid -ErrorAction SilentlyContinue
    if ($process) { $process.CloseMainWindow() | Out-Null; if (-not $process.WaitForExit(10000)) { $process.Kill(); $process.WaitForExit() } }
    $archive = Join-Path $testRoot 'GamePanel-preserved.zip'
    if (-not (Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $proof.RecoverySha256) { throw 'External recovery archive missing before uninstall.' }
    Remove-AppxPackage -Package $installed.PackageFullName
    if (Get-AppxPackage -Name 'GamePanel.CompatibilityTest') { throw 'Package remained registered after uninstall.' }
    $installed = $null
    if (-not (Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $proof.RecoverySha256) { throw 'Uninstall removed or changed the external recovery archive.' }
    & $recoveryProbe --recover-user-data $archive $restoredData $external $proof.RecoverySnapshot $hash
    if ($LASTEXITCODE -ne 0) { throw 'Actual post-uninstall data recovery failed.' }
    [ordered]@{ Success = $true; PackageActuallyUninstalled = $true; ExternalArchiveUnchanged = $true;
        LibraryRecovered = $true; BackupRecoveredAndUsed = $true; DataControlsPresent = $true;
        SmartAppControlTested = $false; RealGamesTested = $false } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'uninstall-recovery.json') -Encoding utf8
    Write-Host 'PASS installed MSIX, package identity/data, external backup/restore, Explorer broker, update gate and actual WPF activation.'
} catch {
    [ordered]@{ Success = $false; Error = $_.ToString(); ProductionSigned = $false; SmartAppControlTested = $false } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'failure.json') -Encoding utf8
    throw
} finally {
    foreach ($id in $activated) {
        try {
            $process = Get-Process -Id $id -ErrorAction SilentlyContinue
            if ($process) { $process.CloseMainWindow() | Out-Null; if (-not $process.WaitForExit(5000)) { $process.Kill() } }
        } catch { Write-Warning ('Process cleanup: ' + $_) }
    }
    if ($installed) {
        try { Remove-AppxPackage -Package $installed.PackageFullName }
        catch { Write-Warning ('Package cleanup: ' + $_) }
    }
    if ($trusted) { Remove-Item -LiteralPath ('Cert:\LocalMachine\TrustedPeople\' + $trusted.Thumbprint) -ErrorAction Continue }
    if ($certificate) { Remove-Item -LiteralPath ('Cert:\CurrentUser\My\' + $certificate.Thumbprint) -ErrorAction Continue }
    [Environment]::SetEnvironmentVariable('GAMEPANEL_DATA_ROOT', $oldData)
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
    if (Test-Path -LiteralPath $restoredData) { Remove-Item -LiteralPath $restoredData -Recurse -Force }
}

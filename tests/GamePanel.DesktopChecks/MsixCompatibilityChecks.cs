using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using GamePanel.Core;

internal static class MsixCompatibilityChecks
{
    // Installed only as a second application in the CI compatibility package, not the submission package.
    public static int Run(string reportPath, string fixturePath, string expectedFixtureHash)
    {
        reportPath = Path.GetFullPath(reportPath);
        var checks = new List<string>();
        string? data = null, family = null;
        try
        {
            family = AppDistribution.PackageFamilyName;
            Require(family is not null && family.StartsWith("GamePanel.CompatibilityTest_", StringComparison.Ordinal), "Not running in the compatibility MSIX identity.");
            Require(Environment.GetEnvironmentVariable("GAMEPANEL_DATA_ROOT") is null, "A diagnostic data override would invalidate the package data check.");
            data = GamePanelPaths.Root;
            var expectedData = AppDistribution.ResolveDataRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), family);
            Require(data == expectedData && !data.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase), "Package data was placed in the installation directory.");
            checks.Add("Real package identity and writable LocalState data root");

            fixturePath = Path.GetFullPath(fixturePath);
            Require(Path.GetFileName(fixturePath) == "speed2.exe", "Unexpected fixture executable.");
            Require(expectedFixtureHash.Length == 64 && expectedFixtureHash.All(Uri.IsHexDigit), "Missing fixture hash.");
            using (var stream = File.OpenRead(fixturePath))
                Require(Convert.ToHexString(SHA256.HashData(stream)).Equals(expectedFixtureHash, StringComparison.OrdinalIgnoreCase), "Synthetic fixture hash mismatch.");
            Require(!fixturePath.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase), "Fixture must be outside WindowsApps.");
            var game = Path.GetDirectoryName(fixturePath)!;
            var config = Path.Combine(game, "ReShade.ini");
            const string original = "[Fixture]\nValue=original\n";
            Require(!File.Exists(config), "External fixture must start without configuration.");
            File.WriteAllText(config, original);
            var inspected = new GameInspector().Inspect(fixturePath);
            var library = new GameLibrary(GameLibrary.DefaultFolder);
            library.Remember(inspected);
            Require(library.Load().Games.Any(g => g.ExecutablePath == fixturePath), "Package library did not persist an external executable.");
            checks.Add("Inspect and persist an external synthetic game");

            var backups = new BackupService(BackupService.DefaultStore, _ => false);
            var snapshot = backups.Create(inspected);
            File.Delete(config);
            Require(backups.Preview(fixturePath, snapshot.Id).MissingCount > 0, "Missing external config was not detected.");
            backups.Restore(fixturePath, snapshot.Id);
            Require(File.ReadAllText(config) == original, "External configuration restore failed.");
            File.WriteAllText(Path.Combine(game, "msix-external-write.txt"), "GamePanel MSIX external file verified");
            checks.Add("Backup and restore outside the package, with data in LocalState");

            Exception? failure = null;
            var thread = new Thread(() =>
            {
                Application? app = null; Window? window = null;
                try
                {
                    app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    var type = Assembly.Load("GamePanel").GetType("GamePanel.Desktop.MainWindow", true)!;
                    window = (Window)Activator.CreateInstance(type)!;
                    var install = type.GetMethod("InstallSoftwareUpdateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    try
                    {
                        // Null is intentional: the package gate must reject before reading a release or changing UI/files.
                        ((Task)install.Invoke(window, [null])!).GetAwaiter().GetResult();
                        throw new Exception("Portable updater was allowed in an MSIX process.");
                    }
                    catch (InvalidOperationException e) when (e.Message.Contains("canale di installazione", StringComparison.Ordinal)) { }
                }
                catch (Exception e) { failure = e; }
                finally { window?.Close(); app?.Shutdown(); }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Require(thread.Join(TimeSpan.FromSeconds(45)), "Packaged WPF/update gate timed out.");
            if (failure is not null) throw new InvalidOperationException("Packaged WPF/update gate failed.", failure);
            checks.Add("Construct the real WPF window and reject portable self-update before side effects");

            // Reuses the existing test of the production Explorer COM broker against the exact locally compiled fixture.
            Require(ExplorerLaunchChecks.Run(fixturePath) == 0, "Packaged Explorer broker failed.");
            checks.Add("Explorer launches the external x86 fixture with the correct working directory and isolated environment");
            Write(true, null);
            return 0;
        }
        catch (Exception e) { Write(false, e.ToString()); return 1; }

        void Write(bool success, string? error)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            var pending = reportPath + ".tmp";
            File.WriteAllText(pending, JsonSerializer.Serialize(new
            { SchemaVersion = 1, Success = success, PackageFamilyName = family, DataRoot = data, Checks = checks,
                ExternalFixture = fixturePath, Error = error, RealGamesTested = false, SmartAppControlTested = false,
                ProductionSigned = false }, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(pending, reportPath);
        }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

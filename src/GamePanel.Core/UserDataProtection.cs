using System.Text.Json;
using static GamePanel.Core.BackupService;

namespace GamePanel.Core;

/// <summary>The external destination is machine-local and deliberately excluded from portable archives.</summary>
public static class UserDataProtection
{
    private sealed record Preferences(int SchemaVersion, string ArchivePath);
    private static string SettingsFile => Path.Combine(GamePanelPaths.Root, "DataProtection.json");
    public static string? Destination
    {
        get
        {
            GuardPath(SettingsFile);
            if (!File.Exists(SettingsFile)) return null;
            if (new FileInfo(SettingsFile).Length > 8192) throw new InvalidDataException("Preferenza copia esterna non valida.");
            Preferences preferences;
            try { preferences = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(SettingsFile)) ?? throw new InvalidDataException("Preferenza copia esterna vuota."); }
            catch (JsonException e) { throw new InvalidDataException("Preferenza copia esterna danneggiata.", e); }
            if (preferences.SchemaVersion != 1 || string.IsNullOrWhiteSpace(preferences.ArchivePath)
                || !Path.IsPathFullyQualified(preferences.ArchivePath)) throw new InvalidDataException("Percorso copia esterna non valido.");
            UserDataArchive.ValidateExternalPath(GamePanelPaths.Root, preferences.ArchivePath, durable: true);
            return preferences.ArchivePath;
        }
    }

    public static void Configure(string archivePath)
    {
        if (!Path.IsPathFullyQualified(archivePath)) throw new InvalidDataException("Scegli un percorso assoluto.");
        archivePath = Path.GetFullPath(archivePath);
        UserDataArchive.ValidateExternalPath(GamePanelPaths.Root, archivePath, durable: true);
        if (!archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Scegli un file .zip.");
        if (!Directory.Exists(Path.GetDirectoryName(archivePath))) throw new DirectoryNotFoundException("La cartella esterna non è disponibile.");
        Directory.CreateDirectory(GamePanelPaths.Root); GuardPath(SettingsFile);
        var temporary = Path.Combine(GamePanelPaths.Root, ".transfer-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(output, new Preferences(1, archivePath)); output.Flush(true); }
            GuardPath(SettingsFile); File.Move(temporary, SettingsFile, true);
        }
        finally { GuardPath(temporary); if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static UserDataTransferResult Preserve(CancellationToken token = default, IProgress<BackupProgress>? progress = null)
    {
        var destination = Destination ?? throw new InvalidOperationException("Scegli prima una copia di sicurezza esterna.");
        // A missing external drive must fail clearly, never fall back to LocalState.
        if (!Directory.Exists(Path.GetDirectoryName(destination))) throw new DirectoryNotFoundException("La cartella esterna non è disponibile. Il backup interno resta conservato.");
        if (File.Exists(destination)) UserDataArchive.Verify(destination, token);
        return UserDataArchive.Export(GamePanelPaths.Root, destination, overwrite: true, token: token, progress: progress);
    }
}

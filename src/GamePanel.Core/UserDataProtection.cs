using System.Text.Json;
using static GamePanel.Core.BackupService;

namespace GamePanel.Core;

/// <summary>The external destination is machine-local and deliberately excluded from portable archives.</summary>
public static class UserDataProtection
{
    private sealed record Preferences(int SchemaVersion, string ArchivePath);
    private static string SettingsFile => Path.Combine(GamePanelPaths.Root, "DataProtection.json");
    public static string? Destination => ReadDestination(GamePanelPaths.Root);

    private static string? ReadDestination(string root)
    {
        var settingsFile = Path.Combine(root, "DataProtection.json");
            GuardPath(settingsFile);
            if (!File.Exists(settingsFile)) return null;
            if (new FileInfo(settingsFile).Length > 8192) throw new InvalidDataException("Preferenza copia esterna non valida.");
            Preferences preferences;
            try { preferences = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(settingsFile)) ?? throw new InvalidDataException("Preferenza copia esterna vuota."); }
            catch (JsonException e) { throw new InvalidDataException("Preferenza copia esterna danneggiata.", e); }
            if (preferences.SchemaVersion != 1 || string.IsNullOrWhiteSpace(preferences.ArchivePath)
                || !Path.IsPathFullyQualified(preferences.ArchivePath)) throw new InvalidDataException("Percorso copia esterna non valido.");
            UserDataArchive.ValidateExternalPath(root, preferences.ArchivePath, durable: true);
            return preferences.ArchivePath;

    }

    public static void RequireIndependentImportSource(string root, string archivePath)
    {
        var destination = ReadDestination(root);
        if (destination is not null && string.Equals(Path.GetFullPath(destination), Path.GetFullPath(archivePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Questo ZIP è la copia esterna attiva e verrà aggiornata dall'app. Prima creane una copia indipendente con Esplora file e importa quella; la sorgente deve restare conservata anche se l'importazione viene interrotta.");
    }

    public static void Configure(string archivePath)
    {
        if (!Path.IsPathFullyQualified(archivePath)) throw new InvalidDataException("Scegli un percorso assoluto.");
        archivePath = Path.GetFullPath(archivePath);
        UserDataArchive.ValidateExternalPath(GamePanelPaths.Root, archivePath, durable: true);
        if (!archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Scegli un file .zip.");
        if (!Directory.Exists(Path.GetDirectoryName(archivePath))) throw new DirectoryNotFoundException("La cartella esterna non è disponibile.");
        if (File.Exists(archivePath))
        {
            string? current = null;
            try { current = Destination; }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) { }
            if (current is null || !string.Equals(Path.GetFullPath(current), archivePath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Questo archivio esiste già e viene conservato. Se contiene dati da recuperare, importalo prima. Per la nuova copia automatica scegli un nome ZIP diverso: non sovrascrivere l'unica copia precedente.");
        }
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

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using static GamePanel.Core.BackupService;

namespace GamePanel.Core;

public sealed record UserDataFile(string Path, long Bytes, string Sha256);
public sealed record UserDataManifest(int SchemaVersion, string Kind, DateTimeOffset CreatedAt, IReadOnlyList<UserDataFile> Files);
public sealed record UserDataTransferResult(int Files, long Bytes);

/// <summary>Local, bounded, verified transfers. Never executes files or writes into game folders.</summary>
public static class UserDataArchive
{
    public const int MaximumFiles = 100000;
    public const long MaximumBytes = 16L * 1024 * 1024 * 1024;
    private const long MaximumFileBytes = 2L * 1024 * 1024 * 1024;
    private const string ManifestName = "GamePanel-data.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static bool IsTransferFile(string relative)
    {
        var parts = ValidateRelative(relative).Split('/');
        return !parts.Any(p => p.StartsWith(".transfer-", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".pending", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".partial", StringComparison.OrdinalIgnoreCase))
            && !parts[0].Equals("Updates", StringComparison.OrdinalIgnoreCase)
            && !parts[0].Equals("DataProtection.json", StringComparison.OrdinalIgnoreCase);
    }

    public static void ValidateExternalPath(string dataRoot, string path, bool durable = false)
    {
        dataRoot = Path.GetFullPath(dataRoot); path = Path.GetFullPath(path);
        GuardPath(dataRoot); GuardPath(path);
        if (Inside(path, dataRoot)) throw new InvalidDataException("Scegli un archivio fuori dalla cartella dati di GamePanel.");
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(local) && Inside(path, Path.Combine(local, "Packages")))
            throw new InvalidDataException("La copia di sicurezza deve essere fuori dai dati delle app MSIX.");
        if (durable && (Inside(path, Path.GetTempPath()) || (!string.IsNullOrEmpty(local) && Inside(path, local))
            || Inside(path, AppContext.BaseDirectory)))
            throw new InvalidDataException("Scegli Documenti, un disco esterno o un'altra cartella personale; non usare Temp, AppData o la cartella dell'app.");
    }

    public static UserDataTransferResult Export(string dataRoot, string archivePath, bool overwrite = false,
        CancellationToken token = default, IProgress<BackupProgress>? progress = null)
    {
        dataRoot = Path.GetFullPath(dataRoot); archivePath = Path.GetFullPath(archivePath);
        ValidateExternalPath(dataRoot, archivePath);
        if (!archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("L'archivio deve avere estensione .zip.");
        if (File.Exists(archivePath) && !overwrite) throw new IOException("L'archivio esiste già.");
        var paths = Enumerate(dataRoot, token);
        var pending = Path.Combine(Path.GetDirectoryName(archivePath)!, ".transfer-" + Guid.NewGuid().ToString("N") + ".tmp");
        var files = new List<UserDataFile>(); long total = 0;
        try
        {
            using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var relative in paths)
                    {
                        token.ThrowIfCancellationRequested();
                        var source = GameFile(dataRoot, relative);
                        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
                        var entry = zip.CreateEntry("data/" + relative, CompressionLevel.Fastest);
                        using var destination = entry.Open();
                        var copied = Copy(input, destination, Math.Min(MaximumFileBytes, MaximumBytes - total), token);
                        total = checked(total + copied.Bytes);
                        files.Add(new(relative, copied.Bytes, copied.Hash));
                        progress?.Report(new("Esportazione dati", files.Count, paths.Count));
                    }
                    var manifest = zip.CreateEntry(ManifestName);
                    using var metadata = manifest.Open();
                    JsonSerializer.Serialize(metadata, new UserDataManifest(1, "GamePanelUserData", DateTimeOffset.UtcNow, files), JsonOptions);
                }
                output.Flush(true);
            }
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                if (HashFile(GameFile(dataRoot, file.Path), token) != file.Sha256)
                    throw new InvalidDataException("Dati modificati durante l'esportazione. Chiudi l'altra copia di GamePanel e riprova.");
            }
            if (!paths.SequenceEqual(Enumerate(dataRoot, token), StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("La cartella dati è cambiata durante l'esportazione.");
            // Verify the actual ZIP before replacing an existing recovery archive.
            Verify(pending, token);
            token.ThrowIfCancellationRequested(); GuardPath(archivePath);
            File.Move(pending, archivePath, overwrite);
            return new(files.Count, total);
        }
        finally { GuardPath(pending); if (File.Exists(pending)) File.Delete(pending); }
    }

    public static UserDataTransferResult Verify(string archivePath, CancellationToken token = default)
    {
        GuardPath(archivePath);
        using var file = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var zip = new ZipArchive(file, ZipArchiveMode.Read);
        var manifest = ReadManifest(zip);
        foreach (var item in manifest.Files)
        {
            using var input = zip.GetEntry("data/" + item.Path)!.Open();
            var copied = Copy(input, Stream.Null, item.Bytes, token);
            if (copied.Bytes != item.Bytes || copied.Hash != item.Sha256) throw new InvalidDataException("Archivio alterato o incompleto: " + item.Path);
        }
        return new(manifest.Files.Count, manifest.Files.Sum(f => f.Bytes));
    }

    public static UserDataTransferResult Import(string archivePath, string dataRoot,
        CancellationToken token = default, IProgress<BackupProgress>? progress = null)
    {
        dataRoot = Path.GetFullPath(dataRoot); archivePath = Path.GetFullPath(archivePath);
        ValidateExternalPath(dataRoot, archivePath);
        Directory.CreateDirectory(dataRoot); GuardPath(dataRoot);
        using var file = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var zip = new ZipArchive(file, ZipArchiveMode.Read);
        var manifest = ReadManifest(zip);
        var staging = Path.Combine(dataRoot, ".transfer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var installed = new List<string>();
        try
        {
            var count = 0;
            foreach (var item in manifest.Files)
            {
                token.ThrowIfCancellationRequested();
                var destination = GameFile(staging, item.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!); GuardPath(destination);
                using var input = zip.GetEntry("data/" + item.Path)!.Open();
                using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var copied = Copy(input, output, item.Bytes, token);
                    if (copied.Bytes != item.Bytes || copied.Hash != item.Sha256) throw new InvalidDataException("Archivio alterato: " + item.Path);
                    output.Flush(true);
                }
                progress?.Report(new("Verifica importazione", ++count, manifest.Files.Count));
            }
            if (File.Exists(Path.Combine(staging, "Library", "library.json")))
                _ = new GameLibrary(Path.Combine(staging, "Library")).Load();
            // Detect every conflict before publishing the first file.
            foreach (var item in manifest.Files) CheckDestination(dataRoot, item, token);
            foreach (var item in manifest.Files)
            {
                token.ThrowIfCancellationRequested(); CheckDestination(dataRoot, item, token);
                var destination = GameFile(dataRoot, item.Path);
                if (File.Exists(destination)) continue; // Identical files were verified above.
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!); GuardPath(destination);
                File.Move(GameFile(staging, item.Path), destination);
                installed.Add(destination);
            }
            return new(manifest.Files.Count, manifest.Files.Sum(f => f.Bytes));
        }
        catch
        {
            // Only files installed by this operation are rolled back; pre-existing files are never removed.
            foreach (var path in installed.AsEnumerable().Reverse())
            { GuardPath(path); if (File.Exists(path)) File.Delete(path); }
            throw;
        }
        finally { GuardPath(staging); if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }

    public static UserDataTransferResult ImportDirectory(string sourceRoot, string dataRoot,
        CancellationToken token = default, IProgress<BackupProgress>? progress = null)
    {
        sourceRoot = Path.GetFullPath(sourceRoot); dataRoot = Path.GetFullPath(dataRoot);
        if (Inside(sourceRoot, dataRoot) || Inside(dataRoot, sourceRoot)) throw new InvalidDataException("Scegli una cartella dati distinta da quella attuale.");
        if (!File.Exists(Path.Combine(sourceRoot, "Library", "library.json")) && !Directory.Exists(Path.Combine(sourceRoot, "Backups")))
            throw new InvalidDataException("Non è una cartella dati GamePanel: seleziona la cartella che contiene Library e/o Backups, non la cartella dell'app o del gioco.");
        var temporary = Path.Combine(Path.GetTempPath(), "GamePanel-transfer-" + Guid.NewGuid().ToString("N") + ".zip");
        try { Export(sourceRoot, temporary, token: token, progress: progress); return Import(temporary, dataRoot, token, progress); }
        finally { GuardPath(temporary); if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void CheckDestination(string root, UserDataFile item, CancellationToken token)
    {
        var path = GameFile(root, item.Path);
        if (Directory.Exists(path) || File.Exists(path) && HashFile(path, token) != item.Sha256)
            throw new InvalidDataException("Dati diversi già presenti: " + item.Path + ". Nessun dato esistente è stato sovrascritto.");
    }

    private static List<string> Enumerate(string root, CancellationToken token)
    {
        GuardPath(root);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Cartella dati non trovata.");
        var files = new List<string>(); var pending = new Stack<string>(); pending.Push(root); long total = 0; var seen = 0;
        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested(); var directory = pending.Pop(); GuardPath(directory);
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                token.ThrowIfCancellationRequested();
                if (++seen > MaximumFiles * 3) throw new InvalidDataException("Troppi elementi nella cartella dati.");
                var relative = ValidateRelative(Path.GetRelativePath(root, path));
                if (!IsTransferFile(relative)) continue;
                GuardPath(path);
                if (Directory.Exists(path)) pending.Push(path);
                else
                {
                    var length = new FileInfo(path).Length;
                    if (length > MaximumFileBytes || length > MaximumBytes - total || files.Count >= MaximumFiles)
                        throw new InvalidDataException("Dati oltre i limiti di esportazione (16 GiB, 100.000 file, 2 GiB per file).");
                    total += length; files.Add(relative);
                }
            }
        }
        return files.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static UserDataManifest ReadManifest(ZipArchive zip)
    {
        if (zip.Entries.Count is < 1 or > MaximumFiles + 1) throw new InvalidDataException("Numero di file nell'archivio non valido.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            if (!names.Add(entry.FullName) || entry.FullName.Contains('\\') || entry.FullName.EndsWith('/')
                || ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000
                || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Nomi duplicati, cartelle o link nell'archivio.");
        }
        var metadata = zip.GetEntry(ManifestName) ?? throw new InvalidDataException("Non è un archivio dati GamePanel.");
        if (metadata.Length > 16 * 1024 * 1024) throw new InvalidDataException("Indice dell'archivio troppo grande.");
        UserDataManifest manifest;
        try
        {
            using var stream = metadata.Open();
            using var bounded = new MemoryStream();
            Copy(stream, bounded, 16 * 1024 * 1024, CancellationToken.None); bounded.Position = 0;
            manifest = JsonSerializer.Deserialize<UserDataManifest>(bounded) ?? throw new InvalidDataException("Indice vuoto.");
        }
        catch (JsonException e) { throw new InvalidDataException("Indice dell'archivio danneggiato.", e); }
        if (manifest.SchemaVersion != 1 || manifest.Kind != "GamePanelUserData" || manifest.Files is null
            || manifest.Files.Count > MaximumFiles || manifest.Files.Count + 1 != zip.Entries.Count)
            throw new InvalidDataException("Formato archivio non supportato.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
        foreach (var item in manifest.Files)
        {
            if (item is null || !IsTransferFile(item.Path) || ValidateRelative(item.Path) != item.Path
                || !paths.Add(item.Path) || item.Bytes is < 0 or > MaximumFileBytes
                || item.Bytes > MaximumBytes - total || item.Sha256 is null || item.Sha256.Length != 64
                || item.Sha256.Any(c => !Uri.IsHexDigit(c)) || item.Sha256 != item.Sha256.ToLowerInvariant()
                || zip.GetEntry("data/" + item.Path) is not { } entry || entry.Length != item.Bytes)
                throw new InvalidDataException("Indice dell'archivio non valido.");
            total += item.Bytes;
        }
        return manifest;
    }

    private static (long Bytes, string Hash) Copy(Stream input, Stream output, long limit, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024]; long total = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var read = input.Read(buffer, 0, buffer.Length);
            if (read == 0) break;
            if (read > limit - total) throw new InvalidDataException("File oltre i limiti dichiarati.");
            hash.AppendData(buffer, 0, read); output.Write(buffer, 0, read); total += read;
        }
        token.ThrowIfCancellationRequested();
        return (total, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

}

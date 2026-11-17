using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using GamePanel.Core;

internal static class UserDataTransferChecks
{
    private sealed class InlineProgress(Action<BackupProgress> action) : IProgress<BackupProgress>
    { public void Report(BackupProgress value) => action(value); }

    public static void Register(Action<string, Action> check, string root, Func<string, bool, bool, string, string> makeExe)
    {
        string Folder()
        { var folder = Path.Combine(root, "transfer-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); return folder; }
        void Reject(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is InvalidDataException or IOException or ArgumentException or OperationCanceledException) { return; }
            throw new Exception("Trasferimento non sicuro accettato.");
        }
        void Require(bool value, string message) { if (!value) throw new Exception(message); }
        string Hash(string value) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        void Archive(string path, UserDataFile item, string content, Action<ZipArchiveEntry>? configure = null)
        {
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            using (var meta = zip.CreateEntry("GamePanel-data.json").Open())
                JsonSerializer.Serialize(meta, new UserDataManifest(1, "GamePanelUserData", DateTimeOffset.UtcNow, [item]));
            var entry = zip.CreateEntry("data/" + item.Path); configure?.Invoke(entry);
            using var writer = new StreamWriter(entry.Open()); writer.Write(content);
        }

        check("Dati: libreria, cover, preferenze e backup recuperabili dopo eliminazione dei dati interni", () =>
        {
            var game = Folder(); var source = Folder(); var archive = Path.Combine(Folder(), "recovery.zip");
            var exe = makeExe(Path.Combine(game, "speed2.exe"), false, false, "d3d9.dll");
            var report = new GameInspector().Inspect(exe);
            var library = new GameLibrary(Path.Combine(source, "Library"));
            var saved = library.Remember(report); library.Rename(saved.SelectedId!, "Il mio gioco");
            Directory.CreateDirectory(Path.Combine(source, "Library", "Covers"));
            File.WriteAllBytes(Path.Combine(source, "Library", "Covers", "local.png"), [1, 2, 3]);
            Directory.CreateDirectory(Path.Combine(source, "Settings"));
            File.WriteAllText(Path.Combine(source, "Settings", "display.json"), "{\"width\":3440}");
            File.WriteAllText(Path.Combine(source, "DataProtection.json"), "machine-local preference");
            Directory.CreateDirectory(Path.Combine(source, "Updates"));
            File.WriteAllText(Path.Combine(source, "Updates", "ignore.txt"), "cache");
            File.WriteAllText(Path.Combine(game, "ReShade.ini"), "original");
            var backup = new BackupService(Path.Combine(source, "Backups"), _ => false).Create(report);
            var exported = UserDataArchive.Export(source, archive);
            Require(exported.Files > 4 && UserDataArchive.Verify(archive) == exported, "Archivio non verificabile.");
            Directory.Delete(source, true); // Simulate reset/uninstall after a verified external export.
            UserDataArchive.Import(archive, source);
            Require(new GameLibrary(Path.Combine(source, "Library")).Load().Games.Single().Title == "Il mio gioco", "Libreria persa.");
            Require(File.ReadAllBytes(Path.Combine(source, "Library", "Covers", "local.png")).SequenceEqual(new byte[] { 1, 2, 3 }), "Cover persa.");
            Require(File.Exists(Path.Combine(source, "Settings", "display.json")), "Preferenze perse.");
            Require(!File.Exists(Path.Combine(source, "DataProtection.json")) && !Directory.Exists(Path.Combine(source, "Updates")), "Preferenze macchina o updater importati.");
            File.Delete(Path.Combine(game, "ReShade.ini"));
            var recovered = new BackupService(Path.Combine(source, "Backups"), _ => false);
            Require(recovered.List(exe).Single().Id == backup.Id, "Snapshot non riconosciuto.");
            recovered.Restore(exe, backup.Id);
            Require(File.ReadAllText(Path.Combine(game, "ReShade.ini")) == "original", "Backup non utilizzabile dopo recupero.");
        });

        check("Dati: migrazione portatile esplicita conserva la sorgente e il doppio import è idempotente", () =>
        {
            var source = Folder(); var destination = Folder();
            var game = Folder(); var report = new GameInspector().Inspect(makeExe(Path.Combine(game, "game.exe"), false, false, "d3d9.dll"));
            var library = new GameLibrary(Path.Combine(source, "Library")); library.Remember(report);
            var original = File.ReadAllBytes(library.FilePath);
            UserDataArchive.ImportDirectory(source, destination); UserDataArchive.ImportDirectory(source, destination);
            Require(File.ReadAllBytes(library.FilePath).SequenceEqual(original), "Sorgente modificata.");
            Require(new GameLibrary(Path.Combine(destination, "Library")).Load().Games.Count == 1, "Import duplicato.");
            Reject(() => UserDataArchive.ImportDirectory(source, source));
        });

        check("Dati: conflitto rilevato prima di pubblicare file, nessuna sovrascrittura", () =>
        {
            var source = Folder(); var destination = Folder(); var archive = Path.Combine(Folder(), "data.zip");
            File.WriteAllText(Path.Combine(source, "a.txt"), "new"); File.WriteAllText(Path.Combine(source, "z.txt"), "source");
            File.WriteAllText(Path.Combine(destination, "z.txt"), "existing");
            UserDataArchive.Export(source, archive);
            Reject(() => UserDataArchive.Import(archive, destination));
            Require(!File.Exists(Path.Combine(destination, "a.txt")) && File.ReadAllText(Path.Combine(destination, "z.txt")) == "existing", "Conflitto ha modificato la destinazione.");
        });

        check("Dati: traversal, nomi Windows pericolosi e preferenze macchina non estraibili", () =>
        {
            foreach (var relative in new[] { "../escape.txt", "x/../../escape.txt", "CON.txt", "x:y", "DataProtection.json" })
            {
                var destination = Folder(); var archive = Path.Combine(Folder(), "bad.zip");
                Archive(archive, new(relative, 1, Hash("x")), "x");
                Reject(() => UserDataArchive.Import(archive, destination));
                Require(!Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories).Any(), "Payload pericoloso pubblicato.");
            }
        });

        check("Dati: payload alterato, dimensione falsa e link ZIP bloccano senza pubblicare", () =>
        {
            foreach (var kind in new[] { "hash", "length", "symlink" })
            {
                var destination = Folder(); var archive = Path.Combine(Folder(), "bad.zip");
                var item = new UserDataFile("Settings/test.txt", kind == "length" ? 0 : 1, Hash(kind == "hash" ? "y" : "x"));
                Archive(archive, item, "x", kind == "symlink" ? entry => entry.ExternalAttributes = (0xa1ff << 16) : null);
                Reject(() => UserDataArchive.Import(archive, destination));
                Require(!Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories).Any(), "Archivio alterato pubblicato.");
            }
        });

        check("Dati: duplicati con differenze di maiuscole e archivio sovradimensionato respinti", () =>
        {
            var destination = Folder(); var archive = Path.Combine(Folder(), "duplicate.zip");
            Archive(archive, new("Settings/a.txt", 1, Hash("x")), "x");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update)) zip.CreateEntry("DATA/Settings/a.txt");
            Reject(() => UserDataArchive.Import(archive, destination));
            var oversized = Path.Combine(Folder(), "oversized.zip");
            Archive(oversized, new("Settings/a.txt", UserDataArchive.MaximumBytes + 1, Hash("x")), "x");
            Reject(() => UserDataArchive.Import(oversized, destination));
        });

        check("Dati: annullamento e sorgente modificata conservano l'ultimo archivio esterno valido", () =>
        {
            var source = Folder(); var archive = Path.Combine(Folder(), "last.zip");
            File.WriteAllText(Path.Combine(source, "a.txt"), "first"); UserDataArchive.Export(source, archive);
            var original = File.ReadAllBytes(archive);
            using var cancellation = new CancellationTokenSource();
            Reject(() => UserDataArchive.Export(source, archive, true, cancellation.Token, new InlineProgress(_ => cancellation.Cancel())));
            Require(File.ReadAllBytes(archive).SequenceEqual(original), "Annullamento ha sostituito la copia valida.");
            Reject(() => UserDataArchive.Export(source, archive, true, progress: new InlineProgress(_ => File.WriteAllText(Path.Combine(source, "a.txt"), "changed"))));
            Require(File.ReadAllBytes(archive).SequenceEqual(original), "Mutazione ha sostituito la copia valida.");
        });

        check("Dati: destinazione interna e link nella sorgente sono bloccati", () =>
        {
            var source = Folder(); File.WriteAllText(Path.Combine(source, "a.txt"), "x");
            Reject(() => UserDataArchive.Export(source, Path.Combine(source, "data.zip")));
            var outside = Path.Combine(Folder(), "outside.txt"); File.WriteAllText(outside, "secret");
            File.CreateSymbolicLink(Path.Combine(source, "link.txt"), outside);
            Reject(() => UserDataArchive.Export(source, Path.Combine(Folder(), "data.zip")));
        });

        check("Dati: archivio vuoto valido e JSON non valido non alterano la destinazione", () =>
        {
            var source = Folder(); var destination = Folder(); var archive = Path.Combine(Folder(), "empty.zip");
            Require(UserDataArchive.Export(source, archive).Files == 0 && UserDataArchive.Import(archive, destination).Files == 0, "Archivio vuoto non gestito.");
            var invalid = Path.Combine(Folder(), "invalid.zip");
            using (var zip = ZipFile.Open(invalid, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(zip.CreateEntry("GamePanel-data.json").Open())) writer.Write("{bad");
            Reject(() => UserDataArchive.Import(invalid, destination));
            Require(!Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories).Any(), "JSON non valido ha scritto dati.");
        });
    }
}

using System.IO;
using System.Windows;
using System.Windows.Controls;
using GamePanel.Core;
using Microsoft.Win32;

namespace GamePanel.Desktop;

internal sealed partial class MainWindow
{
    private string _dataTransferMessage = "L'archivio contiene libreria, copertine, preferenze e backup di GamePanel. Non include una copia completa dei giochi o tutti i loro salvataggi.";
    private bool _dataProtectionReadFailed;
    private bool _externalCopyFailed;
    private string? ReadRecoveryDestination()
    {
        try { var destination = UserDataProtection.Destination; _dataProtectionReadFailed = false; return destination; }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { _dataProtectionReadFailed = true; _dataTransferMessage = "Controlla la posizione della copia esterna: " + e.Message; return null; }
    }

    private void RenderUserDataProtection()
    {
        var destination = ReadRecoveryDestination();
        Card("Conserva libreria e backup",
            (AppDistribution.HasPackageIdentity
                ? "Disinstallare o reimpostare l'app Store può eliminare i dati interni, inclusi i backup. "
                : "Conserva una copia indipendente prima di cambiare PC o passare alla versione Store. ")
            + "Scegli una copia ZIP fuori da AppData e dalla cartella dell'app: viene aggiornata dopo i nuovi backup e alla chiusura regolare. "
            + "Non eliminare l'app prima di aver completato e verificato l'esportazione.");
        var state = Text(destination is null ? "COPIA ESTERNA NON CONFIGURATA" : "Copia esterna: " + destination, 12, Accent);
        state.TextWrapping = TextWrapping.Wrap; state.Margin = new Thickness(0, 0, 0, 12);
        System.Windows.Automation.AutomationProperties.SetAutomationId(state, "DataProtectionStatus"); _content.Children.Add(state);
        var export = Button("Esporta dati e backup", async (_, _) => await ExportUserDataAsync());
        SmallAction(export, "Esporta dati e backup", "backup", primary: true); export.IsEnabled = !_busy;
        System.Windows.Automation.AutomationProperties.SetAutomationId(export, "ExportUserData");
        var import = Button("Importa archivio dati", async (_, _) => await ImportUserDataAsync());
        SmallAction(import, "Importa archivio dati", "folder"); import.IsEnabled = !_busy;
        System.Windows.Automation.AutomationProperties.SetAutomationId(import, "ImportUserData");
        var actions = EqualActions(export, import); actions.Margin = new Thickness(0, 0, 0, 12); _content.Children.Add(actions);
        var migrate = Button("Importa dati della copia portatile", async (_, _) => await MigratePortableDataAsync());
        SmallAction(migrate, "Importa dati della copia portatile", "folder"); migrate.IsEnabled = !_busy;
        System.Windows.Automation.AutomationProperties.SetAutomationId(migrate, "MigratePortableData");
        var configure = Button("Scegli copia esterna", async (_, _) => await ConfigureRecoveryAsync());
        SmallAction(configure, "Scegli copia esterna", "backup"); configure.IsEnabled = !_busy;
        System.Windows.Automation.AutomationProperties.SetAutomationId(configure, "ConfigureExternalRecovery");
        var second = EqualActions(migrate, configure); second.Margin = new Thickness(0, 0, 0, 16); _content.Children.Add(second);
        var note = Text(_dataTransferMessage + "\n\nImportazione senza sovrascrittura: dati identici vengono mantenuti, dati diversi bloccano l'operazione. Le copie originali non vengono eliminate. Gli archivi possono contenere percorsi personali e copie di EXE: conservarli privatamente.", 12, Muted);
        note.TextWrapping = TextWrapping.Wrap; note.Margin = new Thickness(0, 0, 0, 28); _content.Children.Add(note);
    }

    private bool ChooseRecoveryDestination()
    {
        var dialog = new SaveFileDialog { Title = "Scegli la copia ZIP esterna, conservata anche dopo la disinstallazione",
            Filter = "Archivio dati GamePanel (*.zip)|*.zip", DefaultExt = ".zip", AddExtension = true,
            FileName = "GamePanel-recupero.zip", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), OverwritePrompt = true };
        if (dialog.ShowDialog(this) != true) return false;
        try
        {
            // Keep a previously selected archive until a fresh verified export atomically replaces it.
            UserDataProtection.Configure(dialog.FileName);
            _dataTransferMessage = "Posizione esterna impostata. Lo ZIP viene sostituito solo dopo aver verificato la nuova copia.";
            return true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { MessageBox.Show(this, e.Message, "GamePanel · copia esterna"); return false; }
    }

    private bool EnsureRecoveryDestination()
    {
        if (ReadRecoveryDestination() is not null) return true;
        if (!AppDistribution.HasPackageIdentity) return true;
        MessageBox.Show(this, "Prima del primo backup Store scegli uno ZIP esterno, per esempio in Documenti. Verrà aggiornato dopo ogni backup e potrà essere importato dopo una reinstallazione. Se annulli, il nuovo backup non viene creato.", "GamePanel · proteggi i backup");
        return ChooseRecoveryDestination();
    }

    private string PreserveData(CancellationToken token, IProgress<BackupProgress> progress)
    {
        _externalCopyFailed = false;
        if (ReadRecoveryDestination() is null)
        {
            _externalCopyFailed = AppDistribution.HasPackageIdentity;
            return _externalCopyFailed ? "ATTENZIONE: copia esterna non configurata." : "";
        }
        try
        {
            var result = UserDataProtection.Preserve(token, progress);
            return $"Copia esterna aggiornata e verificata: {result.Files} file.";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or OperationCanceledException)
        { _externalCopyFailed = true; return "ATTENZIONE: dati conservati internamente, ma copia esterna NON aggiornata. Prima di disinstallare esporta i dati. " + e.Message; }
    }


    private BackupManifest CreateProtectedBackup(GameReport report, CancellationToken token, IProgress<BackupProgress> progress)
    {
        var snapshot = _backups.Create(report, token, progress);
        _dataTransferMessage = PreserveData(token, progress);
        if (_externalCopyFailed)
            throw new InvalidOperationException("Il backup interno è conservato, ma la copia esterna non è aggiornata. L'operazione è stata interrotta: controlla la destinazione prima di proseguire. " + _dataTransferMessage);
        return snapshot;
    }

    private async Task ConfigureRecoveryAsync()
    {
        if (_busy || !ChooseRecoveryDestination()) return;
        await RunBackupTaskAsync((token, progress) => PreserveData(token, progress),
            message => _dataTransferMessage = message, "Verifica della copia esterna");
    }

    private async Task ExportUserDataAsync()
    {
        if (_busy) return;
        var dialog = new SaveFileDialog { Title = "Esporta libreria, preferenze e backup fuori dall'app",
            Filter = "Archivio dati GamePanel (*.zip)|*.zip", DefaultExt = ".zip", AddExtension = true,
            FileName = "GamePanel-dati-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip", OverwritePrompt = true };
        if (dialog.ShowDialog(this) != true) return;
        try { UserDataArchive.ValidateExternalPath(GamePanelPaths.Root, dialog.FileName, durable: true); }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException) { MessageBox.Show(this, e.Message, "GamePanel · esportazione"); return; }
        await RunBackupTaskAsync((token, progress) => UserDataArchive.Export(GamePanelPaths.Root, dialog.FileName, true, token, progress),
            result => _dataTransferMessage = $"Archivio esterno verificato: {result.Files} file · {result.Bytes / (1024d * 1024):F1} MiB. Puoi conservarlo per una reinstallazione.\n" + dialog.FileName,
            "Esportazione dei dati di GamePanel");
    }

    private async Task ImportUserDataAsync()
    {
        if (_busy) return;
        var dialog = new OpenFileDialog { Title = "Importa un archivio dati GamePanel", Filter = "Archivio dati GamePanel (*.zip)|*.zip", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        if (MessageBox.Show(this, "Importare libreria e backup da questo archivio? I file esistenti diversi non verranno sovrascritti. L'operazione non avvia giochi né modifica le loro cartelle. Importa solo archivi di cui conosci la provenienza.", "GamePanel · importa dati", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await RunBackupTaskAsync((token, progress) =>
        {
            var result = UserDataArchive.Import(dialog.FileName, GamePanelPaths.Root, token, progress);
            return (Result: result, Protection: PreserveData(token, progress));
        }, result => FinishUserDataImport(result.Result, result.Protection), "Verifica e importazione dati");
    }

    private async Task MigratePortableDataAsync()
    {
        if (_busy) return;
        var dialog = new OpenFolderDialog { Title = "Seleziona la cartella dati della copia portatile (contiene Library e Backups)",
            InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GamePanel"), Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        if (MessageBox.Show(this, "Chiudi l'altra copia di GamePanel. Importeremo i dati senza cancellare la sorgente e senza sovrascrivere file diversi. Se hai già una libreria differente, l'importazione si fermerà per conservarla.", "GamePanel · copia portatile", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await RunBackupTaskAsync((token, progress) =>
        {
            var result = UserDataArchive.ImportDirectory(dialog.FolderName, GamePanelPaths.Root, token, progress);
            return (Result: result, Protection: PreserveData(token, progress));
        }, result => FinishUserDataImport(result.Result, result.Protection), "Importazione dalla copia portatile");
    }

    private void FinishUserDataImport(UserDataTransferResult result, string protection)
    {
        _report = null; _path.Text = ""; _snapshots = []; _backupForPath = null; _selectedBackup = null;
        LoadLibrary(); _dataTransferMessage = $"Importazione verificata: {result.Files} file. I percorsi dei giochi sono quelli originali: ricollegali dalla libreria se sono cambiati. " + protection;
    }

    private void InitializeDataProtection()
    {
        Closing += async (_, e) =>
        {
            _checkUpdatesCancellation?.Cancel();
            if (_exitAfterProtection)
            {
                _closing = true; _cancellation?.Cancel(); _checkUpdatesCancellation?.Cancel(); return;
            }
            if (_busy)
            {
                e.Cancel = true;
                MessageBox.Show(this, "Attendi il termine dell'operazione oppure interrompila prima di chiudere.", "GamePanel · operazione in corso");
                return;
            }
            if (ReadRecoveryDestination() is null)
            {
                if (_dataProtectionReadFailed && MessageBox.Show(this, _dataTransferMessage + "\n\nLa copia esterna non può essere aggiornata. Chiudere comunque? Non disinstallare prima di un'esportazione valida.", "GamePanel · copia esterna", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                { e.Cancel = true; return; }
                _closing = true; _checkUpdatesCancellation?.Cancel(); return;
            }
            e.Cancel = true;
            await RunBackupTaskAsync((token, progress) => PreserveData(token, progress), message =>
            {
                _dataTransferMessage = message;
                if (message.StartsWith("ATTENZIONE", StringComparison.Ordinal))
                {
                    if (MessageBox.Show(this, message + "\n\nChiudere comunque? I dati interni restano conservati finché non disinstalli o reimposti l'app.", "GamePanel · copia esterna", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                }
                _exitAfterProtection = true;
            }, "Aggiornamento della copia esterna prima di chiudere");
            if (_exitAfterProtection) _ = Dispatcher.BeginInvoke(new Action(Close));
        };
    }
    private bool _exitAfterProtection;
}

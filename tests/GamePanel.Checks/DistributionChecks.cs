using GamePanel.Core;

internal static class DistributionChecks
{
    public static void Register(Action<string, Action> check, string root)
    {
        check("MSIX: dati isolati dal portatile e dal percorso installato", () =>
        {
            var local = Path.GetFullPath(Path.Combine(root, "distribution-data"));
            var portable = AppDistribution.ResolveDataRoot(local, null);
            var packaged = AppDistribution.ResolveDataRoot(local, "GamePanel.Compatibility_cw5n1h2txyewy");
            if (portable != Path.Combine(local, "GamePanel") || packaged == portable
                || !packaged.EndsWith(Path.Combine("LocalState", "GamePanel"), StringComparison.Ordinal))
                throw new Exception("Percorsi dei dati non separati.");
            foreach (var family in new[] { "", "..", "../escape", "x\\escape" })
            {
                try { AppDistribution.ResolveDataRoot(local, family); }
                catch (InvalidOperationException) { continue; }
                throw new Exception("Identità usata per uscire dalla cartella del pacchetto.");
            }
        });
        check("MSIX: override diagnostico dei dati conservato", () =>
        {
            var previous = Environment.GetEnvironmentVariable("GAMEPANEL_DATA_ROOT");
            var isolated = Path.GetFullPath(Path.Combine(root, "distribution-override"));
            try
            {
                Environment.SetEnvironmentVariable("GAMEPANEL_DATA_ROOT", isolated);
                if (GamePanelPaths.Root != isolated) throw new Exception("Override diagnostico ignorato.");
                Environment.SetEnvironmentVariable("GAMEPANEL_DATA_ROOT", "relative-data");
                try { _ = GamePanelPaths.Root; }
                catch (InvalidOperationException) { return; }
                throw new Exception("Override relativo accettato.");
            }
            finally { Environment.SetEnvironmentVariable("GAMEPANEL_DATA_ROOT", previous); }
        });
    }
}

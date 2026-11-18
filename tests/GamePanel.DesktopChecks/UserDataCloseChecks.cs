using System.IO;
using System.Reflection;
using System.Windows;
using GamePanel.Core;

internal static class UserDataCloseChecks
{
    public static int Run()
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true")
        { Console.Error.WriteLine("This proof uses disposable CI directories only."); return 1; }
        var data = Path.Combine(Path.GetTempPath(), "GamePanel-CloseData-" + Guid.NewGuid().ToString("N"));
        var external = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "GamePanel-CloseArchive-" + Guid.NewGuid().ToString("N"));
        var game = Path.Combine(Path.GetTempPath(), "GamePanel-ProfileFixture-" + Guid.NewGuid().ToString("N"));
        var old = Environment.GetEnvironmentVariable("GAMEPANEL_DATA_ROOT");
        Exception? failure = null;
        var archive = Path.Combine(external, "recovery.zip");
        try
        {
            Directory.CreateDirectory(data); Directory.CreateDirectory(external); Directory.CreateDirectory(game);
            var executable = Path.Combine(game, "game.exe");
            File.Copy(Environment.ProcessPath ?? throw new InvalidOperationException("Missing test host."), executable);
            var report = new GameInspector().Inspect(executable);
            Environment.SetEnvironmentVariable("GAMEPANEL_DATA_ROOT", data);
            File.WriteAllText(Path.Combine(data, "settings.json"), "{\"fixture\":true}");
            UserDataProtection.Configure(archive);
            var thread = new Thread(() =>
            {
                try
                {
                    var app = new Application();
                    var type = Assembly.Load("GamePanel").GetType("GamePanel.Desktop.MainWindow", true)!;
                    var window = (Window)Activator.CreateInstance(type)!;
                    window.Loaded += async (_, _) =>
                    {
                        try
                        {
                            var completed = false;
                            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                            var backup = type.GetMethod("CreateProtectedBackup", flags)!;
                            Func<CancellationToken, IProgress<BackupProgress>, Task<int>> work = (token, progress) =>
                            {
                                backup.Invoke(window, [report, token, progress]);
                                File.WriteAllText(Path.Combine(data, "profile-result.json"), "{\"applied\":true}");
                                return Task.FromResult(1);
                            };
                            var runner = type.GetMethod("RunFxTaskAsync", flags)!.MakeGenericMethod(typeof(int));
                            await ((Task)runner.Invoke(window, [work, new Action<int>(_ => completed = true), "CI profile data preservation", true])!);
                            if (!completed || !File.Exists(archive)) throw new InvalidOperationException("Profile operation did not preserve its backup.");
                            File.WriteAllText(Path.Combine(data, "close-marker.txt"), "closed-after-profile");
                            window.Close();
                        }
                        catch (Exception e)
                        {
                            failure = e;
                            type.GetField("_exitAfterProtection", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
                            window.Close();
                        }
                    };
                    app.Run(window);
                }
                catch (Exception e) { failure = e; }
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(60))) throw new InvalidOperationException("WPF close/export did not finish.");
            if (failure is not null) throw new InvalidOperationException("Real WPF close failed.", failure);
            if (!File.Exists(archive) || UserDataArchive.Verify(archive).Files < 1) throw new InvalidOperationException("Closing the app did not create the verified external archive.");
            var recovered = Path.Combine(Path.GetTempPath(), "GamePanel-CloseRecovered-" + Guid.NewGuid().ToString("N"));
            try
            {
                UserDataArchive.Import(archive, recovered);
                if (File.ReadAllText(Path.Combine(recovered, "settings.json")) != "{\"fixture\":true}"
                    || !File.Exists(Path.Combine(recovered, "profile-result.json"))
                    || File.ReadAllText(Path.Combine(recovered, "close-marker.txt")) != "closed-after-profile"
                    || new BackupService(Path.Combine(recovered, "Backups"), _ => false).List(executable).Count != 1)
                    throw new InvalidOperationException("Data archived during closing was not recoverable.");
            }
            finally { if (Directory.Exists(recovered)) Directory.Delete(recovered, true); }
            Console.WriteLine("PASS actual WPF close: asynchronous archive completed, app exited and external data recovered.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally
        {
            Environment.SetEnvironmentVariable("GAMEPANEL_DATA_ROOT", old);
            if (Directory.Exists(data)) Directory.Delete(data, true);
            if (Directory.Exists(external)) Directory.Delete(external, true);
            if (Directory.Exists(game)) Directory.Delete(game, true);
        }
    }
}

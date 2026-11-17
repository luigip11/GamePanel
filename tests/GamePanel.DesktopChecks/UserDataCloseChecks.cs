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
        var old = Environment.GetEnvironmentVariable("GAMEPANEL_DATA_ROOT");
        Exception? failure = null;
        var archive = Path.Combine(external, "recovery.zip");
        try
        {
            Directory.CreateDirectory(data); Directory.CreateDirectory(external);
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
                    window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(new Action(window.Close));
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
                if (File.ReadAllText(Path.Combine(recovered, "settings.json")) != "{\"fixture\":true}")
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
        }
    }
}

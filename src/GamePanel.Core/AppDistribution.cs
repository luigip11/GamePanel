using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace GamePanel.Core;

/// <summary>Reads Windows package identity without a WinRT dependency or changing system policy.</summary>
public static class AppDistribution
{
    private static readonly Lazy<string?> Family = new(ReadPackageFamilyName);
    public static string? PackageFamilyName => Family.Value;
    public static bool HasPackageIdentity => PackageFamilyName is not null;

    public static string ResolveDataRoot(string localApplicationData, string? packageFamilyName)
    {
        if (!Path.IsPathFullyQualified(localApplicationData))
            throw new InvalidOperationException("Cartella dati utente non disponibile.");
        if (packageFamilyName is null) return Path.Combine(localApplicationData, "GamePanel");
        if (string.IsNullOrWhiteSpace(packageFamilyName) || packageFamilyName is "." or ".."
            || packageFamilyName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || packageFamilyName.Contains('/') || packageFamilyName.Contains('\\'))
            throw new InvalidOperationException("Identità del pacchetto Windows non valida.");
        return Path.Combine(localApplicationData, "Packages", packageFamilyName, "LocalState", "GamePanel");
    }

    private static string? ReadPackageFamilyName()
    {
        if (!OperatingSystem.IsWindows()) return null;
        const int noPackage = 15700, insufficientBuffer = 122;
        uint length = 0;
        var result = GetCurrentPackageFamilyName(ref length, null);
        if (result == noPackage) return null;
        if (result != insufficientBuffer) throw new Win32Exception(result, "Identità del pacchetto Windows non leggibile.");
        if (length is 0 or > 1024) throw new InvalidOperationException("Identità del pacchetto Windows oltre il limite.");
        var family = new StringBuilder(checked((int)length));
        result = GetCurrentPackageFamilyName(ref length, family);
        if (result != 0) throw new Win32Exception(result, "Identità del pacchetto Windows non leggibile.");
        return family.ToString();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetCurrentPackageFamilyName(ref uint packageFamilyNameLength, StringBuilder? packageFamilyName);
}

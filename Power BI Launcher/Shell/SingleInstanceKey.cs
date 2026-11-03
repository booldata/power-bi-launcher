using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PowerBILauncher;

/// <summary>
/// UI-free <c>AppInstance</c> key: one running launcher per copy (exe folder), because each
/// copy owns its own <c>settings.json</c> / <c>profiles.json</c> beside the exe.
/// </summary>
public static class SingleInstanceKey
{
    private const string Prefix = "booldata.PowerBILauncher.";

    public static string ForBaseDirectory(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        string normalized = Path.GetFullPath(baseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Prefix + Convert.ToHexString(hash, 0, 8);
    }
}

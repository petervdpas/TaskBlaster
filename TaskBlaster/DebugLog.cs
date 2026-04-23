using System;
using System.IO;

namespace TaskBlaster;

/// <summary>
/// Last-resort file logger used when the UI thread may be frozen
/// (terminal updates don't flush in that state). Writes to
/// ~/.taskblaster/debug.log; safe to call from anywhere.
/// </summary>
internal static class DebugLog
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".taskblaster", "debug.log");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss.fff}  {message}\n");
        }
        catch
        {
            // swallow — diagnostics must not crash the app further
        }
    }

    public static void Clear()
    {
        try { if (File.Exists(Path)) File.Delete(Path); } catch { }
    }
}

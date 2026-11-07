using System.Diagnostics;
using NativeFileDialogSharp;

namespace RestblCalculator.Gui.UI;

public static class NativeDialogs
{
    public static Task<string?> PickFolder(string? initial = null)
        => Task.Run(() => OperatingSystem.IsMacOS() ? PickFolderMac(initial) : PickFolderNfd(initial));

    public static void Reveal(string file)
    {
        ProcessStartInfo info = new() { UseShellExecute = false };
        if (OperatingSystem.IsWindows())
        {
            info.FileName = "explorer.exe";
            info.Arguments = $"/select,\"{file}\"";
        }
        else if (OperatingSystem.IsMacOS())
        {
            info.FileName = "open";
            info.ArgumentList.Add("-R");
            info.ArgumentList.Add(file);
        }
        else
        {
            info.FileName = "xdg-open";
            info.ArgumentList.Add(Path.GetDirectoryName(file)!);
        }

        using Process? process = Process.Start(info);
    }

    private static string? PickFolderNfd(string? initial)
        => Dialog.FolderPicker(Directory.Exists(initial) ? initial : null) is { IsOk: true } r ? r.Path : null;

    private static string? PickFolderMac(string? initial)
    {
        string from = Directory.Exists(initial) ? $" default location POSIX file \"{initial.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"" : "";
        ProcessStartInfo info = new("osascript") { UseShellExecute = false, RedirectStandardOutput = true };
        info.ArgumentList.Add("-e");
        info.ArgumentList.Add($"POSIX path of (choose folder{from})");

        using Process process = Process.Start(info)!;
        string path = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return process.ExitCode == 0 && path.Length > 0 ? path : null;
    }
}

using NativeFileDialogSharp;

namespace RestblTool.Gui.UI;

public static class NativeDialogs
{
    public static Task<string?> PickFolder(string? initial = null)
        => Task.Run(() => Dialog.FolderPicker(Directory.Exists(initial) ? initial : null) is { IsOk: true } r ? r.Path : null);

    public static Task<string?> OpenFile(string filter, string? initial = null)
        => Task.Run(() => Dialog.FileOpen(filter, Directory.Exists(initial) ? initial : null) is { IsOk: true } r ? r.Path : null);

    public static Task<string?> SaveFile(string filter, string? initial = null)
        => Task.Run(() => Dialog.FileSave(filter, initial) is { IsOk: true } r ? r.Path : null);
}

namespace RestblTool.Gui.UI;

public static class Icon
{
    public static readonly string FolderOpen = Pick("", "");
    public static readonly string Search = Pick("", "");
    public static readonly string Close = Pick("", "");
    public static readonly string Warning = Pick("", "");
    public static readonly string Info = Pick("", "");
    public static readonly string Check = Pick("", "");

    public static readonly string[] All = [FolderOpen, Search, Close, Warning, Info, Check];

    private static string Pick(string segoe, string awesome) => Fonts.WindowsFaces ? segoe : awesome;
}

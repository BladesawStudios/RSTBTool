using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using Silk.NET.Input;

namespace RestblTool.Gui.UI;

public static unsafe class Platform
{
    private static IKeyboard? _keyboard;
    private static IntPtr _clipboard;

    public static void Hook(IKeyboard keyboard)
    {
        _keyboard = keyboard;
        ImGuiIOPtr io = ImGui.GetIO();
        io.GetClipboardTextFn = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)&GetClipboard;
        io.SetClipboardTextFn = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void>)&SetClipboard;

        keyboard.KeyDown += (k, _, _) => Modifiers(k);
        keyboard.KeyUp += (k, _, _) => Modifiers(k);
    }

    private static void Modifiers(IKeyboard k)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.AddKeyEvent(ImGuiKey.ModCtrl, k.IsKeyPressed(Key.ControlLeft) || k.IsKeyPressed(Key.ControlRight));
        io.AddKeyEvent(ImGuiKey.ModShift, k.IsKeyPressed(Key.ShiftLeft) || k.IsKeyPressed(Key.ShiftRight));
        io.AddKeyEvent(ImGuiKey.ModAlt, k.IsKeyPressed(Key.AltLeft) || k.IsKeyPressed(Key.AltRight));
        io.AddKeyEvent(ImGuiKey.ModSuper, k.IsKeyPressed(Key.SuperLeft) || k.IsKeyPressed(Key.SuperRight));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static IntPtr GetClipboard(IntPtr userData)
    {
        Marshal.FreeCoTaskMem(_clipboard);
        _clipboard = Marshal.StringToCoTaskMemUTF8(_keyboard?.ClipboardText ?? "");
        return _clipboard;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void SetClipboard(IntPtr userData, IntPtr text)
    {
        if (_keyboard is not null) _keyboard.ClipboardText = Marshal.PtrToStringUTF8(text) ?? "";
    }
}

using ImGuiNET;
using RestblTool.Gui;
using RestblTool.Gui.UI;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

WindowOptions options = WindowOptions.Default with
{
    Size = new Vector2D<int>(1100, 760),
    Title = "RESTBL Generator",
    VSync = true,
};

using IWindow window = Window.Create(options);
GL? gl = null;
IInputContext? input = null;
ImGuiController? controller = null;
App? app = null;

window.Load += () =>
{
    gl = window.CreateOpenGL();
    input = window.CreateInput();
    controller = new ImGuiController(gl, window, input, null, () =>
    {
        Fonts.Load(1f);
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
    });
    unsafe { ImGui.GetIO().NativePtr->IniFilename = null; }
    Platform.Hook(input.Keyboards[0]);
    Theme.Apply(1f);
    app = new App(args);
};

window.FramebufferResize += size => gl?.Viewport(size);

window.FileDrop += paths => app?.Drop(paths);

window.Render += delta =>
{
    controller!.Update((float)delta);
    gl!.ClearColor(Theme.Panel.X, Theme.Panel.Y, Theme.Panel.Z, 1f);
    gl.Clear(ClearBufferMask.ColorBufferBit);
    ImGui.PushFont(Fonts.Body);
    app!.Draw();
    ImGui.PopFont();
    controller.Render();
};

window.Closing += () =>
{
    app?.Dispose();
    controller?.Dispose();
    input?.Dispose();
    gl?.Dispose();
};

window.Run();

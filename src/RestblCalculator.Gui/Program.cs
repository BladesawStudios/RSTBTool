using ImGuiNET;
using RestblCalculator.Gui;
using RestblCalculator.Gui.UI;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

WindowOptions options = WindowOptions.Default with
{
    Size = new Vector2D<int>(1100, 760),
    Title = "RESTBL Calculator",
    VSync = true,
};

using IWindow window = Window.Create(options);
GL? gl = null;
IInputContext? input = null;
ImGuiController? controller = null;
App? app = null;
float builtScale = 0;

void BuildUi(float scale)
{
    controller?.Dispose();
    controller = new ImGuiController(gl!, window, input!, null, () =>
    {
        Fonts.Load(scale);
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
    });
    unsafe { ImGui.GetIO().NativePtr->IniFilename = null; }
    Platform.Attach();
    Theme.Apply(scale);
    builtScale = scale;
}

window.Load += () =>
{
    WindowIcon.Apply(window);
    gl = window.CreateOpenGL();
    input = window.CreateInput();
    app = new App(args);
    float scale = app.UiScale ?? Scaling.Auto(window);
    Scaling.FitWindow(window, scale);
    Platform.Hook(input.Keyboards[0]);
    BuildUi(scale);
};

window.FramebufferResize += size => gl?.Viewport(size);

window.FileDrop += paths => app?.Drop(paths);

window.Render += delta =>
{
    float wanted = app!.UiScale ?? Scaling.Auto(window);
    if (MathF.Abs(wanted - builtScale) > 0.01f) BuildUi(wanted);

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

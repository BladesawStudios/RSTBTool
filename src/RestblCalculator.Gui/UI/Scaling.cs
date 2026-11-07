using Silk.NET.GLFW;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace RestblCalculator.Gui.UI;

public static class Scaling
{
    private const float Comfortable = 1.25f;
    private const int BaseWidth = 1100;
    private const int BaseHeight = 760;

    public static float Auto(IWindow window) => Comfortable * MonitorScale(window);

    public static unsafe float MonitorScale(IWindow window)
    {
        if (window.FramebufferSize.X != window.Size.X) return 1f;

        if (!Glfw.GetApi().Context.TryGetProcAddress("glfwGetWindowContentScale", out nint function)) return 1f;

        float x = 0, y = 0;
        ((delegate* unmanaged[Cdecl]<nint, float*, float*, void>)function)(window.Handle, &x, &y);
        return x > 0 ? x : 1f;
    }

    public static void FitWindow(IWindow window, float scale)
    {
        Vector2D<int> screen = window.Monitor?.Bounds.Size ?? new Vector2D<int>(int.MaxValue / 2, int.MaxValue / 2);
        window.Size = new Vector2D<int>(Math.Min((int)(BaseWidth * scale), screen.X - 40), Math.Min((int)(BaseHeight * scale), screen.Y - 90));

        try
        {
            window.Center();
        }
        catch (GlfwException)
        {
        }
    }
}

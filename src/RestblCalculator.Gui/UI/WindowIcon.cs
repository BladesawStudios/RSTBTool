using Silk.NET.Core;
using Silk.NET.GLFW;
using Silk.NET.Windowing;
using StbImageSharp;

namespace RestblCalculator.Gui.UI;

public static class WindowIcon
{
    public static void Apply(IWindow window)
    {
        if (OperatingSystem.IsMacOS()) return;

        using Stream stream = typeof(WindowIcon).Assembly.GetManifestResourceStream("icon.png")!;
        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        RawImage[] icons = [.. new[] { 16, 32, 48, 256 }.Select(size => new RawImage(size, size, Shrink(image, size)))];

        try
        {
            window.SetWindowIcon(icons);
        }
        catch (GlfwException)
        {
        }
    }

    private static byte[] Shrink(ImageResult image, int size)
    {
        byte[] output = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                long r = 0, g = 0, b = 0, a = 0, count = 0;
                for (int sy = y * image.Height / size; sy < Math.Max((y + 1) * image.Height / size, y * image.Height / size + 1); sy++)
                {
                    for (int sx = x * image.Width / size; sx < Math.Max((x + 1) * image.Width / size, x * image.Width / size + 1); sx++)
                    {
                        int at = (sy * image.Width + sx) * 4;
                        int alpha = image.Data[at + 3];
                        r += image.Data[at] * alpha;
                        g += image.Data[at + 1] * alpha;
                        b += image.Data[at + 2] * alpha;
                        a += alpha;
                        count++;
                    }
                }

                int to = (y * size + x) * 4;
                if (a > 0)
                {
                    output[to] = (byte)(r / a);
                    output[to + 1] = (byte)(g / a);
                    output[to + 2] = (byte)(b / a);
                }

                output[to + 3] = (byte)(a / count);
            }
        }

        return output;
    }
}

using Artificer.Utils;
using System.Collections.Generic;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Artificer.UIStudio;

internal static class StudioApp
{
    public static void Run(IReadOnlyList<IStory> stories)
    {
        var opts = WindowOptions.Default with
        {
            Size  = new(1400, 900),
            Title = "Artificer UI Studio",
        };

        using var window = Window.Create(opts);

        GL? gl = null;
        ImGuiController? controller = null;
        IInputContext? input = null;
        StudioWindow? studioWindow = null;

        window.Load += () =>
        {
            gl    = GL.GetApi(window);
            input = window.CreateInput();
            UiServices.Current = new StubUiServices();
            controller  = new ImGuiController(gl, window, input);
            studioWindow = new StudioWindow(stories);
        };

        window.Update += dt =>
        {
            controller!.Update((float)dt);
            studioWindow!.Draw();
        };

        window.Render += _ =>
        {
            gl!.ClearColor(0.06f, 0.08f, 0.13f, 1f);
            gl.Clear(ClearBufferMask.ColorBufferBit);
            controller!.Render();
        };

        window.Closing += () =>
        {
            controller?.Dispose();
            input?.Dispose();
        };

        window.Run();
    }
}

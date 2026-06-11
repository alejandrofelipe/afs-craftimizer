using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Artificer.UIStudio;

internal sealed unsafe class ImGuiController : IDisposable
{
    private readonly GL _gl;
    private readonly IView _view;

    private uint _vao, _vbo, _ebo;
    private uint _shader;
    private uint _fontTexture;
    private int _uniformTex, _uniformProjMtx;
    private uint _attribPos, _attribUv, _attribColor;

    private IKeyboard? _kb;
    private IMouse? _mouse;

    public ImGuiController(GL gl, IView view, IInputContext input)
    {
        _gl = gl;
        _view = view;

        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io.ConfigFlags  |= ImGuiConfigFlags.NavEnableKeyboard;

        CreateDeviceObjects();
        CreateFontTexture();
        BindInput(input);
    }

    // ── input ─────────────────────────────────────────────────────────────────

    private void BindInput(IInputContext input)
    {
        _kb    = input.Keyboards.Count > 0 ? input.Keyboards[0] : null;
        _mouse = input.Mice.Count > 0      ? input.Mice[0]      : null;

        if (_kb != null)
        {
            _kb.KeyDown += (_, key, _) => ImGui.GetIO().AddKeyEvent(ToImGuiKey(key), true);
            _kb.KeyUp   += (_, key, _) => ImGui.GetIO().AddKeyEvent(ToImGuiKey(key), false);
            _kb.KeyChar += (_, c)      => ImGui.GetIO().AddInputCharacter(c);
        }
        if (_mouse != null)
        {
            _mouse.MouseMove += (_, pos)   => ImGui.GetIO().AddMousePosEvent(pos.X, pos.Y);
            _mouse.MouseDown += (_, btn)   => ImGui.GetIO().AddMouseButtonEvent((int)btn, true);
            _mouse.MouseUp   += (_, btn)   => ImGui.GetIO().AddMouseButtonEvent((int)btn, false);
            _mouse.Scroll    += (_, wheel) => ImGui.GetIO().AddMouseWheelEvent(wheel.X, wheel.Y);
        }
    }

    private void SetPerFrameData(float dt)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(_view.Size.X, _view.Size.Y);
        if (_view.Size.X > 0 && _view.Size.Y > 0)
            io.DisplayFramebufferScale = new Vector2(
                _view.FramebufferSize.X / (float)_view.Size.X,
                _view.FramebufferSize.Y / (float)_view.Size.Y);
        io.DeltaTime = dt;
    }

    public void Update(float dt)
    {
        SetPerFrameData(dt);
        ImGui.NewFrame();
    }

    public void Render()
    {
        ImGui.Render();
        RenderDrawData(ImGui.GetDrawData());
    }

    // ── device objects ────────────────────────────────────────────────────────

    private void CreateDeviceObjects()
    {
        const string vert = @"
#version 330 core
layout(location=0) in vec2 Position;
layout(location=1) in vec2 UV;
layout(location=2) in vec4 Color;
uniform mat4 ProjMtx;
out vec2 Frag_UV;
out vec4 Frag_Color;
void main() {
    Frag_UV = UV;
    Frag_Color = Color;
    gl_Position = ProjMtx * vec4(Position.xy, 0, 1);
}";
        const string frag = @"
#version 330 core
in vec2 Frag_UV;
in vec4 Frag_Color;
uniform sampler2D Texture;
out vec4 Out_Color;
void main() { Out_Color = Frag_Color * texture(Texture, Frag_UV.st); }";

        var vs = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vs, vert);
        _gl.CompileShader(vs);

        var fs = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fs, frag);
        _gl.CompileShader(fs);

        _shader = _gl.CreateProgram();
        _gl.AttachShader(_shader, vs);
        _gl.AttachShader(_shader, fs);
        _gl.LinkProgram(_shader);
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);

        _uniformTex     = _gl.GetUniformLocation(_shader, "Texture");
        _uniformProjMtx = _gl.GetUniformLocation(_shader, "ProjMtx");
        _attribPos      = (uint)_gl.GetAttribLocation(_shader, "Position");
        _attribUv       = (uint)_gl.GetAttribLocation(_shader, "UV");
        _attribColor    = (uint)_gl.GetAttribLocation(_shader, "Color");

        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();
        _vao = _gl.GenVertexArray();

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

        var stride = (uint)sizeof(ImDrawVert);
        _gl.EnableVertexAttribArray(_attribPos);
        _gl.EnableVertexAttribArray(_attribUv);
        _gl.EnableVertexAttribArray(_attribColor);
        _gl.VertexAttribPointer(_attribPos,   2, VertexAttribPointerType.Float,        false, stride, (void*)0);
        _gl.VertexAttribPointer(_attribUv,    2, VertexAttribPointerType.Float,        false, stride, (void*)8);
        _gl.VertexAttribPointer(_attribColor, 4, VertexAttribPointerType.UnsignedByte, true,  stride, (void*)16);

        _gl.BindVertexArray(0);
    }

    private void CreateFontTexture()
    {
        ImGui.GetIO().Fonts.GetTexDataAsRGBA32(out byte* px, out int w, out int h, out _);

        _fontTexture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _fontTexture);
        var linear = (int)GLEnum.Linear;
        _gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, in linear);
        _gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, in linear);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)w, (uint)h, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, px);

        ImGui.GetIO().Fonts.SetTexID((nint)_fontTexture);
        ImGui.GetIO().Fonts.ClearTexData();
    }

    // ── render ────────────────────────────────────────────────────────────────

    private void RenderDrawData(ImDrawDataPtr data)
    {
        if (data.CmdListsCount == 0) return;

        var fbW = (int)(data.DisplaySize.X * data.FramebufferScale.X);
        var fbH = (int)(data.DisplaySize.Y * data.FramebufferScale.Y);
        if (fbW <= 0 || fbH <= 0) return;

        _gl.GetInteger(GetPName.ActiveTexture, out int prevActiveTex);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.GetInteger(GetPName.CurrentProgram, out int prevProg);
        _gl.GetInteger(GetPName.TextureBinding2D, out int prevTex);
        _gl.GetInteger(GetPName.VertexArrayBinding, out int prevVao);
        _gl.GetInteger(GetPName.ArrayBufferBinding, out int prevVbo);
        Span<int> vp = stackalloc int[4];
        _gl.GetInteger(GetPName.Viewport, vp);
        Span<int> sc = stackalloc int[4];
        _gl.GetInteger(GetPName.ScissorBox, sc);
        bool prevBlend       = _gl.IsEnabled(EnableCap.Blend);
        bool prevCullFace    = _gl.IsEnabled(EnableCap.CullFace);
        bool prevDepthTest   = _gl.IsEnabled(EnableCap.DepthTest);
        bool prevScissorTest = _gl.IsEnabled(EnableCap.ScissorTest);

        _gl.Enable(EnableCap.Blend);
        _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        _gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha,
                              BlendingFactor.One,      BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.ScissorTest);
        _gl.Viewport(0, 0, (uint)fbW, (uint)fbH);

        float l = data.DisplayPos.X, r = data.DisplayPos.X + data.DisplaySize.X;
        float t = data.DisplayPos.Y, b = data.DisplayPos.Y + data.DisplaySize.Y;
        Span<float> proj = stackalloc float[]
        {
            2/(r-l),     0,           0, 0,
            0,           2/(t-b),     0, 0,
            0,           0,          -1, 0,
            (r+l)/(l-r), (t+b)/(b-t), 0, 1,
        };

        _gl.UseProgram(_shader);
        _gl.Uniform1(_uniformTex, 0);
        _gl.UniformMatrix4(_uniformProjMtx, 1, false, proj);
        _gl.BindVertexArray(_vao);

        var clipOff   = data.DisplayPos;
        var clipScale = data.FramebufferScale;

        for (int n = 0; n < data.CmdListsCount; n++)
        {
            var cmdList = data.CmdLists[n];

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(cmdList.VtxBuffer.Size * sizeof(ImDrawVert)),
                (void*)cmdList.VtxBuffer.Data, BufferUsageARB.StreamDraw);

            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(cmdList.IdxBuffer.Size * sizeof(ushort)),
                (void*)cmdList.IdxBuffer.Data, BufferUsageARB.StreamDraw);

            for (int i = 0; i < cmdList.CmdBuffer.Size; i++)
            {
                var cmd = cmdList.CmdBuffer[i];
                if (cmd.UserCallback != IntPtr.Zero) continue;

                var cMin = new Vector2(
                    (cmd.ClipRect.X - clipOff.X) * clipScale.X,
                    (cmd.ClipRect.Y - clipOff.Y) * clipScale.Y);
                var cMax = new Vector2(
                    (cmd.ClipRect.Z - clipOff.X) * clipScale.X,
                    (cmd.ClipRect.W - clipOff.Y) * clipScale.Y);
                if (cMin.X >= fbW || cMin.Y >= fbH || cMax.X <= 0 || cMax.Y <= 0) continue;

                _gl.Scissor(
                    (int)cMin.X, fbH - (int)cMax.Y,
                    (uint)(cMax.X - cMin.X), (uint)(cMax.Y - cMin.Y));

                _gl.BindTexture(TextureTarget.Texture2D, (uint)(long)cmd.TextureId);
                _gl.DrawElementsBaseVertex(PrimitiveType.Triangles,
                    cmd.ElemCount, DrawElementsType.UnsignedShort,
                    (void*)(cmd.IdxOffset * sizeof(ushort)), (int)cmd.VtxOffset);
            }
        }

        _gl.UseProgram((uint)prevProg);
        _gl.BindTexture(TextureTarget.Texture2D, (uint)prevTex);
        _gl.ActiveTexture((TextureUnit)prevActiveTex);
        _gl.BindVertexArray((uint)prevVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, (uint)prevVbo);
        if (prevBlend)       _gl.Enable(EnableCap.Blend);       else _gl.Disable(EnableCap.Blend);
        if (prevCullFace)    _gl.Enable(EnableCap.CullFace);    else _gl.Disable(EnableCap.CullFace);
        if (prevDepthTest)   _gl.Enable(EnableCap.DepthTest);   else _gl.Disable(EnableCap.DepthTest);
        if (prevScissorTest) _gl.Enable(EnableCap.ScissorTest); else _gl.Disable(EnableCap.ScissorTest);
        _gl.Viewport(vp[0], vp[1], (uint)vp[2], (uint)vp[3]);
        _gl.Scissor(sc[0], sc[1], (uint)sc[2], (uint)sc[3]);
    }

    // ── key mapping ───────────────────────────────────────────────────────────

    private static ImGuiKey ToImGuiKey(Key key) => key switch
    {
        Key.Tab          => ImGuiKey.Tab,
        Key.Left         => ImGuiKey.LeftArrow,
        Key.Right        => ImGuiKey.RightArrow,
        Key.Up           => ImGuiKey.UpArrow,
        Key.Down         => ImGuiKey.DownArrow,
        Key.PageUp       => ImGuiKey.PageUp,
        Key.PageDown     => ImGuiKey.PageDown,
        Key.Home         => ImGuiKey.Home,
        Key.End          => ImGuiKey.End,
        Key.Insert       => ImGuiKey.Insert,
        Key.Delete       => ImGuiKey.Delete,
        Key.Backspace    => ImGuiKey.Backspace,
        Key.Space        => ImGuiKey.Space,
        Key.Enter        => ImGuiKey.Enter,
        Key.Escape       => ImGuiKey.Escape,
        Key.Apostrophe   => ImGuiKey.Apostrophe,
        Key.Comma        => ImGuiKey.Comma,
        Key.Minus        => ImGuiKey.Minus,
        Key.Period       => ImGuiKey.Period,
        Key.Slash        => ImGuiKey.Slash,
        Key.Semicolon    => ImGuiKey.Semicolon,
        Key.Equal        => ImGuiKey.Equal,
        Key.LeftBracket  => ImGuiKey.LeftBracket,
        Key.BackSlash    => ImGuiKey.Backslash,
        Key.RightBracket => ImGuiKey.RightBracket,
        Key.GraveAccent  => ImGuiKey.GraveAccent,
        Key.CapsLock     => ImGuiKey.CapsLock,
        Key.ScrollLock   => ImGuiKey.ScrollLock,
        Key.NumLock      => ImGuiKey.NumLock,
        Key.PrintScreen  => ImGuiKey.PrintScreen,
        Key.Pause        => ImGuiKey.Pause,
        Key.F1  => ImGuiKey.F1,  Key.F2  => ImGuiKey.F2,  Key.F3  => ImGuiKey.F3,
        Key.F4  => ImGuiKey.F4,  Key.F5  => ImGuiKey.F5,  Key.F6  => ImGuiKey.F6,
        Key.F7  => ImGuiKey.F7,  Key.F8  => ImGuiKey.F8,  Key.F9  => ImGuiKey.F9,
        Key.F10 => ImGuiKey.F10, Key.F11 => ImGuiKey.F11, Key.F12 => ImGuiKey.F12,
        Key.Number0 => ImGuiKey._0, Key.Number1 => ImGuiKey._1,
        Key.Number2 => ImGuiKey._2, Key.Number3 => ImGuiKey._3,
        Key.Number4 => ImGuiKey._4, Key.Number5 => ImGuiKey._5,
        Key.Number6 => ImGuiKey._6, Key.Number7 => ImGuiKey._7,
        Key.Number8 => ImGuiKey._8, Key.Number9 => ImGuiKey._9,
        Key.A => ImGuiKey.A, Key.B => ImGuiKey.B, Key.C => ImGuiKey.C,
        Key.D => ImGuiKey.D, Key.E => ImGuiKey.E, Key.F => ImGuiKey.F,
        Key.G => ImGuiKey.G, Key.H => ImGuiKey.H, Key.I => ImGuiKey.I,
        Key.J => ImGuiKey.J, Key.K => ImGuiKey.K, Key.L => ImGuiKey.L,
        Key.M => ImGuiKey.M, Key.N => ImGuiKey.N, Key.O => ImGuiKey.O,
        Key.P => ImGuiKey.P, Key.Q => ImGuiKey.Q, Key.R => ImGuiKey.R,
        Key.S => ImGuiKey.S, Key.T => ImGuiKey.T, Key.U => ImGuiKey.U,
        Key.V => ImGuiKey.V, Key.W => ImGuiKey.W, Key.X => ImGuiKey.X,
        Key.Y => ImGuiKey.Y, Key.Z => ImGuiKey.Z,
        Key.Keypad0 => ImGuiKey.Keypad0, Key.Keypad1 => ImGuiKey.Keypad1,
        Key.Keypad2 => ImGuiKey.Keypad2, Key.Keypad3 => ImGuiKey.Keypad3,
        Key.Keypad4 => ImGuiKey.Keypad4, Key.Keypad5 => ImGuiKey.Keypad5,
        Key.Keypad6 => ImGuiKey.Keypad6, Key.Keypad7 => ImGuiKey.Keypad7,
        Key.Keypad8 => ImGuiKey.Keypad8, Key.Keypad9 => ImGuiKey.Keypad9,
        Key.KeypadDecimal  => ImGuiKey.KeypadDecimal,
        Key.KeypadDivide   => ImGuiKey.KeypadDivide,
        Key.KeypadMultiply => ImGuiKey.KeypadMultiply,
        Key.KeypadSubtract => ImGuiKey.KeypadSubtract,
        Key.KeypadAdd      => ImGuiKey.KeypadAdd,
        Key.KeypadEnter    => ImGuiKey.KeypadEnter,
        Key.KeypadEqual    => ImGuiKey.KeypadEqual,
        Key.ShiftLeft    => ImGuiKey.LeftShift,  Key.ShiftRight   => ImGuiKey.RightShift,
        Key.ControlLeft  => ImGuiKey.LeftCtrl,   Key.ControlRight => ImGuiKey.RightCtrl,
        Key.AltLeft      => ImGuiKey.LeftAlt,    Key.AltRight     => ImGuiKey.RightAlt,
        Key.SuperLeft    => ImGuiKey.LeftSuper,  Key.SuperRight   => ImGuiKey.RightSuper,
        Key.Menu         => ImGuiKey.Menu,
        _ => ImGuiKey.None,
    };

    // ── cleanup ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _gl.DeleteProgram(_shader);
        _gl.DeleteTexture(_fontTexture);
        ImGui.DestroyContext();
    }
}

using Craftimizer.Utils;
using ImGuiNET;
using System.Numerics;

namespace Craftimizer.UIStudio.Stories;

internal sealed class MacroClipboardStory : IStory
{
    public string Category => "Pages";
    public string Name     => "MacroClipboard";

    private static readonly string[] Quantidades =
    [
        "1 macro", "3 macros",
    ];

    // Texto mock de cada macro (1 string por macro).
    private static readonly string[] Macros =
    [
        "/ac \"Muscular Tempo\" <wait.3>\n" +
        "/ac \"Careful Synthesis\" <wait.3>\n" +
        "/ac \"Careful Synthesis\" <wait.3>\n" +
        "/ac \"Careful Synthesis\" <wait.3>",

        "/ac \"Muscular Tempo\" <wait.3>\n" +
        "/ac \"Prudent Touch\" <wait.3>\n" +
        "/ac \"Prudent Touch\" <wait.3>\n" +
        "/ac \"Careful Synthesis\" <wait.3>",

        "/ac \"Reflect\" <wait.3>\n" +
        "/ac \"Basic Touch\" <wait.3>\n" +
        "/ac \"Standard Touch\" <wait.3>\n" +
        "/ac \"Byregot's Blessing\" <wait.3>",
    ];

    private int _quantidade;

    public void Draw()
    {
        DrawControls();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var count = _quantidade == 0 ? 1 : 3;

        for (var i = 0; i < count; i++)
        {
            using (ImRaii.PushId(i))
            {
                var label = count == 1 ? "Macro" : $"Macro {i + 1}";
                using (ImRaii2.GroupPanel(label, -1, out var inner))
                {
                    ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + inner);
                    ImGui.TextUnformatted(Macros[i]);
                    ImGui.PopTextWrapPos();

                    ImGui.Spacing();

                    var btn = ImGui.GetFrameHeight();
                    ImGuiUtils.AlignRight(btn, inner);
                    ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Paste, btn);
                }

                ImGui.Spacing();
            }
        }
    }

    // ── Controles de estado ───────────────────────────────────────────────────
    private void DrawControls()
    {
        ImGui.SetNextItemWidth(160);
        ImGui.Combo("Quantidade", ref _quantidade, Quantidades, Quantidades.Length);
    }
}

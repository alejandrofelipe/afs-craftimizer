using Artificer.Utils;
using ImGuiNET;

namespace Artificer.UIStudio.Stories;

internal sealed class IconButtonsStory : IStory
{
    public string Category => "Atoms";
    public string Name     => "Icon Buttons";

    public void Draw()
    {
        Section("Enabled");
        ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Edit);
        ImGui.SameLine(0, 8);
        ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Paste);
        ImGui.SameLine(0, 8);
        ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Save);
        ImGui.SameLine(0, 8);
        ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Trash);

        ImGui.Spacing();
        Section("Disabled");
        ImGui.BeginDisabled(true);
        ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Edit);
        ImGui.SameLine(0, 8);
        ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Paste);
        ImGui.SameLine(0, 8);
        ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Save);
        ImGui.SameLine(0, 8);
        ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Trash);
        ImGui.EndDisabled();

        ImGui.Spacing();
        Section("Custom sizes");
        ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Edit, 16f);
        ImGui.SameLine(0, 8);
        ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Edit, 24f);
        ImGui.SameLine(0, 8);
        ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Edit, 32f);
        ImGui.SameLine(0, 8);
        ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Edit, 48f);

        ImGui.Spacing();
        Section("With Tooltip (habilitado)");
        ImGuiUtils.IconButtonWithTooltip(FontAwesomeIcon.Flag,  "Abrir no mapa");
        ImGui.SameLine(0, 8);
        ImGuiUtils.IconButtonWithTooltip(FontAwesomeIcon.Edit,  "Editar");
        ImGui.SameLine(0, 8);
        ImGuiUtils.IconButtonWithTooltip(FontAwesomeIcon.Trash, "Excluir");

        ImGui.Spacing();
        Section("With Tooltip (desabilitado — tooltip ainda aparece)");
        using (ImRaii.Disabled(true))
        {
            ImGuiUtils.IconButtonWithTooltip(FontAwesomeIcon.Flag,  "Abrir no mapa",
                flags: (int)ImGuiHoveredFlags.AllowWhenDisabled);
            ImGui.SameLine(0, 8);
            ImGuiUtils.IconButtonWithTooltip(FontAwesomeIcon.Edit,  "Editar",
                flags: (int)ImGuiHoveredFlags.AllowWhenDisabled);
        }
    }

    private static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(title.ToUpperInvariant());
        ImGui.Separator();
        ImGui.Spacing();
    }
}

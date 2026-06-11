using Craftimizer.Utils;
using ImGuiNET;
using System.Collections.Generic;

namespace Craftimizer.UIStudio.Stories;

internal sealed class TabbedWindowStory : IStory
{
    public string Category => "Templates";
    public string Name     => "Tabbed Window";

    private string _inputText = "Exemplo de texto";
    private bool   _checked   = true;
    private float  _slider    = 0.65f;

    public void Draw()
    {
        Section("T1 — Janela com Abas (ex: MacroEditor, Settings)");

        if (ImGui.BeginTabBar("##tabs"))
        {
            if (ImGui.BeginTabItem("Geral"))
            {
                ImGui.Spacing();
                using (ImRaii2.GroupPanel("Configurações Gerais", -1, out _))
                {
                    ImGui.SetNextItemWidth(260);
                    ImGui.InputText("Nome", ref _inputText, 256);
                    ImGui.Checkbox("Ativar funcionalidade", ref _checked);
                    ImGui.SetNextItemWidth(260);
                    ImGui.SliderFloat("Intensidade", ref _slider, 0f, 1f, "%.2f");
                }
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Stats"))
            {
                ImGui.Spacing();
                ImGuiUtils.DrawBarRow(new List<ImGuiUtils.BarData>
                {
                    new("Progress",   Colors.Progress,   _slider * 4000f, 4000f),
                    new("Quality",    Colors.Quality,    _slider * 3500f, 3500f),
                    new("Durability", Colors.Durability, _slider *   80f,   80f),
                    new("CP",         Colors.CP,         _slider *  400f,  400f),
                });
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Sobre"))
            {
                ImGui.Spacing();
                ImGui.TextWrapped("Craftimizer UI Studio — Template T1: Tabbed Window");
                ImGui.Spacing();
                ImGui.TextDisabled("Versão 2.20.2.0 · FFXIV 7.51+ · Dalamud.NET.Sdk 15.0.0");
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
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

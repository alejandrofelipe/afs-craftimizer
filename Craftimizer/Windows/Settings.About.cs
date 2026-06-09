using Craftimizer.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI;
using System.Numerics;
using PluginClass = Craftimizer.Plugin.Plugin;

namespace Craftimizer.Windows;

public sealed partial class Settings
{
    private void DrawTabAbout()
    {
        using var tab = ImRaii.TabItem("About", ConsumeSelectedTab("About"));
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        var plugin = _plugin;
        var icon = plugin.Icon;
        var iconDim = new Vector2(UIConstants.PluginIconSize) * ImGuiHelpers.GlobalScale;

        using (var table = ImRaii.Table("settingsAboutTable", 2))
        {
            if (table)
            {
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, iconDim.X);

                ImGui.TableNextColumn();
                ImGui.Image(icon.Handle, iconDim);

                ImGui.TableNextColumn();
                ImGuiUtils.AlignMiddle(new(float.PositiveInfinity, HeaderFont.GetFontSize() + SubheaderFont.GetFontSize() + ImGui.GetFontSize() * 3 + ImGui.GetStyle().ItemSpacing.Y * 4), new(0, iconDim.Y));

                using (HeaderFont.Push())
                {
                    ImGuiUtils.AlignCentered(ImGui.CalcTextSize("Craftimizer").X);
                    ImGuiUtils.Hyperlink("Craftimizer", "https://github.com/WorkingRobot/Craftimizer", false);
                }

                using (SubheaderFont.Push())
                    ImGuiUtils.TextCentered($"v{plugin.Version} {plugin.BuildConfiguration}");

                ImGuiUtils.AlignCentered(ImGui.CalcTextSize($"By {plugin.Author} (WorkingRobot)").X);
                ImGui.TextUnformatted($"By {plugin.Author} (");
                ImGui.SameLine(0, 0);
                ImGuiUtils.Hyperlink("WorkingRobot", "https://github.com/WorkingRobot");
                ImGui.SameLine(0, 0);
                ImGui.TextUnformatted(")");

                using (ImRaii.PushColor(ImGuiCol.Text, Colors.Link))
                {
                    ImGuiUtils.AlignCentered(ImGui.CalcTextSize("Support the original author on Ko-fi").X);
                    ImGui.TextUnformatted("Support the original author on ");
                    ImGui.SameLine(0, 0);
                    ImGuiUtils.Hyperlink("Ko-fi", PluginClass.SupportLink);
                }
            }
        }

        ImGuiHelpers.ScaledDummy(5);

        ImGui.Separator();

        ImGuiHelpers.ScaledDummy(5);

        using (SubheaderFont.Push())
            ImGuiUtils.TextCentered("Special Thanks");

        ImGuiUtils.TextWrappedTo("Thank you to ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("alostsock", "https://github.com/alostsock");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(" for making ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("Craftingway", "https://craftingway.app");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(" and the original solver algorithm.");

        ImGuiUtils.TextWrappedTo("Thank you to ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("KonaeAkira", "https://github.com/KonaeAkira");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(" for making ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("raphael-rs", "https://raphael-xiv.com");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(" and the Optimal algorithm.");

        ImGuiUtils.TextWrappedTo("Thank you to ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("FFXIV Teamcraft", "https://ffxivteamcraft.com");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(" and its users for their community rotations.");

        ImGuiUtils.TextWrappedTo("Thank you to ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("this", "https://dke.maastrichtuniversity.nl/m.winands/documents/multithreadedMCTS2.pdf");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(", ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("this", "https://liacs.leidenuniv.nl/~plaata1/papers/paper_ICAART18.pdf");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(", and ");
        ImGui.SameLine(0, 0);
        ImGuiUtils.Hyperlink("this paper", "https://arxiv.org/abs/2308.04459");
        ImGui.SameLine(0, 0);
        ImGuiUtils.TextWrappedTo(" for inspiration and design references.");
    }
}

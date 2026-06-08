using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Solver;
using Craftimizer.Utils;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using PluginClass = Craftimizer.Plugin.Plugin;
using Configuration = Craftimizer.Plugin.Configuration;
using MacroCopyConfiguration = Craftimizer.Plugin.MacroCopyConfiguration;
using Service = Craftimizer.Plugin.Service;

namespace Craftimizer.Windows;

public sealed partial class Settings : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.None;

    private readonly PluginClass _plugin;
    private Configuration Config => _plugin.Configuration;

    private static float OptionWidth => 200 * ImGuiHelpers.GlobalScale;
    private static Vector2 OptionButtonSize => new(OptionWidth, ImGui.GetFrameHeight());

    private string? SelectedTab { get; set; }

    private IFontHandle HeaderFont { get; }
    private IFontHandle SubheaderFont { get; }

    public Settings(PluginClass plugin) : base("Craftimizer Settings", WindowFlags)
    {
        _plugin = plugin;
        _plugin.WindowSystem.AddWindow(this);

        HeaderFont = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(UiBuilder.DefaultFontSizePx * 2f)));
        SubheaderFont = Service.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(UiBuilder.DefaultFontSizePx * 1.5f)));

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new(450, 400),
            MaximumSize = new(float.PositiveInfinity)
        };
    }

    public void SelectTab(string label)
    {
        SelectedTab = label;
    }

    private ImGuiTabItemFlags ConsumeSelectedTab(string label)
    {
        if (string.Equals(SelectedTab, label, StringComparison.Ordinal))
        {
            SelectedTab = null;
            return ImGuiTabItemFlags.SetSelected;
        }
        return ImGuiTabItemFlags.None;
    }

    // ── Shared DrawOption overloads ───────────────────────────────────────────

    private static void DrawOption(string label, string tooltip, bool val, Action<bool> setter, ref bool isDirty, string? description = null)
    {
        var startX = ImGui.GetCursorPosX();
        if (ImGui.Checkbox(label, ref val))
        {
            setter(val);
            isDirty = true;
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGuiUtils.TooltipWrapped(tooltip);
        if (description != null)
        {
            ImGui.SetCursorPosX(startX + ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X);
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGui.TextUnformatted(description);
        }
    }

    private static void DrawSectionTitle(string title)
    {
        ImGuiHelpers.ScaledDummy(4);
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted(title);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(2);
    }

    private static void DrawOption<T>(string label, string tooltip, T value, T min, T max, Action<T> setter, ref bool isDirty) where T : struct, INumber<T>
    {
        ImGui.SetNextItemWidth(OptionWidth);
        var text = value.ToString();
        ArgumentNullException.ThrowIfNull(text, nameof(value));
        if (ImGui.InputText(label, ref text, 8, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CharsDecimal))
        {
            if (T.TryParse(text, null, out var newValue))
            {
                newValue = T.Clamp(newValue, min, max);
                if (value != newValue)
                {
                    setter(newValue);
                    isDirty = true;
                }
            }
        }
        else
        {
            var newValue = T.Clamp(value, min, max);
            if (value != newValue)
            {
                setter(newValue);
                isDirty = true;
            }
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGuiUtils.TooltipWrapped(tooltip);
    }

    private static void DrawOption(string label, string tooltip, string value, Action<string> setter, ref bool isDirty)
    {
        ImGui.SetNextItemWidth(OptionWidth);
        var text = value;
        if (ImGui.InputText(label, ref text, 255, ImGuiInputTextFlags.AutoSelectAll))
        {
            if (!string.Equals(value, text, StringComparison.Ordinal))
            {
                setter(text);
                isDirty = true;
            }
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGuiUtils.TooltipWrapped(tooltip);
    }

    private static void DrawOption<T>(string label, string tooltip, Func<T, string> getName, Func<T, string> getTooltip, T value, Action<T> setter, ref bool isDirty, params T[] excludedValues) where T : struct, Enum
    {
        ImGui.SetNextItemWidth(OptionWidth);
        using (var combo = ImRaii.Combo(label, getName(value)))
        {
            if (combo)
            {
                foreach (var type in Enum.GetValues<T>())
                {
                    if (excludedValues.Contains(type))
                        continue;
                    if (ImGui.Selectable(getName(type), value.Equals(type)))
                    {
                        setter(type);
                        isDirty = true;
                    }
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.TooltipWrapped(getTooltip(type));
                }
            }
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGuiUtils.TooltipWrapped(tooltip);
    }

    // ── Name/tooltip helpers ──────────────────────────────────────────────────

    private static string GetAlgorithmName(SolverAlgorithm algorithm) =>
        algorithm switch
        {
            SolverAlgorithm.Oneshot => "Oneshot",
            SolverAlgorithm.OneshotForked => "Oneshot Forked",
            SolverAlgorithm.Stepwise => "Stepwise",
            SolverAlgorithm.StepwiseForked => "Stepwise Forked",
            SolverAlgorithm.StepwiseGenetic => "Stepwise Genetic",
            SolverAlgorithm.Raphael => "Optimal",
            SolverAlgorithm.NextActionForked => "Next Action Forked",
            _ => "Unknown",
        };

    private static string GetAlgorithmTooltip(SolverAlgorithm algorithm) =>
        algorithm switch
        {
            SolverAlgorithm.Oneshot => "Run through all iterations and pick the best macro",
            SolverAlgorithm.OneshotForked => "Oneshot, but using multiple solvers simultaneously",
            SolverAlgorithm.Stepwise => "Run through all iterations and pick the next best step, " +
                                                "and repeat using previous steps as a starting point",
            SolverAlgorithm.StepwiseForked => "Stepwise, but using multiple solvers simultaneously",
            SolverAlgorithm.StepwiseGenetic => "Stepwise Forked, but the top N next best steps are " +
                                                "selected from the solvers, and each one is equally " +
                                                "used as a starting point",
            SolverAlgorithm.Raphael => "Finds the best solution, every time. This solver has " +
                                                "very different options compared to the rest, as it " +
                                                "is designed using an entirely different algorithm.",
            SolverAlgorithm.NextActionForked => "Evaluates each candidate next action independently " +
                                                "using forked MCTS and picks the best one. Designed " +
                                                "for Synthesis Helper: faster response and better " +
                                                "adaptation to changing conditions (Good, Excellent, etc.).",
            _ => "Unknown"
        };

    private static string GetCopyTypeName(MacroCopyConfiguration.CopyType type) =>
        type switch
        {
            MacroCopyConfiguration.CopyType.OpenWindow => "Open a Window",
            MacroCopyConfiguration.CopyType.CopyToMacro => "Copy to Macros",
            MacroCopyConfiguration.CopyType.CopyToClipboard => "Copy to Clipboard",
            MacroCopyConfiguration.CopyType.CopyToMacroMate => "Copy to Macro Mate",
            _ => "Unknown",
        };

    private static string GetCopyTypeTooltip(MacroCopyConfiguration.CopyType type) =>
        type switch
        {
            MacroCopyConfiguration.CopyType.OpenWindow => "Open a dedicated window with all macros being copied. " +
                                                                "Copy, view, and choose at your own leisure.",
            MacroCopyConfiguration.CopyType.CopyToMacro => "Copy directly to the game's macro system.",
            MacroCopyConfiguration.CopyType.CopyToClipboard => "Copy to your clipboard. Macros are separated by a blank line.",
            MacroCopyConfiguration.CopyType.CopyToMacroMate => "Copy directly to a Macro Mate macro. Requires the Macro Mate plugin.",
            _ => "Unknown"
        };

    private static string GetProgressBarTypeName(Configuration.ProgressBarType type) =>
        type switch
        {
            Configuration.ProgressBarType.Colorful => "Colorful",
            Configuration.ProgressBarType.Simple => "Simple",
            Configuration.ProgressBarType.None => "None",
            _ => "Unknown",
        };

    private static string GetProgressBarTooltip(Configuration.ProgressBarType type) =>
        type switch
        {
            Configuration.ProgressBarType.Colorful => "Colorful, rainbow colors",
            Configuration.ProgressBarType.Simple => "Simple, grayscale colors",
            Configuration.ProgressBarType.None => "No progress bar; only percent completion is shown",
            _ => "Unknown"
        };

    // ── Window lifecycle ──────────────────────────────────────────────────────

    public override void PreDraw()
    {
        base.PreDraw();
        Theme.Push();
    }

    public override void PostDraw()
    {
        Theme.Pop();
        base.PostDraw();
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("settingsTabBar"))
        {
            DrawTabGeneral();
            DrawTabRecipeNote();
            if (Config.EnableSynthHelper)
                DrawTabSynthHelper();
            DrawTabMacroEditor();
            DrawTabAbout();

            ImGui.EndTabBar();
        }
    }

    public void Dispose()
    {
        _plugin.WindowSystem.RemoveWindow(this);
        SubheaderFont?.Dispose();
        HeaderFont?.Dispose();
    }
}

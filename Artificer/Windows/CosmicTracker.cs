using Artificer.Plugin;
using Artificer.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace Artificer.Windows;

public sealed class CosmicTracker : Window, IDisposable
{
    private static readonly string[] TypeLabels =
        ["Type I", "Type II", "Type III", "Type IV", "Type V", "Type VI", "Type VII"];

    private CosmicToolTracker.ToolProgress? _progress;
    private readonly Plugin.Plugin _plugin;

    private readonly int[]      _deltas         = new int[7];
    private readonly DateTime[] _highlightUntil = new DateTime[7];

    private bool _hideComplete;

    private readonly TitleBarButton _eyeButton;
    private readonly TitleBarButton _minimizeButton;

    public CosmicTracker(Plugin.Plugin plugin) : base(
        "Cosmic Tracker###Artificer-cosmic",
        ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoFocusOnAppearing |
        ImGuiWindowFlags.NoNav)
    {
        _plugin = plugin;

        _progress = plugin.CosmicToolTracker.CachedProgress;
        plugin.CosmicToolTracker.OnProgressChanged += OnProgressChanged;

        IsOpen = _progress != null && plugin.Configuration.EnableCosmicToolTracking
                                   && !plugin.Configuration.CosmicTrackerHidden;
        RespectCloseHotkey = false;
        SizeConstraints    = new WindowSizeConstraints { MinimumSize = new(-1), MaximumSize = new(float.PositiveInfinity) };

        _eyeButton = new TitleBarButton
        {
            Icon        = FontAwesomeIcon.Eye,
            IconOffset  = new(2, 1),
            Click       = _ => { _hideComplete = !_hideComplete; UpdateEyeButton(); },
            ShowTooltip = () => ImGuiUtils.Tooltip(_hideComplete ? "Mostrar concluídos" : "Ocultar concluídos"),
        };

        _minimizeButton = new TitleBarButton
        {
            Icon        = FontAwesomeIcon.WindowMinimize,
            IconOffset  = new(2, 1),
            IconColor   = plugin.Configuration.CosmicTrackerMinimized ? Colors.CosmicActive : null,
            Click       = _ => ToggleMinimized(),
            ShowTooltip = () => ImGuiUtils.Tooltip(
                _plugin.Configuration.CosmicTrackerMinimized ? "Modo normal" : "Modo compacto"),
        };

        TitleBarButtons =
        [
            _eyeButton,
            _minimizeButton,
            new()
            {
                Icon        = FontAwesomeIcon.Sync,
                IconOffset  = new(2, 1),
                Click       = _ =>
                {
                    _plugin.CosmicToolTracker.ForceRefresh();
                    Plugin.Plugin.DisplayNotification(new()
                    {
                        Content         = "Dados do Cosmic Tool atualizados.",
                        Title           = "Cosmic Tracker",
                        Type            = Dalamud.Interface.ImGuiNotification.NotificationType.Success,
                        InitialDuration = TimeSpan.FromSeconds(3),
                    });
                },
                ShowTooltip = () => ImGuiUtils.Tooltip("Atualizar"),
            },
        ];

        plugin.WindowSystem.AddWindow(this);
    }

    public void ToggleHidden()
    {
        _plugin.Configuration.CosmicTrackerHidden = !_plugin.Configuration.CosmicTrackerHidden;
        _plugin.Configuration.Save();
        IsOpen = !_plugin.Configuration.CosmicTrackerHidden
              && _progress != null
              && _plugin.Configuration.EnableCosmicToolTracking;
    }

    private void ToggleMinimized()
    {
        _plugin.Configuration.CosmicTrackerMinimized = !_plugin.Configuration.CosmicTrackerMinimized;
        _plugin.Configuration.Save();
        _minimizeButton.IconColor = _plugin.Configuration.CosmicTrackerMinimized
            ? Colors.CosmicActive
            : null;
    }

    private void UpdateEyeButton()
    {
        _eyeButton.Icon      = _hideComplete ? FontAwesomeIcon.EyeSlash : FontAwesomeIcon.Eye;
        _eyeButton.IconColor = _hideComplete ? Colors.CosmicComplete : null;
    }

    private void OnProgressChanged(CosmicToolTracker.ToolProgress? progress)
    {
        if (_progress != null && progress != null)
        {
            for (var t = 0; t < 7; t++)
            {
                var prev = _progress.Types[t].Current;
                var next = progress.Types[t].Current;
                if (next != prev && progress.Types[t].Available)
                {
                    _deltas[t]         = next - prev;
                    _highlightUntil[t] = DateTime.UtcNow + TimeSpan.FromSeconds(10);
                }
            }
        }
        _progress = progress;
        IsOpen = progress != null
              && _plugin.Configuration.EnableCosmicToolTracking
              && !_plugin.Configuration.CosmicTrackerHidden;
    }

    public override void PreDraw()
    {
        if (_progress is { } p)
        {
            var jobRowId  = Service.Objects.LocalPlayer?.ClassJob.RowId ?? 0;
            var jobRow    = LuminaSheets.ClassJobSheet.GetRowOrDefault(jobRowId);
            var jobAbbr   = jobRow?.Abbreviation.ToString() ?? "";
            var jobLabel  = jobAbbr.Length > 0 ? jobAbbr : "Cosmic Tracker";
            var stagePart = p.MaxStage > 0 ? $"Stage {p.Stage}/{p.MaxStage}" : $"Stage {p.Stage}";
            WindowName = $"{jobLabel} — {stagePart}###Artificer-cosmic";
        }
        Theme.Push();
    }

    public override void PostDraw()
    {
        Theme.Pop();
        base.PostDraw();
    }

    public override void Draw()
    {
        if (_progress is not { } p)
        {
            ImGuiUtils.DrawEmptyState(
                (int)FontAwesomeIcon.Star,
                "Sem dados ainda",
                "Entre em qualquer mapa de Cosmic Exploration.");
            return;
        }

        var barWidth  = 280f * ImGuiHelpers.GlobalScale;
        var minimized = _plugin.Configuration.CosmicTrackerMinimized;
        var now       = DateTime.UtcNow;

        var anyRendered = false;
        for (var t = 0; t < 7; t++)
        {
            var td = p.Types[t];
            if (!td.Available || td.Max == 0) continue;
            var state = GetTypeState(t, p);
            if (_hideComplete && state is ImGuiUtils.ResearchTypeState.Complete
                                       or ImGuiUtils.ResearchTypeState.Maxed) continue;

            anyRendered = true;
            if (minimized)
                ImGuiUtils.DrawResearchTypeRow(TypeLabels[t], td.Current, td.Needed, td.Max, state, barWidth, ImGuiUtils.ResearchTypeRowMode.Minimized);
            else
            {
                var delta = (now < _highlightUntil[t]) ? _deltas[t] : (int?)null;
                ImGuiUtils.DrawResearchTypeRow(TypeLabels[t], td.Current, td.Needed, td.Max, state, barWidth, delta: delta);
            }
        }

        if (!anyRendered)
            ImGuiUtils.DrawEmptyState(
                (int)FontAwesomeIcon.CheckCircle,
                "Tudo concluído",
                "Todos os tipos de pesquisa foram completados.\nDesative o filtro para visualizá-los.");

        if (!minimized && p.MissionActive)
        {
            ImGui.Separator();
            ImGui.Spacing();
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.CosmicMission))
                ImGui.TextUnformatted("  Stellar Mission ativa");
        }
    }

    private static ImGuiUtils.ResearchTypeState GetTypeState(int t, CosmicToolTracker.ToolProgress p)
    {
        var td = p.Types[t];
        if (!td.Available) return ImGuiUtils.ResearchTypeState.Locked;
        if (td.Max > 0 && td.Current >= td.Max) return ImGuiUtils.ResearchTypeState.Maxed;
        if (td.Needed > 0 && td.Current >= td.Needed) return ImGuiUtils.ResearchTypeState.Complete;
        return ImGuiUtils.ResearchTypeState.Active;
    }

    public void Dispose()
    {
        _plugin.CosmicToolTracker.OnProgressChanged -= OnProgressChanged;
        _plugin.WindowSystem.RemoveWindow(this);
    }
}

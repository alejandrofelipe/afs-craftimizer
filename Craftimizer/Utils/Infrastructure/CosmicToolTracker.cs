using Craftimizer.Plugin;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.WKS;
using System;

namespace Craftimizer.Utils;

public sealed unsafe class CosmicToolTracker : IDisposable
{
    public record ToolProgress(
        int CurrentData,
        int NeededData,
        int ResearchType    // 0-based: 0=Type I … 6=Type VII
    );

    public event Action<ToolProgress?>? OnProgressChanged;
    public ToolProgress? CachedProgress { get; private set; }

    private readonly global::Craftimizer.Plugin.Plugin _plugin;

    private Hook<WKSManagerLoadDelegate>?          _loadHook;
    private Hook<WKSMissionModuleReportDelegate>?  _reportHook;
    private Hook<WKSMissionModuleAbandonDelegate>? _abandonHook;

    private delegate void WKSManagerLoadDelegate(WKSManager* self, ushort territoryId);
    private delegate void WKSMissionModuleReportDelegate(WKSMissionModule* self);
    private delegate void WKSMissionModuleAbandonDelegate(WKSMissionModule* self);

    public CosmicToolTracker(global::Craftimizer.Plugin.Plugin plugin)
    {
        _plugin = plugin;

        try
        {
            _loadHook = Service.GameInteropProvider.HookFromAddress<WKSManagerLoadDelegate>(
                (nint)WKSManager.MemberFunctionPointers.Load, OnLoad);
            _loadHook.Enable();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[CosmicToolTracker] Could not hook WKSManager.Load — territory enter updates disabled");
        }

        try
        {
            _reportHook = Service.GameInteropProvider.HookFromAddress<WKSMissionModuleReportDelegate>(
                (nint)WKSMissionModule.MemberFunctionPointers.ReportMission, OnReport);
            _reportHook.Enable();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[CosmicToolTracker] Could not hook WKSMissionModule.ReportMission — delivery updates disabled");
        }

        try
        {
            _abandonHook = Service.GameInteropProvider.HookFromAddress<WKSMissionModuleAbandonDelegate>(
                (nint)WKSMissionModule.MemberFunctionPointers.AbandonMission, OnAbandon);
            _abandonHook.Enable();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[CosmicToolTracker] Could not hook WKSMissionModule.AbandonMission");
        }

        Service.ClientState.TerritoryChanged += OnTerritoryChanged;

        // Capture initial state if the plugin loads while already in a WKS zone.
        RefreshSnapshot();
    }

    private void OnLoad(WKSManager* self, ushort territoryId)
    {
        _loadHook!.Original(self, territoryId);
        RefreshSnapshot();
    }

    private void OnReport(WKSMissionModule* self)
    {
        _reportHook!.Original(self);
        RefreshSnapshot();
    }

    private void OnAbandon(WKSMissionModule* self)
    {
        _abandonHook!.Original(self);
        RefreshSnapshot();
    }

    private void OnTerritoryChanged(uint _) => RefreshSnapshot();

    public void ForceRefresh() => RefreshSnapshot();

    private void RefreshSnapshot()
    {
        CachedProgress = ReadProgress();
        OnProgressChanged?.Invoke(CachedProgress);
    }

    private ToolProgress? ReadProgress()
    {
        if (!_plugin.Configuration.EnableCosmicToolTracking) return null;

        var wks = WKSManager.Instance();
        if (wks == null || !wks->IsLoaded) return null;

        var research = wks->ResearchModule;
        if (research == null || !research->IsLoaded) return null;

        var jobRowId = Service.Objects.LocalPlayer?.ClassJob.RowId ?? 0;
        var toolClass = GetToolClassIndex(jobRowId);
        if (toolClass < 0) return null;

        var currentType = GetCurrentResearchType(research, (byte)toolClass);

        var current = research->GetCurrentAnalysis((byte)toolClass, currentType);
        var needed  = research->GetNeededAnalysis((byte)toolClass, currentType);
        if (needed == 0) return null;

        return new ToolProgress(
            CurrentData:  current,
            NeededData:   needed,
            ResearchType: currentType
        );
    }

    // DoH: CRP=0 … CUL=7 | DoL: MIN=8, BTN=9, FSH=10
    private static int GetToolClassIndex(uint rowId) => rowId switch
    {
        8  => 0, 9  => 1, 10 => 2, 11 => 3,
        12 => 4, 13 => 5, 14 => 6, 15 => 7,
        16 => 8, 17 => 9, 18 => 10,
        _  => -1
    };

    private static byte GetCurrentResearchType(WKSResearchModule* mod, byte toolClass)
    {
        for (var t = 6; t >= 0; t--)
        {
            var tb = (byte)t;
            if (mod->IsTypeAvailable(toolClass, tb) && mod->GetNeededAnalysis(toolClass, tb) > 0)
                return tb;
        }
        return 0;
    }

    public void Dispose()
    {
        Service.ClientState.TerritoryChanged -= OnTerritoryChanged;
        _loadHook?.Dispose();
        _reportHook?.Dispose();
        _abandonHook?.Dispose();
    }
}

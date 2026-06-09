using Craftimizer.Plugin;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.WKS;
using Lumina.Excel.Sheets;
using System;

namespace Craftimizer.Utils;

public sealed unsafe class CosmicToolTracker : IDisposable
{
    public record ResearchTypeData(
        ushort Current,   // raw XP from Analysis array
        ushort Needed,    // threshold to advance to next stage
        ushort Max,       // XP cap for this type at this stage
        bool   Available  // whether this type is unlocked
    );

    public record ToolProgress(
        ResearchTypeData[] Types,    // indices 0–6 (Type I = 0 … Type VII = 6)
        int     ActiveType,          // highest available type index; -1 if none
        bool    MissionActive,       // wks->State.CurrentMission.MissionUnitRowId != 0
        byte    Stage,               // research->CurrentStages[toolClass], 1-based
        byte    MaxStage             // total stages (from Lumina WKSCosmoToolDataAmount.Stages.Count)
    );

    public event Action<ToolProgress?>? OnProgressChanged;
    public ToolProgress? CachedProgress { get; private set; }

    private readonly global::Craftimizer.Plugin.Plugin _plugin;
    private DateTime _lastAutoRefresh = DateTime.MinValue;

    private Hook<WKSManagerLoadDelegate>?          _loadHook;
    private Hook<WKSMissionModuleReportDelegate>?  _reportHook;
    private Hook<WKSMissionModuleAbandonDelegate>? _abandonHook;

    private delegate void WKSManagerLoadDelegate(WKSManager* self, ushort territoryId);
    private delegate void WKSMissionModuleReportDelegate(WKSMissionModule* self);
    private delegate void WKSMissionModuleAbandonDelegate(WKSMissionModule* self);

    public CosmicToolTracker(global::Craftimizer.Plugin.Plugin plugin)
    {
        _plugin = plugin;

        if (_plugin.Configuration.EnableCosmicToolTracking)
        {
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
        }

        Service.ClientState.TerritoryChanged += OnTerritoryChanged;
        Service.ClientState.Login += OnLogin;
        Service.ClientState.ClassJobChanged += OnClassJobChanged;
        Service.Framework.Update += OnFrameworkUpdate;

        // Schedule initial snapshot on the framework (main) thread.
        // Set _lastAutoRefresh now so OnFrameworkUpdate doesn't fire a second
        // refresh on the very first tick before RunOnFrameworkThread executes.
        _ = Service.Framework.RunOnFrameworkThread(RefreshSnapshot);
        _lastAutoRefresh = DateTime.UtcNow;
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

    private void OnClassJobChanged(uint _) => RefreshSnapshot();

    private void OnLogin()
        => _ = Service.Framework.RunOnTick(RefreshSnapshot, delayTicks: 30);

    // Periodic auto-refresh every 5 seconds so XP updates without manual interaction.
    private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework _)
    {
        if (!_plugin.Configuration.EnableCosmicToolTracking) return;
        if (DateTime.UtcNow - _lastAutoRefresh >= TimeSpan.FromSeconds(5))
        {
            _lastAutoRefresh = DateTime.UtcNow;
            RefreshSnapshot();
        }
    }

    public void ForceRefresh()
    {
        _lastAutoRefresh = DateTime.UtcNow; // reset timer so the next auto-refresh is 5s away
        RefreshSnapshot();
    }

    private void RefreshSnapshot()
    {
        var progress = ReadProgress();
        // Avoid redundant notifications: skip if result is null and was already null
        // (common when tracking is disabled or player is not in Cosmic territory).
        if (progress == null && CachedProgress == null) return;
        CachedProgress = progress;
        OnProgressChanged?.Invoke(CachedProgress);
    }

    private ToolProgress? ReadProgress()
    {
        if (!_plugin.Configuration.EnableCosmicToolTracking)
            return null;

        var wks = WKSManager.Instance();
        if (wks == null || !wks->IsLoaded)
            return null;

        var research = wks->ResearchModule;
        if (research == null || !research->IsLoaded)
            return null;

        var jobRowId  = Service.Objects.LocalPlayer?.ClassJob.RowId ?? 0;
        var toolClass = GetToolClassIndex(jobRowId);
        if (toolClass < 0) return null;

        var tc            = (byte)toolClass;
        var types         = new ResearchTypeData[7];
        var activeType    = -1;
        var rawAnalysis   = research->Analysis;
        var unlockedCount = research->UnlockedStages[tc];
        var stage         = tc < research->CurrentStages.Length ? research->CurrentStages[tc] : (byte)0;

        // WKSCosmoToolClass: row 0 is a placeholder (all zeros).
        // Jobs are at rows 1–11 (CRP=1 … FSH=11), so look up tc+1.
        //
        // Lumina Stages[] layout (0-based index i):
        //   MaxAmount[t]      = XP cap while the player is IN stage (i+1)
        //   RequiredAmount[t] = threshold that must be reached to ENTER stage (i+1)
        //
        // So for current stage S (1-based):
        //   max    = Stages[S-1].MaxAmount      — cap for current stage
        //   needed = Stages[S].RequiredAmount   — threshold to advance to S+1
        var hasLuminaData    = false;
        byte maxStage        = 0;
        WKSCosmoToolDataAmount.StagesStruct lumCur  = default; // Stages[stage-1] → max cap
        WKSCosmoToolDataAmount.StagesStruct lumNext = default; // Stages[stage]   → required threshold
        var classRow = LuminaSheets.WKSCosmoToolClassSheet.GetRowOrDefault((uint)(tc + 1));
        if (classRow is { } cr && stage > 0)
        {
            var da    = cr.DataAmount.Value;
            maxStage  = (byte)da.Stages.Count;
            var siCur = stage - 1;
            if (siCur < da.Stages.Count)
            {
                lumCur        = da.Stages[siCur];
                hasLuminaData = true;
            }
            var siNext = stage; // stage is 1-based, so this is the next stage's 0-based index
            if (siNext < da.Stages.Count)
                lumNext = da.Stages[siNext];
        }

        for (var t = 0; t < 7; t++)
        {
            var available = t < unlockedCount;
            var rawIdx    = tc * 7 + t;
            var current   = available && rawIdx < rawAnalysis.Length ? rawAnalysis[rawIdx] : (ushort)0;
            ushort needed = 0, max = 0;
            if (available && hasLuminaData)
            {
                if (t < lumNext.RequiredAmount.Count) needed = lumNext.RequiredAmount[t];
                if (t < lumCur.MaxAmount.Count)       max    = lumCur.MaxAmount[t];
            }
            types[t] = new ResearchTypeData(current, needed, max, available);
            if (available)
                activeType = t;
        }

        var missionActive = wks->State.CurrentMission.MissionUnitRowId != 0;

        return new ToolProgress(
            Types:         types,
            ActiveType:    activeType,
            MissionActive: missionActive,
            Stage:         stage,
            MaxStage:      maxStage
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

    public void Dispose()
    {
        Service.Framework.Update -= OnFrameworkUpdate;
        Service.ClientState.TerritoryChanged -= OnTerritoryChanged;
        Service.ClientState.Login -= OnLogin;
        Service.ClientState.ClassJobChanged -= OnClassJobChanged;
        _loadHook?.Dispose();
        _reportHook?.Dispose();
        _abandonHook?.Dispose();
    }
}

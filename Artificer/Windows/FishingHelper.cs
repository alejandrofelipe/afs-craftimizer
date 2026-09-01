using Artificer.Application.Fishing;
using Artificer.Plugin;
using Artificer.Utils;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game.WKS;
using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace Artificer.Windows;

public sealed unsafe class FishingHelper : Window, IDisposable
{
    private readonly global::Artificer.Plugin.Plugin _plugin;
    private CosmicFishingMission? _mission;
    private bool ShouldOpen { get; set; }

    public FishingHelper(global::Artificer.Plugin.Plugin plugin) : base("Fishing Helper###ArtificerFishingHelper")
    {
        _plugin = plugin;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ShowCloseButton = false;
        IsOpen = true;
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(UIConstants.FishingHelperWidth, -1),
            MaximumSize = new(UIConstants.FishingHelperWidth, 10000),
        };
        TitleBarButtons =
        [
            new()
            {
                Icon = FontAwesomeIcon.Cog,
                IconOffset = new(2, 1),
                Click = _ => _plugin.OpenSettingsTab("Crafting Log"),
                ShowTooltip = () => ImGuiUtils.Tooltip("Open Settings"),
            },
        ];
        _plugin.WindowSystem.AddWindow(this);
    }

    public override void Update()
    {
        base.Update();
        ShouldOpen = CalculateShouldOpen();
    }

    public override bool DrawConditions() => ShouldOpen;

    private bool CalculateShouldOpen()
    {
        if (!_plugin.Configuration.EnableCosmicFishingHelper)
            return false;
        if (Service.Objects.LocalPlayer is not { } player || player.ClassJob.RowId != 18) // FSH
            return false;
        var wks = WKSManager.Instance();
        if (wks == null || !wks->IsLoaded)
            return false;
        var missionId = wks->State.CurrentMission.MissionUnitRowId;
        if (missionId == 0)
        {
            _mission = null;
            return false;
        }
        _mission = CosmicFishingMissions.Resolve(missionId); // cacheado por id
        return _mission != null;
    }

    public override void PreDraw()  { base.PreDraw(); Theme.Push(); }
    public override void PostDraw() { Theme.Pop(); base.PostDraw(); }

    public override void Draw()
    {
        if (_mission is not { } mission)
            return;

        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGui.TextUnformatted(mission.Name);
        ImGuiHelpers.ScaledDummy(2);

        for (var i = 0; i < mission.Fish.Count; i++)
        {
            if (i > 0) ImGuiHelpers.ScaledDummy(4);
            DrawFishCard(mission.Fish[i]);
        }
    }

    private void DrawFishCard(RequiredFish fish)
    {
        var iconSize = new Vector2(ImGui.GetFrameHeight());

        // Linha 1: ícone + nome + xN
        DrawItemIcon(fish.IconId, iconSize, fish.Name);
        ImGui.SameLine();
        ImGui.TextUnformatted(fish.Name);
        if (fish.Quantity > 0)
        {
            ImGui.SameLine();
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGui.TextUnformatted($"x{fish.Quantity}");
        }

        if (!CosmicFishCatchData.Entries.TryGetValue(fish.ItemId, out var info))
        {
            ImGuiUtils.DrawBadgePill("sem dados de captura", Colors.ConditionGood);
            return;
        }

        // Linha 2: isca → [mooches] → alvo (ícones com tooltip do nome).
        // BaitItemId == 0 = sentinela "isca desconhecida na fonte" (decisão 2026-08-31):
        // nesse caso, em vez da cadeia, desenhar pill muted "isca desconhecida" (Colors.TextMuted).
        if (info.BaitItemId != 0)
            DrawBaitChain(info, fish, iconSize);
        else
            ImGuiUtils.DrawBadgePill("isca desconhecida", Colors.TextMuted);

        // Linha 3: tug + hookset + badges condicionais
        var tugColor = info.Tug switch
        {
            FishTug.Strong    => Colors.ConditionGood,
            FishTug.Legendary => Colors.ConditionExcellent,
            _                 => Colors.ConditionNormal,
        };
        using (ImRaii.PushColor(ImGuiCol.Text, tugColor))
            ImGui.TextUnformatted($"({CosmicFishFormat.TugText(info.Tug)})");
        ImGuiUtils.HoveredTooltip($"{info.Tug} bite");
        ImGui.SameLine();
        // FishHookset.Unknown (sentinela, 3 peixes) → NÃO desenhar a pill de hookset.
        if (info.Hookset != FishHookset.Unknown)
        {
            var hooksetColor = info.Hookset switch
            {
                FishHookset.Precise  => Colors.ActionBuff,
                FishHookset.Powerful => Colors.Durability,
                FishHookset.Stellar  => Colors.CosmicActive,
                _                    => Colors.ConditionNormal,
            };
            ImGuiUtils.DrawBadgePill(CosmicFishFormat.HooksetName(info.Hookset), hooksetColor);
        }

        // MultiHook real na fonte vai de 1 a 5 (decisão 2026-08-31): badge genérica, só quando >= 2.
        if (info.MultiHook >= 2)
        {
            ImGui.SameLine();
            ImGuiUtils.DrawBadgePill($"Multi Hook ×{info.MultiHook}", Colors.ActionBuff);
        }
        if (info.Lure != FishLure.None)
        {
            ImGui.SameLine();
            ImGuiUtils.DrawBadgePill($"Lure: {info.Lure}", Colors.Quality);
        }
        if (info.Predators is { Length: > 0 } predators)
        {
            foreach (var (fishId, count) in predators)
            {
                var name = LuminaSheets.ItemSheet.GetRowOrDefault(fishId)?.Name.ExtractText() ?? $"#{fishId}";
                ImGuiUtils.DrawBadgePill($"Intuition: {count}x {name}", Colors.CosmicMission);
            }
        }
    }

    // Isca + cada peixe da mooch chain, um ícone por vez, na mesma linha (SameLine).
    // O alvo (fish) já foi desenhado na linha 1 do card, então a cadeia não repete o ícone
    // dele aqui — fish só documenta o destino implícito ao final da cadeia.
    private void DrawBaitChain(FishCatchInfo info, RequiredFish fish, Vector2 iconSize)
    {
        var baitItem = LuminaSheets.ItemSheet.GetRowOrDefault(info.BaitItemId);
        DrawItemIcon(baitItem?.Icon ?? 0, iconSize, baitItem?.Name.ExtractText() ?? $"#{info.BaitItemId}");

        for (var i = 0; i < info.MoochChain.Length; i++)
        {
            ImGui.SameLine();
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGui.TextUnformatted("→");
            ImGui.SameLine();

            var moochId = info.MoochChain[i];
            var moochItem = LuminaSheets.ItemSheet.GetRowOrDefault(moochId);
            var moochName = moochItem?.Name.ExtractText() ?? $"#{moochId}";
            DrawItemIcon(moochItem?.Icon ?? 0, iconSize, $"Mooch: {moochName}");
        }
    }

    // Mesmo plumbing de ícone das janelas existentes: _plugin.IconManager.GetIconCached(id)
    // (IconManager.cs:152) → ImGui.Image(Handle, size). Dummy no lugar do ícone quando
    // iconId == 0 (mesma defesa de PluginImGuiUtils.DrawItemIcon).
    private void DrawItemIcon(uint iconId, Vector2 size, string name)
    {
        if (iconId == 0)
            ImGui.Dummy(size);
        else
            ImGui.Image(_plugin.IconManager.GetIconCached(iconId).Handle, size);
        ImGuiUtils.HoveredTooltip(name);
    }

    public void Dispose() => _plugin.WindowSystem.RemoveWindow(this);
}

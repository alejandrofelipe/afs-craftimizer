using System;
using System.Collections.Generic;
using Artificer.Application.Retainer;
using Artificer.Plugin;
using Artificer.Utils;
using Artificer.Utils.Infrastructure;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace Artificer.Windows;

/// <summary>
/// Assistente de preço do retainer. Abre ao entrar na lista de venda do mercado (addon RetainerSellList);
/// lista os itens à venda com preço atual vs menor preço do home world (preenchido on-demand quando o
/// usuário abre "comparar preços" de um item) e oferece dois mecanismos assist: copiar o undercut pro
/// clipboard, ou preencher direto o campo (best-effort, com salvaguarda). O plugin nunca confirma a venda.
/// </summary>
public sealed class RetainerMarketWindow : Window, IDisposable
{
    private readonly Plugin.Plugin _plugin;
    private readonly LiveMarketPriceSource _prices;
    private List<RetainerMarketItem> _items = new();
    // (itemId, isHq) -> (menor preço, é de retainer próprio). Preenchido on-demand pelas offerings ao vivo.
    private readonly Dictionary<(uint ItemId, bool IsHq), (long Price, bool IsOwn)> _lowest = new();

    // Slot cujo último "Preencher" falhou (feedback inline). -1 = nenhum.
    private short _fillFailedSlot = -1;

    private readonly IAddonLifecycle.AddonEventDelegate _onSetup;
    private readonly IAddonLifecycle.AddonEventDelegate _onFinalize;

    public RetainerMarketWindow(Plugin.Plugin plugin)
        : base("Preço do Retainer###ArtificerRetainer")
    {
        _plugin = plugin;
        _prices = new LiveMarketPriceSource();
        _prices.LowestReceived += OnLowest;

        _onSetup = (_, _) =>
        {
            if (!_plugin.Configuration.EnableRetainerPriceAssistant) return;
            Refresh();
            IsOpen = true;
        };
        _onFinalize = (_, _) => IsOpen = false;

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "RetainerSellList", _onSetup);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "RetainerSellList", _onFinalize);

        IsOpen = false;
        plugin.WindowSystem.AddWindow(this);
    }

    private void OnLowest(uint itemId, long price, bool isOwn, bool isHq)
        => _lowest[(itemId, isHq)] = (price, isOwn);

    /// <summary>Recarrega as listagens do retainer ativo e limpa os menores preços da sessão anterior.</summary>
    public void Refresh()
    {
        _lowest.Clear();
        _items = RetainerMarketReader.TryReadListings(out var items) ? items : new();
    }

    private (long Price, bool IsOwn)? LowestFor(RetainerMarketItem it)
    {
        if (_plugin.Configuration.RetainerPricingHqAware)
            return _lowest.TryGetValue((it.ItemId, it.IsHq), out var v) ? v : null;

        // HQ-agnóstico: menor entre as qualidades que temos.
        (long Price, bool IsOwn)? best = null;
        foreach (var hq in new[] { false, true })
            if (_lowest.TryGetValue((it.ItemId, hq), out var v) && (best is null || v.Price < best.Value.Price))
                best = v;
        return best;
    }

    private static string ItemName(uint itemId)
    {
        var name = LuminaSheets.ItemSheet.GetRowOrDefault(itemId)?.Name.ExtractText();
        return string.IsNullOrEmpty(name) ? $"#{itemId}" : name;
    }

    public override void Draw()
    {
        if (!LiveMarketPriceSource.IsAtHomeWorld())
        {
            using var c = ImRaii.PushColor(ImGuiCol.Text, Colors.Durability);
            ImGui.TextUnformatted("Você não está no seu home world — os preços podem ser de outro world.");
        }

        if (_items.Count == 0)
        {
            using var c = ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted);
            ImGui.TextUnformatted("Nenhum item à venda neste retainer.");
            return;
        }

        using var table = ImRaii.Table("retainerItems", 5,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp);
        if (!table) return;

        ImGui.TableSetupColumn("Item");
        ImGui.TableSetupColumn("Atual");
        ImGui.TableSetupColumn("Menor (home world)");
        ImGui.TableSetupColumn("Undercut");
        ImGui.TableSetupColumn("Ações");
        ImGui.TableHeadersRow();

        var cfg = _plugin.Configuration;
        var sellOpen = RetainerSellAssistant.IsSellDialogOpen();

        foreach (var it in _items)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(it.IsHq ? $"{ItemName(it.ItemId)} (HQ)" : ItemName(it.ItemId));

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(it.CurrentPrice.ToString("N0"));

            var lo = LowestFor(it);
            var hasLowest = lo is not null;

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(hasLowest ? lo!.Value.Price.ToString("N0") : "— (abra comparar preços)");

            var undercut = hasLowest
                ? UndercutCalculator.Compute((int)lo!.Value.Price, cfg.UndercutMode, cfg.UndercutAmount, lo.Value.IsOwn, cfg.UndercutSelf, cfg.RetainerPriceFloor)
                : 0;

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(hasLowest ? undercut.ToString("N0") : "—");

            ImGui.TableNextColumn();
            using (ImRaii.Disabled(!hasLowest))
            {
                if (ImGui.Button($"Copiar##{it.Slot}"))
                    RetainerSellAssistant.CopyPriceToClipboard(undercut);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Copia o undercut pro clipboard — cole (Ctrl+V) no campo de preço e confirme.");

            ImGui.SameLine();
            using (ImRaii.Disabled(!hasLowest || !sellOpen))
            {
                if (ImGui.Button($"Preencher##{it.Slot}"))
                    _fillFailedSlot = RetainerSellAssistant.TryFillPrice(undercut) ? (short)-1 : it.Slot;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Preenche o campo de preço do diálogo de venda aberto (você confirma). Se falhar, use Copiar.");

            if (_fillFailedSlot == it.Slot)
            {
                ImGui.SameLine();
                using var c = ImRaii.PushColor(ImGuiCol.Text, Colors.Bad);
                ImGui.TextUnformatted("não deu — use Copiar");
            }
        }
    }

    public void Dispose()
    {
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "RetainerSellList", _onSetup);
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "RetainerSellList", _onFinalize);
        _prices.LowestReceived -= OnLowest;
        _prices.Dispose();
        _plugin.WindowSystem.RemoveWindow(this);
    }
}

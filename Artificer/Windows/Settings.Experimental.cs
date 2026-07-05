using Artificer.Application.Retainer;
using Artificer.Utils;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Linq;
using Service = Artificer.Plugin.Service;

namespace Artificer.Windows;

public sealed partial class Settings
{
    private void DrawTabExperimental()
    {
        using var tab = ImRaii.TabItem("Experimental", ConsumeSelectedTab("Experimental"));
        if (!tab)
            return;

        ImGuiHelpers.ScaledDummy(5);

        using (ImRaii2.GroupPanel("⚠  Aviso", -1, out _))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.Bad))
                ImGui.TextWrapped("Estas opções são experimentais ou de uso avançado. " +
                                  "Alterações podem causar comportamento inesperado.");
        }

        ImGuiHelpers.ScaledDummy(10);

        var isDirty = false;

        // ── Feature: Listas de Coleta ─────────────────────────────────────────

        DrawSectionTitle("LISTAS DE COLETA");

        using (ImRaii2.GroupPanel("Listas de Coleta", -1, out _))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGui.TextWrapped("Sistema de planejamento de materiais de crafting. " +
                                  "Permite criar listas com receitas, rastrear inventário " +
                                  "e consultar preços do mercado.");

            ImGuiHelpers.ScaledDummy(4);

            DrawOption(
                "Habilitar Listas de Coleta",
                "Ativa o sistema de planejamento de materiais de crafting.",
                Config.EnableCraftingLists,
                v => Config.EnableCraftingLists = v,
                ref isDirty);

            DrawOption(
                "Apagar listas concluídas automaticamente",
                "⚠ Esta ação não pode ser desfeita.",
                Config.AutoDeleteCompletedLists,
                v => Config.AutoDeleteCompletedLists = v,
                ref isDirty);

            if (Config.EnableCraftingLists)
            {
                ImGuiHelpers.ScaledDummy(4);
                if (ImGui.Button("Abrir Lista de Coleta"))
                    _plugin.CraftingListWindow.OpenAndFocus();

                ImGuiHelpers.ScaledDummy(4);
                DrawSectionTitle("INVENTÁRIO");

                DrawOption(
                    "Verificar inventário ao abrir lista",
                    "Sincroniza automaticamente o inventário quando uma lista é aberta.",
                    Config.AutoSyncInventoryOnOpen,
                    v => Config.AutoSyncInventoryOnOpen = v,
                    ref isDirty);

                DrawOption(
                    "Incluir retainers na verificação",
                    "ℹ Só funciona para retainers visitados nessa sessão.",
                    Config.IncludeRetainersInSync,
                    v => Config.IncludeRetainersInSync = v,
                    ref isDirty);

                ImGuiHelpers.ScaledDummy(4);
                DrawSectionTitle("TELEPORTE");

                var teleporterStatus = _plugin.TeleportHelper.IsAvailable;
                using (ImRaii.PushColor(ImGuiCol.Text, teleporterStatus ? Colors.Progress : Colors.Bad))
                    ImGui.TextUnformatted(teleporterStatus ? "✓ Teleporter ativo" : "✗ Teleporter não encontrado");

                ImGuiHelpers.ScaledDummy(4);
                DrawSectionTitle("PREÇOS");

                DrawOption(
                    "Mostrar preços de compra dos materiais",
                    "Exibe colunas de preço de servidor e DC via Universalis.",
                    Config.ShowMarketPrices,
                    v => Config.ShowMarketPrices = v,
                    ref isDirty);

                if (Config.ShowMarketPrices)
                {
                    DrawOption(
                        "Atualizar a cada (min)",
                        "TTL do cache de preços em minutos.",
                        Config.MarketPriceCacheTtlMinutes,
                        1, 120,
                        v => Config.MarketPriceCacheTtlMinutes = v,
                        ref isDirty);

                    var universalisStatus = _plugin.MarketboardHelper.IsIpcAvailable;
                    using (ImRaii.PushColor(ImGuiCol.Text, universalisStatus ? Colors.Progress : Colors.TextMuted))
                        ImGui.TextUnformatted(universalisStatus ? "✓ Via plugin Universalis" : "○ Via REST API (online)");
                }
            }
        }

        ImGuiHelpers.ScaledDummy(10);

        // ── Feature: Gear Wear Tracking ───────────────────────────────────────

        DrawSectionTitle("RASTREAMENTO DE DESGASTE");

        using (ImRaii2.GroupPanel("Gear Wear Tracking", -1, out _))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGui.TextWrapped("Aprende quanto de durabilidade de equip cada receita consome " +
                                  "ao longo do tempo. Após ~10 crafts da mesma receita, as " +
                                  "previsões ficam precisas. Dados armazenados localmente.");

            ImGuiHelpers.ScaledDummy(4);

            DrawOption(
                "Habilitar Gear Wear Tracking",
                "Learn how much gear durability each recipe consumes over time. The plugin will monitor your gear condition before and after each craft, building a database of wear rates per recipe. After ~10 crafts of the same recipe, predictions become accurate. Data is stored locally and never shared.",
                Config.EnableGearWearTracking,
                v => Config.EnableGearWearTracking = v,
                ref isDirty,
                "🔬 Rastreia desgaste de equip para prever crafts restantes.");

            if (Config.EnableGearWearTracking && Config.GearWearData.Count > 0)
            {
                ImGuiHelpers.ScaledDummy(2);
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                    ImGui.TextWrapped($"Dados coletados: {Config.GearWearData.Count} receitas monitoradas, " +
                                      $"{Config.GearWearData.Values.Sum(s => s.SampleCount)} crafts registrados.");

                if (_confirmClearGearWear)
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, Colors.Bad))
                        ImGui.TextWrapped("Tem certeza? Esta ação não pode ser desfeita.");
                    if (ImGui.Button("Confirmar limpeza", OptionButtonSize))
                    {
                        Config.GearWearData.Clear();
                        isDirty = true;
                        _confirmClearGearWear = false;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancelar"))
                        _confirmClearGearWear = false;
                }
                else
                {
                    if (ImGui.Button("Limpar dados de rastreamento", OptionButtonSize))
                        _confirmClearGearWear = true;
                    if (ImGui.IsItemHovered())
                        ImGuiUtils.HoveredTooltip("Apaga todos os dados de desgaste coletados. O rastreamento reinicia do zero.");
                }
            }
        }

        ImGuiHelpers.ScaledDummy(10);

        // ── Feature: Cache de Ícones ──────────────────────────────────────────

        DrawSectionTitle("CACHE DE ÍCONES");

        using (ImRaii2.GroupPanel("Icon Cache Management", -1, out _))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGui.TextWrapped("Controla o ciclo de vida dos ícones em memória. " +
                                  "A maioria dos usuários não precisa ajustar estas opções.");

            ImGuiHelpers.ScaledDummy(4);

            DrawOption(
                "Enable Automatic Cache Cleanup",
                "Unload unused icons after inactivity period. Disable for maximum performance on high-memory systems.",
                Config.EnableIconCacheEviction,
                v => Config.EnableIconCacheEviction = v,
                ref isDirty,
                "Automatically frees memory by unloading icons not recently used."
            );

            if (Config.EnableIconCacheEviction)
            {
                DrawOption(
                    "Sliding Expiration (min)",
                    "Icon unloaded if not accessed for this duration. Lower values save more memory but may cause brief loading delays.",
                    Config.IconCacheSlidingExpirationMinutes,
                    1, 60,
                    v => Config.IconCacheSlidingExpirationMinutes = v,
                    ref isDirty
                );

                DrawOption(
                    "Max Cache Time (min)",
                    "Icon unloaded after this time, even if accessed frequently. Prevents memory buildup in long sessions.",
                    Config.IconCacheAbsoluteExpirationMinutes,
                    5, 120,
                    v => Config.IconCacheAbsoluteExpirationMinutes = v,
                    ref isDirty
                );
            }

            DrawOption(
                "Cache Size Limit",
                "Maximum icons in cache (0 = unlimited). Recommended: 1024 for typical use, 2048 for power users.",
                Config.IconCacheSizeLimit,
                0, 4096,
                v => Config.IconCacheSizeLimit = v,
                ref isDirty
            );
        }

        ImGuiHelpers.ScaledDummy(10);

        // ── Feature: Preço do Retainer ────────────────────────────────────────
        DrawSectionTitle("PREÇO DO RETAINER");

        using (ImRaii2.GroupPanel("Preço do Retainer", -1, out _))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGui.TextWrapped("Ao abrir a lista de venda do mercado de um retainer, mostra o preço atual " +
                                  "vs. o menor preço do seu home world (preenchido quando você abre \"comparar " +
                                  "preços\" de um item) e ajuda a aplicar um undercut. Modelo assistido: você " +
                                  "confirma cada venda; o plugin nunca confirma sozinho.");

            ImGuiHelpers.ScaledDummy(4);

            DrawOption(
                "Habilitar Assistente de Preço",
                "Abre a janela de preços ao entrar na lista de venda do retainer.",
                Config.EnableRetainerPriceAssistant,
                v => Config.EnableRetainerPriceAssistant = v,
                ref isDirty);

            if (Config.EnableRetainerPriceAssistant)
            {
                ImGuiHelpers.ScaledDummy(4);

                DrawOption<UndercutMode>(
                    "Modo de undercut",
                    "Como calcular o preço abaixo do menor do mercado.",
                    m => m == UndercutMode.FixedAmount ? "Valor fixo (gil)" : "Percentual (%)",
                    m => m == UndercutMode.FixedAmount ? "Subtrai uma quantia fixa de gil." : "Subtrai uma porcentagem do menor preço.",
                    Config.UndercutMode,
                    v => Config.UndercutMode = v,
                    ref isDirty);

                DrawOption(
                    "Quantia do undercut",
                    "Gil (modo fixo) ou porcentagem (modo percentual) a subtrair do menor preço.",
                    Config.UndercutAmount,
                    1, 99,
                    v => Config.UndercutAmount = v,
                    ref isDirty);

                DrawOption(
                    "Considerar qualidade (HQ/NQ)",
                    "Casa o menor preço pela qualidade do item à venda. Desligado usa o menor entre HQ e NQ.",
                    Config.RetainerPricingHqAware,
                    v => Config.RetainerPricingHqAware = v,
                    ref isDirty);

                DrawOption(
                    "Dar undercut nos próprios retainers",
                    "Se o menor preço já é de um retainer seu: ligado desconta mesmo assim; desligado iguala (não compete consigo).",
                    Config.UndercutSelf,
                    v => Config.UndercutSelf = v,
                    ref isDirty);

                DrawOption(
                    "Piso de preço",
                    "Nunca precificar abaixo deste valor.",
                    Config.RetainerPriceFloor,
                    1, 999,
                    v => Config.RetainerPriceFloor = v,
                    ref isDirty);
            }
        }

        ImGuiHelpers.ScaledDummy(10);

        if (isDirty)
            Config.Save();
    }
}

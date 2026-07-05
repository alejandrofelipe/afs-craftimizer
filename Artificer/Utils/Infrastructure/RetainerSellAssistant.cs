using Artificer.Plugin;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Artificer.Utils.Infrastructure;

/// <summary>
/// Ajuda o usuário a definir o preço de venda no diálogo RetainerSell — modelo ASSIST:
/// (1) copiar o preço pro clipboard (robusto, à prova de patch; o usuário cola e confirma), e
/// (2) tentar preencher o campo numérico direto (best-effort, com salvaguarda — nunca crasha:
///     se o input não for encontrado/definível, retorna false sem tocar em ponteiro nulo).
/// O plugin NUNCA confirma a venda (nada de input sintético de confirmação).
/// </summary>
/// <remarks>
/// Verificado por reflection sobre FFXIVClientStructs.dll (SDK dev atual):
/// <see cref="AtkComponentNumericInput"/> expõe um método público real <c>SetValue(int)</c>
/// (além do campo <c>Value</c> e de <c>InnerSetValue(int, bool, bool)</c>), então o preenchimento
/// direto É possível — mas o node não é localizado por ID fixo: varremos
/// <c>AtkUldManager.NodeList</c> e usamos <c>AtkResNode.GetAsAtkComponentNumericInput()</c>, que
/// já faz a checagem de tipo internamente (retorna null se o nó não for um NumericInput) — mais
/// seguro que castar manualmente via <c>ComponentType</c>.
/// </remarks>
public static unsafe class RetainerSellAssistant
{
    private const string AddonName = "RetainerSell";

    private static AtkUnitBase* Addon()
    {
        var addr = Service.GameGui.GetAddonByName(AddonName).Address;
        return addr == 0 ? null : (AtkUnitBase*)addr;
    }

    /// <summary>True se o diálogo de venda do retainer está aberto.</summary>
    public static bool IsSellDialogOpen() => Addon() != null;

    /// <summary>Mecanismo robusto: coloca o preço no clipboard pra o usuário colar (Ctrl+V).</summary>
    public static void CopyPriceToClipboard(int price) => ImGui.SetClipboardText(price.ToString());

    /// <summary>
    /// Mecanismo best-effort: tenta preencher o campo numérico do preço diretamente.
    /// SALVAGUARDA: retorna false (sem crashar) se o diálogo não está aberto, se o input
    /// numérico não for localizado, ou se não houver setter de valor disponível.
    /// </summary>
    public static bool TryFillPrice(int price)
    {
        var addon = Addon();
        if (addon == null) return false;

        var numeric = FindPriceNumericInput(addon);
        if (numeric == null) return false;

        // Setter real verificado via reflection: AtkComponentNumericInput.SetValue(int).
        // triggerCallback/playSoundEffect não se aplicam a esta assinatura pública.
        numeric->SetValue(price);
        return true;
    }

    /// <summary>Localiza defensivamente o AtkComponentNumericInput do preço varrendo os nós do addon. Null se não achar.</summary>
    private static AtkComponentNumericInput* FindPriceNumericInput(AtkUnitBase* addon)
    {
        // Guardas: UldManager e NodeList podem ser nulos/vazios.
        var uld = &addon->UldManager;
        if (uld == null || uld->NodeList == null) return null;

        for (var i = 0; i < uld->NodeListCount; i++)
        {
            var node = uld->NodeList[i];
            if (node == null) continue;

            // GetAsAtkComponentNumericInput() já valida internamente o tipo do nó/componente
            // (NodeType.Component + ComponentType.NumericInput) e retorna null em caso de
            // mismatch — não precisamos (nem devemos) castar manualmente aqui.
            var numeric = node->GetAsAtkComponentNumericInput();
            if (numeric != null) return numeric;
        }
        return null;
    }
}

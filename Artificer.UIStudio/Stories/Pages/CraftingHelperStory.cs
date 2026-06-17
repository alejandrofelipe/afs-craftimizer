using Artificer.Utils;
using ImGuiNET;
using System;
using System.Numerics;

namespace Artificer.UIStudio.Stories;

internal sealed class CraftingHelperStory : IStory
{
    public string Category => "Pages";
    public string Name     => "CraftingHelper";

    private static readonly string[] Sections =
    [
        "0 · CraftableStatus", "1 · Recipe Header", "2 · Gear Condition",
        "3 · Saved Macro",     "4 · Suggested Macro", "5 · Community Macro",
        "6 · Main Button",
    ];

    private int _section;

    // ── Mock constants ────────────────────────────────────────────────────────
    private const float PanelW = 300f;

    private const int MockCraftsmanship  = 3700;
    private const int MockControl        = 3800;
    private const int MockCP             = 590;
    private const int MockRequiredCrafts = 3900;
    private const int MockRequiredCtrl   = 3950;

    public void Draw()
    {
        ImGui.SetNextItemWidth(240f);
        ImGui.Combo("Seção##ch", ref _section, Sections, Sections.Length);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var width = ImGui.GetContentRegionAvail().X;

        switch (_section)
        {
            case 0: DrawSection_CraftableStatus(width); break;
            case 1: DrawSection_RecipeHeader(width);    break;
            case 2: DrawSection_GearCondition(width);   break;
            case 3: DrawSection_SavedMacro(width);      break;
            case 4: DrawSection_SuggestedMacro(width);  break;
            case 5: DrawSection_CommunityMacro(width);  break;
            case 6: DrawSection_MainButton(width);      break;
        }
    }

    // ── Seção 0: CraftableStatus ──────────────────────────────────────────────

    private static void DrawSection_CraftableStatus(float totalW)
    {
        // 8 sub-estados side by side, wrapping automático
        var states = new (string Label, Action Draw)[]
        {
            ("OK",                  DrawCraftStatus_OK),
            ("LockedClassJob",      DrawCraftStatus_Locked),
            ("WrongClassJob",       DrawCraftStatus_WrongJob),
            ("SpecialistRequired",  DrawCraftStatus_Specialist),
            ("RequiredItem",        DrawCraftStatus_RequiredItem),
            ("RequiredStatus",      DrawCraftStatus_RequiredStatus),
            ("CraftsmanshipTooLow", DrawCraftStatus_CraftsmanshipLow),
            ("ControlTooLow",       DrawCraftStatus_ControlLow),
        };

        DrawGallery(states, PanelW);
    }

    private static void DrawCraftStatus_OK()
    {
        if (!ImGui.BeginTable("ok##stats", 2)) return;
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 110f);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
        DrawStatRow2("Craftsmanship", MockCraftsmanship.ToString());
        DrawStatRow2("Control",       MockControl.ToString());
        DrawStatRow2("CP",            MockCP.ToString());
        ImGui.EndTable();
    }

    private static void DrawCraftStatus_Locked()
    {
        ImGuiUtils.TextCentered("You do not have Weaver unlocked.");
        ImGui.Separator();
        ImGuiUtils.TextCentered("Unlock it from Guildmaster Maronne");
        ImGuiUtils.TextCentered("Ul'dah - Steps of Thal (12.5, 8.6)");
    }

    private static void DrawCraftStatus_WrongJob()
    {
        ImGuiUtils.TextCentered("You are not a Weaver.");
        if (ImGuiUtils.ButtonCentered("Switch Job"))
            _ = 0; // no-op in story
        ImGuiUtils.HoveredTooltip("Swap to gearset 4");
    }

    private static void DrawCraftStatus_Specialist()
    {
        ImGuiUtils.TextCentered("You need to be a specialist to craft this recipe.");
        ImGui.Separator();
        ImGuiUtils.TextCentered("Trade a Soul of the Crafter to Zuzuvano");
        ImGuiUtils.TextCentered("Ul'dah - Steps of Thal (14.2, 10.8)");
    }

    private static void DrawCraftStatus_RequiredItem()
    {
        ImGuiUtils.TextCentered("You are missing the required equipment.");
        ImGuiUtils.TextCentered("[★] Diadochos Needle");
    }

    private static void DrawCraftStatus_RequiredStatus()
    {
        ImGuiUtils.TextCentered("You are missing the required status effect.");
        ImGuiUtils.TextCentered("[✦] Well Fed");
    }

    private static void DrawCraftStatus_CraftsmanshipLow()
    {
        ImGuiUtils.TextCentered("Your Craftsmanship is too low.");
        if (!ImGui.BeginTable("craftslow##stats", 2)) return;
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextColumn(); ImGui.TextUnformatted("Current");
        ImGui.TableNextColumn(); ImGui.TextColored(Colors.Good,  MockCraftsmanship.ToString());
        ImGui.TableNextColumn(); ImGui.TextUnformatted("Required");
        ImGui.TableNextColumn(); ImGui.TextColored(Colors.Bad,   MockRequiredCrafts.ToString());
        ImGui.TableNextColumn(); ImGui.TextUnformatted("You need");
        ImGui.TableNextColumn(); ImGui.TextUnformatted((MockRequiredCrafts - MockCraftsmanship).ToString());
        ImGui.EndTable();
    }

    private static void DrawCraftStatus_ControlLow()
    {
        ImGuiUtils.TextCentered("Your Control is too low.");
        if (!ImGui.BeginTable("ctrllow##stats", 2)) return;
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextColumn(); ImGui.TextUnformatted("Current");
        ImGui.TableNextColumn(); ImGui.TextColored(Colors.Good, MockControl.ToString());
        ImGui.TableNextColumn(); ImGui.TextUnformatted("Required");
        ImGui.TableNextColumn(); ImGui.TextColored(Colors.Bad,  MockRequiredCtrl.ToString());
        ImGui.TableNextColumn(); ImGui.TextUnformatted("You need");
        ImGui.TableNextColumn(); ImGui.TextUnformatted((MockRequiredCtrl - MockControl).ToString());
        ImGui.EndTable();
    }

    // ── Seção 1: Recipe Header ────────────────────────────────────────────────

    private static void DrawSection_RecipeHeader(float totalW)
    {
        var states = new (string Label, Action Draw)[]
        {
            ("Normal",      DrawRecipe_Normal),
            ("Expert",      DrawRecipe_Expert),
            ("Collectible", DrawRecipe_Collectible),
            ("Cosmic",      DrawRecipe_Cosmic),
        };
        DrawGallery(states, PanelW);
    }

    private static void DrawRecipe_Normal()
    {
        ImGuiUtils.TextCentered("Lv90 ★★★  Espada de Aço");
        ImGui.Separator();
        if (!ImGui.BeginTable("rn##stats", 2)) return;
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 90f);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
        DrawStatRow2("Progress",   "4100");
        DrawStatRow2("Quality",    "7800");
        DrawStatRow2("Durability", "70");
        ImGui.EndTable();
    }

    private static void DrawRecipe_Expert()
    {
        ImGuiUtils.TextCentered("Lv90  Lâmina Expert");
        ImGui.SameLine(0, 4);
        ImGuiUtils.DrawBadgePill("Expert", Colors.Bad);
        ImGui.Separator();
        if (!ImGui.BeginTable("re##stats", 2)) return;
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 90f);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
        DrawStatRow2("Progress",   "5060");
        DrawStatRow2("Quality",    "12628");
        DrawStatRow2("Durability", "55");
        ImGui.EndTable();
    }

    private static void DrawRecipe_Collectible()
    {
        ImGuiUtils.TextCentered("Lv90  Engrenagem Coletável");
        ImGui.SameLine(0, 4);
        ImGuiUtils.DrawBadgePill("Collectible", Colors.Collectability);
        ImGui.Separator();
        if (!ImGui.BeginTable("rc##stats", 2)) return;
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 90f);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
        DrawStatRow2("Progress",   "4300");
        DrawStatRow2("Quality",    "9000");
        DrawStatRow2("Durability", "80");
        ImGui.EndTable();
    }

    private static void DrawRecipe_Cosmic()
    {
        ImGuiUtils.TextCentered("Lv90~100  Ferramenta Cósmica");
        ImGui.SameLine(0, 4);
        ImGuiUtils.DrawBadgePill("Cosmic", Colors.CosmicActive);
        ImGui.Separator();
        if (!ImGui.BeginTable("rco##stats", 2)) return;
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 110f);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch);
        DrawStatRow2("Progress",   "4100");
        DrawStatRow2("Quality",    "7800");
        DrawStatRow2("Durability", "70");
        ImGui.TableNextColumn(); ImGui.TextUnformatted("Research Type II");
        ImGui.TableNextColumn();
        ImGui.ProgressBar(0.62f, new Vector2(-1, ImGui.GetTextLineHeight()));
        ImGui.EndTable();
    }

    // ── Seção 2: Gear Condition Alert ─────────────────────────────────────────

    private static void DrawSection_GearCondition(float totalW)
    {
        var states = new (string Label, Action Draw)[]
        {
            ("Info (≥50%)",      () => ImGuiUtils.DrawAlert(AlertVariant.Info,    "Gear Condition", "72% · ~30 crafts left")),
            ("Warning (25-50%)",      () => ImGuiUtils.DrawAlert(AlertVariant.Warning, "Gear Condition", "38% · ~12–15 crafts left")),
            ("Danger (<25%)",         () => ImGuiUtils.DrawAlert(AlertVariant.Danger,  "Gear Condition", "18% · ~3 crafts left — repair now!")),
        };
        DrawGallery(states, PanelW);
    }

    // ── Seções 3–6: stubs temporários ────────────────────────────────────────

    private static void DrawSection_SavedMacro(float _)     => ImGui.TextDisabled("(Task 3)");
    private static void DrawSection_SuggestedMacro(float _) => ImGui.TextDisabled("(Task 4)");
    private static void DrawSection_CommunityMacro(float _) => ImGui.TextDisabled("(Task 5)");
    private static void DrawSection_MainButton(float _)     => ImGui.TextDisabled("(Task 5)");

    // ── Helpers compartilhados ────────────────────────────────────────────────

    // Desenha uma galeria side-by-side de sub-estados, cada um dentro de um GroupPanel.
    private static void DrawGallery((string Label, Action Draw)[] states, float panelW)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var i = 0;
        foreach (var (label, draw) in states)
        {
            if (i > 0) ImGui.SameLine(0, spacing);
            using (var panel = ImRaii2.GroupPanel(label, panelW, out _))
                if (panel) draw();
            i++;
        }
    }

    private static void DrawStatRow2(string label, string value)
    {
        ImGui.TableNextColumn(); ImGui.TextUnformatted(label);
        ImGui.TableNextColumn(); ImGuiUtils.TextRight(value);
    }
}

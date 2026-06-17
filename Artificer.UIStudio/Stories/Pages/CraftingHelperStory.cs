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

    // ── Seção 3: Saved Macro Card ─────────────────────────────────────────────

    private static void DrawSection_SavedMacro(float totalW)
    {
        var states = new (string Label, Action Draw)[]
        {
            ("Calculando",    DrawSaved_Loading),
            ("Vazio",         DrawSaved_Empty),
            ("Falhou",        DrawSaved_Failed),
            ("Exceção",       DrawSaved_Exception),
            ("Pronto",        DrawSaved_Ready),
            ("HashMismatch",  DrawSaved_HashMismatch),
        };
        DrawGallery(states, PanelW);
    }

    private static void DrawSaved_Loading()
        => ImGuiUtils.TextMiddleNewLine("Calculating...", new Vector2(PanelW, CardH));

    private static void DrawSaved_Empty()
    {
        var availW  = PanelW;
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var iconH   = ImGui.GetTextLineHeight() * 1.6f;
        var totalH  = iconH + spacing + ImGui.GetTextLineHeightWithSpacing() + ImGui.GetTextLineHeight();
        var startY  = ImGui.GetCursorPosY() + Math.Max(0f, (CardH - totalH) / 2f);
        ImGui.SetCursorPosY(startY);
        ImGuiUtils.TextCentered("📂", availW);
        ImGuiUtils.TextCentered("No saved macro for this recipe", availW);
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGuiUtils.TextCentered("Create one in the Macro Editor or solve below.", availW);
        ImGui.SetCursorPosY(startY + CardH + spacing);
    }

    private static void DrawSaved_Failed()
    {
        var availW  = PanelW;
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var iconH   = ImGui.GetTextLineHeight() * 1.6f;
        var totalH  = iconH + spacing + ImGui.GetTextLineHeightWithSpacing() + ImGui.GetTextLineHeight();
        var startY  = ImGui.GetCursorPosY() + Math.Max(0f, (CardH - totalH) / 2f);
        ImGui.SetCursorPosY(startY);
        ImGuiUtils.TextCentered("⚠", availW);
        ImGuiUtils.TextCentered("Couldn't generate a macro", availW);
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
            ImGuiUtils.TextCentered("Try adjusting solver settings", availW);
        ImGui.SetCursorPosY(startY + CardH + spacing);
    }

    private static void DrawSaved_Exception()
    {
        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushColor(ImGuiCol.Text, Colors.Bad))
            ImGuiUtils.TextCentered("An exception occurred");
        if (ImGuiUtils.ButtonCentered("Copy Error Message"))
            ImGui.SetClipboardText("System.Exception: Mock error");
    }

    private static void DrawSaved_Ready()        => DrawMockMacroCard("ready", "Iron Will", 87, hashMismatch: false);
    private static void DrawSaved_HashMismatch() => DrawMockMacroCard("mismatch", "Iron Will", 87, hashMismatch: true);

    // Renders the macro card without PluginImGuiUtils (replaces arcs with colored grid).
    private static void DrawMockMacroCard(string id, string name, int hqPct, bool hashMismatch)
    {
        var spacing   = ImGui.GetStyle().ItemSpacing;
        var miniRowH  = (CardH - spacing.Y) / 2f;
        var arcColW   = miniRowH * 2 + spacing.X;
        var botRowH   = ImGui.GetFrameHeight();
        var rightColW = Math.Max(1f, PanelW - arcColW - 1f);

        if (!ImGui.BeginTable($"mcard##{id}", 2, ImGuiTableFlags.None, new Vector2(PanelW, 0))) return;
        ImGui.TableSetupColumn("left",  ImGuiTableColumnFlags.WidthFixed, arcColW);
        ImGui.TableSetupColumn("right", ImGuiTableColumnFlags.WidthFixed, rightColW);

        // Row 1: 2×2 colored arc grid | action icon buttons
        ImGui.TableNextRow(ImGuiTableRowFlags.None, CardH);
        ImGui.TableSetColumnIndex(0);
        DrawMockArcGrid(miniRowH, arcColW);

        ImGui.TableSetColumnIndex(1);
        {
            var itemsPerRow = Math.Max(1, (int)MathF.Floor((rightColW + spacing.X) / (miniRowH + spacing.X)));
            // Draw 2 rows of action slots using colored dummy squares
            for (var i = 0; i < itemsPerRow * 2; i++)
            {
                if (i % itemsPerRow != 0) ImGui.SameLine(0, spacing.X);
                var pos = ImGui.GetCursorScreenPos();
                ImGui.GetWindowDrawList().AddRectFilled(pos, pos + new Vector2(miniRowH), ImGui.GetColorU32(ImGuiCol.FrameBg), 3f);
                ImGui.Dummy(new Vector2(miniRowH));
            }
        }

        // Row 2: HQ% | macro name + edit + copy buttons
        ImGui.TableNextRow(ImGuiTableRowFlags.None, botRowH);
        ImGui.TableSetColumnIndex(0);
        {
            var pctColor = hqPct >= 100 ? Colors.Progress
                         : hqPct >=  75 ? Colors.Quality
                         : hqPct >=  50 ? Colors.ActionBuff
                         :                Colors.Bad;
            ImGui.AlignTextToFramePadding();
            using (ImRaii.PushColor(ImGuiCol.Text, pctColor))
                ImGuiUtils.TextCentered($"{hqPct}%", arcColW);
        }
        ImGui.TableSetColumnIndex(1);
        {
            if (hashMismatch)
            {
                using (ImRaii.PushColor(ImGuiCol.Text, Colors.Bad))
                {
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted("⚠");
                }
                ImGuiUtils.HoveredTooltip("Macro salvo com stats diferentes", wrapWidth: 300);
                ImGui.SameLine();
            }
            var iconH      = botRowH;
            var cellStart  = ImGui.GetCursorPos();
            var cellAvailW = ImGui.GetContentRegionAvail().X;
            var editX      = cellStart.X + cellAvailW - iconH * 2 - spacing.X;
            ImGui.SetCursorPos(new Vector2(editX, cellStart.Y));
            ImGuiUtils.IconButtonSquare(0xF044 /* edit icon codepoint */, iconH);
            ImGuiUtils.HoveredTooltip("Open in Macro Editor");
            ImGui.SameLine(0, spacing.X);
            ImGuiUtils.IconButtonSquare(0xF0EA /* paste icon codepoint */, iconH);
            ImGuiUtils.HoveredTooltip("Copy to Clipboard");
            var nameMaxW  = cellAvailW - iconH * 2 - spacing.X * 2;
            ImGui.SetCursorPos(new Vector2(cellStart.X, cellStart.Y + (botRowH - ImGui.GetTextLineHeight()) * 0.5f));
            var nameMin = ImGui.GetCursorScreenPos();
            ImGui.PushClipRect(nameMin, nameMin + new Vector2(nameMaxW, botRowH), true);
            using (ImRaii.PushColor(ImGuiCol.Text, Colors.TextMuted))
                ImGui.TextUnformatted(name);
            ImGui.PopClipRect();
        }
        ImGui.EndTable();
    }

    // Visual substitute for DrawMacroStatArcs: 2×2 grid of colored rectangles.
    private static void DrawMockArcGrid(float cellH, float totalW)
    {
        var spacing = ImGui.GetStyle().ItemSpacing;
        var colors  = new[] { Colors.Progress, Colors.Quality, Colors.ActionBuff, Colors.Bad };
        var labels  = new[] { "P", "Q", "D", "CP" };
        for (var i = 0; i < 4; i++)
        {
            if (i % 2 != 0) ImGui.SameLine(0, spacing.X);
            var pos = ImGui.GetCursorScreenPos();
            var dl  = ImGui.GetWindowDrawList();
            var col = colors[i];
            dl.AddRectFilled(pos, pos + new Vector2(cellH), ImGui.ColorConvertFloat4ToU32(col with { W = 0.25f }), 4f);
            dl.AddRect(      pos, pos + new Vector2(cellH), ImGui.ColorConvertFloat4ToU32(col), 4f);
            dl.AddText(pos + new Vector2(3, 2), ImGui.GetColorU32(ImGuiCol.Text), labels[i]);
            ImGui.Dummy(new Vector2(cellH));
        }
    }

    // Default height for macro card content (2 frame rows).
    private static float CardH => 2 * ImGui.GetFrameHeightWithSpacing();

    // ── Seções 4–6: stubs temporários ────────────────────────────────────────

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

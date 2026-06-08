using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Craftimizer.Plugin;

public static class LuminaSheets
{
    private static readonly ExcelModule Module = Service.DataManager.GameData.Excel;

    // Sheets used on startup or in hot paths — eager.
    public static readonly ExcelSheet<Recipe> RecipeSheet = Module.GetSheet<Recipe>();
    public static readonly ExcelSheet<Action> ActionSheet = Module.GetSheet<Action>();
    public static readonly ExcelSheet<CraftAction> CraftActionSheet = Module.GetSheet<CraftAction>();
    public static readonly ExcelSheet<Status> StatusSheet = Module.GetSheet<Status>();
    public static readonly ExcelSheet<Addon> AddonSheet = Module.GetSheet<Addon>();
    public static readonly ExcelSheet<ClassJob> ClassJobSheet = Module.GetSheet<ClassJob>();
    public static readonly ExcelSheet<Item> ItemSheet = Module.GetSheet<Item>();
    public static readonly ExcelSheet<ItemFood> ItemFoodSheet = Module.GetSheet<ItemFood>();
    public static readonly ExcelSheet<RecipeLevelTable> RecipeLevelTableSheet = Module.GetSheet<RecipeLevelTable>();

    // Sheets used only on first interaction or under specific conditions — lazy.
    private static readonly System.Lazy<ExcelSheet<Item>> _itemSheetEnglish = new(() => Module.GetSheet<Item>(Language.English)!);
    private static readonly System.Lazy<ExcelSheet<Level>> _levelSheet = new(() => Module.GetSheet<Level>());
    private static readonly System.Lazy<ExcelSheet<Quest>> _questSheet = new(() => Module.GetSheet<Quest>());
    private static readonly System.Lazy<ExcelSheet<Materia>> _materiaSheet = new(() => Module.GetSheet<Materia>());
    private static readonly System.Lazy<ExcelSheet<BaseParam>> _baseParamSheet = new(() => Module.GetSheet<BaseParam>());
    private static readonly System.Lazy<ExcelSheet<WKSMissionToDoEvalutionRefin>> _wksMissionSheet = new(() => Module.GetSheet<WKSMissionToDoEvalutionRefin>());
    private static readonly System.Lazy<ExcelSheet<WKSCosmoToolClass>> _wksCosmoToolClassSheet = new(() => Module.GetSheet<WKSCosmoToolClass>());
    private static readonly System.Lazy<ExcelSheet<GathererCrafterLvAdjustTable>> _gathererCrafterLvAdjustTableSheet = new(() => Module.GetSheet<GathererCrafterLvAdjustTable>());

    public static ExcelSheet<Item> ItemSheetEnglish => _itemSheetEnglish.Value;
    public static ExcelSheet<Level> LevelSheet => _levelSheet.Value;
    public static ExcelSheet<Quest> QuestSheet => _questSheet.Value;
    public static ExcelSheet<Materia> MateriaSheet => _materiaSheet.Value;
    public static ExcelSheet<BaseParam> BaseParamSheet => _baseParamSheet.Value;
    public static ExcelSheet<WKSMissionToDoEvalutionRefin> WKSMissionToDoEvalutionRefinSheet => _wksMissionSheet.Value;
    public static ExcelSheet<WKSCosmoToolClass> WKSCosmoToolClassSheet => _wksCosmoToolClassSheet.Value;
    public static ExcelSheet<GathererCrafterLvAdjustTable> GathererCrafterLvAdjustTableSheet => _gathererCrafterLvAdjustTableSheet.Value;
}

using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Artificer.Plugin;

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
    private static readonly System.Lazy<ExcelSheet<WKSMissionUnit>> _wksMissionUnitSheet = new(() => Module.GetSheet<WKSMissionUnit>());
    private static readonly System.Lazy<ExcelSheet<GathererCrafterLvAdjustTable>> _gathererCrafterLvAdjustTableSheet = new(() => Module.GetSheet<GathererCrafterLvAdjustTable>());
    private static readonly System.Lazy<ExcelSheet<GatheringItem>> _gatheringItemSheet = new(() => Module.GetSheet<GatheringItem>());
    private static readonly System.Lazy<ExcelSheet<GatheringPointBase>> _gatheringPointBaseSheet = new(() => Module.GetSheet<GatheringPointBase>());
    private static readonly System.Lazy<ExcelSheet<GatheringPoint>> _gatheringPointSheet = new(() => Module.GetSheet<GatheringPoint>());
    private static readonly System.Lazy<ExcelSheet<TerritoryType>> _territoryTypeSheet = new(() => Module.GetSheet<TerritoryType>());
    private static readonly System.Lazy<ExcelSheet<Aetheryte>> _aetheryteSheet = new(() => Module.GetSheet<Aetheryte>());
    private static readonly System.Lazy<ExcelSheet<Map>> _mapSheet = new(() => Module.GetSheet<Map>());
    private static readonly System.Lazy<ExcelSheet<ExportedGatheringPoint>> _exportedGatheringPointSheet = new(() => Module.GetSheet<ExportedGatheringPoint>());

    public static ExcelSheet<Item> ItemSheetEnglish => _itemSheetEnglish.Value;
    public static ExcelSheet<Level> LevelSheet => _levelSheet.Value;
    public static ExcelSheet<Quest> QuestSheet => _questSheet.Value;
    public static ExcelSheet<Materia> MateriaSheet => _materiaSheet.Value;
    public static ExcelSheet<BaseParam> BaseParamSheet => _baseParamSheet.Value;
    public static ExcelSheet<WKSMissionToDoEvalutionRefin> WKSMissionToDoEvalutionRefinSheet => _wksMissionSheet.Value;
    public static ExcelSheet<WKSCosmoToolClass> WKSCosmoToolClassSheet => _wksCosmoToolClassSheet.Value;
    public static ExcelSheet<WKSMissionUnit> WKSMissionUnitSheet => _wksMissionUnitSheet.Value;
    public static ExcelSheet<GathererCrafterLvAdjustTable> GathererCrafterLvAdjustTableSheet => _gathererCrafterLvAdjustTableSheet.Value;
    public static ExcelSheet<GatheringItem> GatheringItemSheet => _gatheringItemSheet.Value;
    public static ExcelSheet<GatheringPointBase> GatheringPointBaseSheet => _gatheringPointBaseSheet.Value;
    public static ExcelSheet<GatheringPoint> GatheringPointSheet => _gatheringPointSheet.Value;
    public static ExcelSheet<TerritoryType> TerritoryTypeSheet => _territoryTypeSheet.Value;
    public static ExcelSheet<Aetheryte> AetheryteSheet => _aetheryteSheet.Value;
    public static ExcelSheet<Map> MapSheet => _mapSheet.Value;
    public static ExcelSheet<ExportedGatheringPoint> ExportedGatheringPointSheet => _exportedGatheringPointSheet.Value;
}

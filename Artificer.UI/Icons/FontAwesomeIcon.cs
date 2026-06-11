// Artificer.UI/Icons/FontAwesomeIcon.cs
namespace Artificer.Utils;

// Unicode codepoints for FontAwesome Free 6 Solid.
// Values match Dalamud.Interface.FontAwesomeIcon so glyphs render identically.
public enum FontAwesomeIcon : ushort
{
    None                = 0,
    Bars                = 0xf0c9,
    Book                = 0xf02d,
    Boxes               = 0xf468,
    ChartBar            = 0xf080,
    Check               = 0xf00c,
    CheckCircle         = 0xf058,
    ChevronDown         = 0xf078,
    ChevronLeft         = 0xf053,
    ChevronRight        = 0xf054,
    Circle              = 0xf111,
    Clipboard           = 0xf328,
    Cog                 = 0xf013,
    Dice                = 0xf522,
    Download            = 0xf019,
    Edit                = 0xf044,
    ExclamationCircle   = 0xf06a,
    ExclamationTriangle = 0xf071,
    Eye                 = 0xf06e,
    EyeSlash            = 0xf070,
    FileImport          = 0xf56f,
    Filter              = 0xf0b0,
    Flag                = 0xf024,
    FolderOpen          = 0xf07c,
    Globe               = 0xf0ac,
    Heart               = 0xf004,
    InfoCircle          = 0xf05a,
    List                = 0xf03a,
    ListAlt             = 0xf022,
    LocationArrow       = 0xf124,
    Paste               = 0xf0ea,
    PencilAlt           = 0xf303,
    Plus                = 0xf067,
    PlusCircle          = 0xf055,
    Redo                = 0xf01e,
    Save                = 0xf0c7,
    Search              = 0xf002,
    Star                = 0xf005,
    Sync                = 0xf021,
    Times               = 0xf00d,
    Tools               = 0xf7d9,
    Trash               = 0xf1f8,
    Undo                = 0xf0e2,
    Upload              = 0xf093,
    WindowMinimize      = 0xf2d1,
}

public static class FontAwesomeIconExtensions
{
    public static string ToIconString(this FontAwesomeIcon icon) => ((char)icon).ToString();
}

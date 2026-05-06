using Microsoft.Maui.Hosting;

namespace Ben.Services;

public sealed class AppFontOption
{
    public required string FileName { get; init; }
    public required string Alias { get; init; }
    public required string DisplayName { get; init; }
}

public static class AppFontCatalog
{
    public const string UserFontResourceKey = "UserHandwritingFont";

    private static readonly IReadOnlyList<AppFontOption> AllFonts = new List<AppFontOption>
    {
        new() { FileName = "ComicRelief-Regular.ttf",       Alias = "ComicRelief",              DisplayName = "Comic Relief" },
        new() { FileName = "Delius-Regular.ttf",            Alias = "Delius",                   DisplayName = "Delius" },
        new() { FileName = "IBMPlexSans-Medium.ttf",        Alias = "IBMPlexSans-Medium",       DisplayName = "Plex Sans" },
        new() { FileName = "Inter-Regular.ttf",             Alias = "Inter-Regular",            DisplayName = "Inter" },
        new() { FileName = "Lato-Regular.ttf",              Alias = "Lato-Regular",             DisplayName = "Lato" },
        new() { FileName = "Montserrat-SemiBold.ttf",       Alias = "Montserrat-SemiBold",      DisplayName = "Montserrat" },
        new() { FileName = "NotoSerif-Regular.ttf",         Alias = "NotoSerif-Regular",        DisplayName = "Noto Serif" },
        new() { FileName = "OpenSans-Regular.ttf",          Alias = "OpenSans",                 DisplayName = "Open Sans" },
        new() { FileName = "PatrickHand-Regular.ttf",       Alias = "PatrickHand",              DisplayName = "Patrick Hand" },
        new() { FileName = "Roboto-Regular.ttf",            Alias = "Roboto",                   DisplayName = "Roboto" },
        new() { FileName = "SourceSerifPro-SemiBold.ttf",   Alias = "SourceSerifPro-SemiBold",  DisplayName = "Source Serif" },
    };

    public static IReadOnlyList<AppFontOption> UserSelectableFonts => AllFonts;

    public static void ConfigureFonts(IFontCollection fonts)
    {
        foreach (var font in AllFonts)
        {
            fonts.AddFont(font.FileName, font.Alias);
        }
    }
}

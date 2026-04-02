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
        new() { FileName = "Caveat-Regular.ttf", Alias = "Caveat", DisplayName = "Caveat" },
        new() { FileName = "ComicRelief-Regular.ttf", Alias = "ComicRelief", DisplayName = "Comic Relief" },
        new() { FileName = "Delius-Regular.ttf", Alias = "Delius", DisplayName = "Delius" },
        new() { FileName = "LovedByTheKing-Regular.ttf", Alias = "LovedByTheKing", DisplayName = "Loved By The King" },
        new() { FileName = "OpenSans-Regular.ttf", Alias = "OpenSans", DisplayName = "Open Sans" },
        new() { FileName = "PatrickHand-Regular.ttf", Alias = "PatrickHand", DisplayName = "Patrick Hand" },
        new() { FileName = "Roboto-Regular.ttf", Alias = "Roboto", DisplayName = "Roboto" }
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

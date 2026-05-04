using MudBlazor;
using MudBlazor.Utilities;

namespace GoldBank.Admin.Services;

public static class GoldTheme
{
    public static readonly MudTheme Instance = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = new MudColor("#C9A227"),
            PrimaryDarken = "#8B6F1A",
            PrimaryLighten = "#E5C76B",
            Secondary = new MudColor("#3D2E12"),
            SecondaryDarken = "#1F1A10",
            SecondaryLighten = "#6B4F1D",
            Tertiary = new MudColor("#FFB300"),
            AppbarBackground = new MudColor("#C9A227"),
            AppbarText = new MudColor("#1A1308"),
            DrawerBackground = new MudColor("#FFFDF6"),
            DrawerText = new MudColor("#1F1A10"),
            Background = new MudColor("#FFFDF6"),
            Surface = new MudColor("#FFFFFF"),
            ActionDefault = new MudColor("#8B6F1A"),
            LinesDefault = new MudColor("#E5C76B"),
        },
        PaletteDark = new PaletteDark
        {
            Primary = new MudColor("#D4AF37"),
            PrimaryDarken = "#8B6F1A",
            PrimaryLighten = "#E5C76B",
            Secondary = new MudColor("#FFB300"),
            SecondaryDarken = "#C68400",
            SecondaryLighten = "#FFD54F",
            Tertiary = new MudColor("#E5C76B"),
            AppbarBackground = new MudColor("#1F1A10"),
            AppbarText = new MudColor("#F5E9C7"),
            DrawerBackground = new MudColor("#15120A"),
            DrawerText = new MudColor("#F5E9C7"),
            Background = new MudColor("#15120A"),
            Surface = new MudColor("#1F1A10"),
            TextPrimary = new MudColor("#F5E9C7"),
            TextSecondary = new MudColor("#C9A227"),
            ActionDefault = new MudColor("#E5C76B"),
            LinesDefault = new MudColor("#3D2E12"),
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "6px",
        },
    };
}

namespace PDV.WebUI.Theme;

using MudBlazor;

public static class PremiumTheme
{
    public static MudTheme Theme => new MudTheme()
    {
        PaletteDark = new PaletteDark()
        {
            Primary = "#7c4dff",          // Púrpura vibrante
            Secondary = "#6366f1",        // Índigo
            Background = "#080a14",       // Fondo base muy oscuro
            Surface = "#111428",          // Superficie de tarjetas/paneles
            AppbarBackground = "#080a14",
            DrawerBackground = "#080a14",
            TextPrimary = "#ffffff",
            TextSecondary = "rgba(255, 255, 255, 0.6)",
            ActionDefault = "rgba(255, 255, 255, 0.8)",
            ActionDisabled = "rgba(255, 255, 255, 0.35)",
            Divider = "rgba(255, 255, 255, 0.08)",
            Success = "#69f0ae",
            Error = "#ff5252",
            Warning = "#ffb74d",
            Info = "#4fc3f7"
        }
    };
}

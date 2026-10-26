using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace NaRyby_mac
{
    public class App : Application
    {
        public override void Initialize()
        {
            Name = "NaRyby";
            // Motyw jasny albo ciemny – zgodnie z ustawieniem systemu, dopóki użytkownik nie wybierze innego.
            RequestedThemeVariant = ThemeVariant.Default;
            Styles.Add(new FluentTheme());

            // Menu aplikacji na macOS (zamiast domyślnego „About Avalonia”).
            var about = new NativeMenuItem("O mnie");
            about.Click += async (s, e) =>
            {
                var desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                if (desktop != null && desktop.MainWindow != null)
                    await AboutWindow.ShowAbout(desktop.MainWindow);
            };
            var menu = new NativeMenu();
            menu.Items.Add(about);
            NativeMenu.SetMenu(this, menu);
        }

        /// <summary>Ustawia motyw: "light", "dark" albo null – zgodnie z systemem.</summary>
        public static void SetThemeMode(string mode)
        {
            Current.RequestedThemeVariant = mode == "dark" ? ThemeVariant.Dark
                : mode == "light" ? ThemeVariant.Light
                : ThemeVariant.Default;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Ui.ApplyTheme(ActualThemeVariant == ThemeVariant.Dark);
            ActualThemeVariantChanged += (s, e) => Ui.ApplyTheme(ActualThemeVariant == ThemeVariant.Dark);

            var desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (desktop != null) desktop.MainWindow = new MainWindow();
            base.OnFrameworkInitializationCompleted();
        }
    }
}

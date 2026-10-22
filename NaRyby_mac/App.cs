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
            // Kolory tabeli i wykresu są dobrane do jasnego tła, więc motyw jest zawsze jasny.
            RequestedThemeVariant = ThemeVariant.Light;
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

        public override void OnFrameworkInitializationCompleted()
        {
            var desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (desktop != null) desktop.MainWindow = new MainWindow();
            base.OnFrameworkInitializationCompleted();
        }
    }
}

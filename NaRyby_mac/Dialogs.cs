using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NaRyby_win;

namespace NaRyby_mac
{
    /// <summary>Proste okna komunikatów (Avalonia nie ma odpowiednika MessageBox).</summary>
    public static class Dialogs
    {
        public static Task Info(Window owner, string text)
        {
            return Show(owner, text, false);
        }

        /// <summary>Pytanie Tak/Nie; zamknięcie okna oznacza „Nie”.</summary>
        public static Task<bool> Ask(Window owner, string text)
        {
            return Show(owner, text, true);
        }

        private static Task<bool> Show(Window owner, string text, bool question)
        {
            var window = new Window
            {
                Title = "NaRyby",
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                MinWidth = 340,
                MaxWidth = 520,
                FontSize = 13
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            if (question)
            {
                var no = MakeButton("Nie");
                no.IsCancel = true;
                no.Click += (s, e) => window.Close(false);
                buttons.Children.Add(no);
            }
            var ok = MakeButton(question ? "Tak" : "OK");
            ok.IsDefault = true;
            if (!question) ok.IsCancel = true;
            ok.Click += (s, e) => window.Close(true);
            buttons.Children.Add(ok);

            var panel = new StackPanel { Margin = new Thickness(20), Spacing = 18 };
            panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(buttons);
            window.Content = panel;

            return window.ShowDialog<bool>(owner);
        }

        private static Button MakeButton(string text)
        {
            return new Button { Content = text, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        }
    }

    /// <summary>
    /// Okno „O mnie” – informacje o programie, autorze i źródle danych.
    /// </summary>
    public class AboutWindow : Window
    {
        private const string WeatherUrl = "https://open-meteo.com";

        private AboutWindow()
        {
            Title = "O mnie";
            Width = 460;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Ui.Surface;
            FontSize = 13;

            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            try
            {
                header.Children.Add(new Image
                {
                    Width = 64,
                    Height = 64,
                    Source = new Bitmap(AssetLoader.Open(new Uri("avares://NaRyby/Assets/icon.png")))
                });
            }
            catch (Exception)
            {
                // Brak ikony nie przeszkadza w działaniu programu.
            }
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(new TextBlock { Text = "NaRyby", FontSize = 26, FontWeight = FontWeight.Bold, Foreground = Ui.SteelBlue });
            titles.Children.Add(new TextBlock { Text = "kiedy jechać na ryby?", FontSize = 14, Foreground = Ui.DimGray });
            header.Children.Add(titles);

            var ok = new Button { Content = "OK", IsDefault = true, IsCancel = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            ok.Click += (s, e) => Close();

            var panel = new StackPanel { Margin = new Thickness(22), Spacing = 8 };
            panel.Children.Add(header);
            panel.Children.Add(new TextBlock
            {
                Text = "Prognoza brań na 7 dni dla wędkarzy – na podstawie pogody, ciśnienia, " +
                       "wiatru, fazy księżyca i wybranego gatunku ryby.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 4)
            });
            panel.Children.Add(new TextBlock { Text = "Autor: Filip Dąbrowski", FontWeight = FontWeight.Bold });
            panel.Children.Add(MakeLink("Kod źródłowy: ", "github.com/filipdab/NaRyby_win", Reports.RepoUrl));
            panel.Children.Add(MakeLink("Dane pogodowe: ", "Open-Meteo.com", WeatherUrl));
            panel.Children.Add(new TextBlock { Text = "Licencja MIT · © 2026 Filip Dąbrowski", Foreground = Ui.DimGray });
            panel.Children.Add(new TextBlock
            {
                Text = "Prognoza ma charakter orientacyjny – ryby nie zawsze czytają prognozy. Połamania kija!",
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyle.Italic,
                FontSize = 12,
                Foreground = Ui.DimGray,
                Margin = new Thickness(0, 6, 0, 6)
            });
            panel.Children.Add(ok);
            Content = panel;
        }

        private Control MakeLink(string prefix, string linkText, string url)
        {
            var link = new TextBlock
            {
                Text = linkText,
                Foreground = Ui.SteelBlue,
                TextDecorations = TextDecorations.Underline,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            link.PointerPressed += async (s, e) => await Launcher.LaunchUriAsync(new Uri(url));

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = prefix });
            row.Children.Add(link);
            return row;
        }

        public static Task ShowAbout(Window owner)
        {
            return new AboutWindow().ShowDialog(owner);
        }
    }

    /// <summary>
    /// Okno wyboru, gdy wyszukiwanie zwróciło kilka miejscowości o tej samej nazwie.
    /// </summary>
    public class CityPickerWindow : Window
    {
        private readonly ListBox _list;

        private CityPickerWindow(List<City> cities)
        {
            Title = "Wybierz miejscowość";
            Width = 480;
            Height = 360;
            CanResize = false;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontSize = 13;

            _list = new ListBox { ItemsSource = cities.Select(c => c.Description).ToList() };
            _list.SelectedIndex = 0;
            _list.DoubleTapped += (s, e) => Close((int?)_list.SelectedIndex);

            var ok = new Button { Content = "Dodaj", IsDefault = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
            ok.Click += (s, e) => Close((int?)_list.SelectedIndex);
            var cancel = new Button { Content = "Anuluj", IsCancel = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
            cancel.Click += (s, e) => Close(null);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);

            var label = new TextBlock { Text = "Znaleziono kilka miejscowości – wybierz właściwą:", Margin = new Thickness(0, 0, 0, 8) };

            var root = new DockPanel { Margin = new Thickness(14) };
            DockPanel.SetDock(label, Dock.Top);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(label);
            root.Children.Add(buttons);
            root.Children.Add(new Border
            {
                BorderBrush = Ui.BorderBrush,
                BorderThickness = new Thickness(1),
                Child = _list
            });
            Content = root;
        }

        public static async Task<City> Pick(Window owner, List<City> cities)
        {
            int? index = await new CityPickerWindow(cities).ShowDialog<int?>(owner);
            return index.HasValue && index.Value >= 0 && index.Value < cities.Count ? cities[index.Value] : null;
        }
    }
}

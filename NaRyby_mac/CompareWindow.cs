using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using NaRyby_win;

namespace NaRyby_mac
{
    /// <summary>
    /// Porównanie łowisk: oceny na 7 dni dla wszystkich zapisanych miejscowości w jednej tabeli.
    /// </summary>
    public class CompareWindow : Window
    {
        private readonly WeatherService _weather;
        private readonly List<City> _cities;
        private readonly Species _species;
        private readonly DockPanel _table;
        private readonly TextBlock _status;

        private CompareWindow(WeatherService weather, List<City> cities, Species species)
        {
            _weather = weather;
            _cities = cities;
            _species = species;

            Title = "Porównanie łowisk – " + species.Name.ToLower();
            Width = 1000;
            Height = 440;
            MinWidth = 720;
            MinHeight = 300;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Ui.WindowBackground;
            FontSize = 13;

            var info = new TextBlock
            {
                Text = "Ocena brań (0–100) dla każdego dnia. Kliknij dwukrotnie miejscowość, aby otworzyć jej prognozę.",
                Margin = new Thickness(0, 0, 0, 8)
            };
            _status = new TextBlock
            {
                Text = "Pobieram prognozy dla " + cities.Count + " miejscowości...",
                Foreground = Ui.DimGray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            };
            _table = new DockPanel();

            var root = new DockPanel { Margin = new Thickness(14) };
            DockPanel.SetDock(info, Dock.Top);
            DockPanel.SetDock(_status, Dock.Bottom);
            root.Children.Add(info);
            root.Children.Add(_status);
            root.Children.Add(new Border
            {
                Background = Ui.Surface,
                BorderBrush = Ui.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Child = _table
            });
            Content = root;
        }

        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            Cursor = new Cursor(StandardCursorType.Wait);
            var results = await Reports.CompareSpotsAsync(_weather, _cities, _species);
            Cursor = Cursor.Default;
            Fill(results);
        }

        private void Fill(List<SpotResult> results)
        {
            var first = results.FirstOrDefault(r => r.Forecast != null);
            if (first == null)
            {
                _status.Text = "Nie udało się pobrać żadnej prognozy. Sprawdź połączenie z internetem.";
                return;
            }

            var now = first.Forecast.Current.Time;
            var dates = first.Forecast.Days.Select(d => d.Date).ToList();
            string columns = "160*," + string.Join(",", dates.Select(d => "55*")) + ",190*";

            var header = new Grid { ColumnDefinitions = new ColumnDefinitions(columns), Background = Ui.HeaderBackground, Height = 30 };
            var titles = new List<string> { "Miejscowość" };
            titles.AddRange(dates.Select(d => Reports.ShortDayName(d, now)));
            titles.Add("Najlepiej");
            for (int i = 0; i < titles.Count; i++)
            {
                var cell = new TextBlock
                {
                    Text = titles[i],
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(i == 0 ? 10 : 6, 0, 4, 0),
                    HorizontalAlignment = i > 0 && i <= dates.Count ? HorizontalAlignment.Center : HorizontalAlignment.Left
                };
                Grid.SetColumn(cell, i);
                header.Children.Add(cell);
            }

            var list = new ListBox { Background = Ui.Surface, BorderThickness = new Thickness(0), SelectionMode = SelectionMode.Single };
            foreach (var r in results) list.Items.Add(SpotRow(r, dates, now, columns));
            list.DoubleTapped += (s, e) => Choose(list);
            list.KeyDown += (s, e) =>
            {
                if (e.Key != Key.Enter) return;
                e.Handled = true;
                Choose(list);
            };

            DockPanel.SetDock(header, Dock.Top);
            _table.Children.Add(header);
            _table.Children.Add(list);

            var top = results.First(r => r.Forecast != null);
            int errors = results.Count(r => r.Forecast == null);
            _status.Text = top.Best.Score > 0
                ? string.Format("Najlepsze łowisko w tym tygodniu: {0} – {1}, {2} ({3}/100).",
                    top.City.Name, Reports.DayName(top.Best.Date, now), top.Best.BestWindowText, top.Best.Score)
                : string.Format("Okres ochronny {0} – w tym tygodniu nie wolno go łowić.", _species.Genitive);
            if (errors > 0) _status.Text += string.Format("   Nie udało się pobrać: {0}.", errors);
        }

        private static ListBoxItem SpotRow(SpotResult r, List<DateTime> dates, DateTime now, string columns)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions(columns), Height = 30 };
            row.Children.Add(new TextBlock
            {
                Text = r.City.Name,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(6, 0, 4, 0)
            });

            string best;
            if (r.Forecast == null)
            {
                best = "błąd pobierania";
            }
            else
            {
                for (int i = 0; i < dates.Count; i++)
                {
                    var day = r.DayAt(dates[i]);
                    if (day == null) continue;
                    var cell = new Border
                    {
                        Background = Ui.Brush(FishingAdvisor.ScoreColor(day.Score)),
                        Margin = new Thickness(2, 3),
                        CornerRadius = new CornerRadius(3),
                        Child = new TextBlock
                        {
                            Text = day.Score.ToString(),
                            Foreground = Brushes.White,
                            FontWeight = FontWeight.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    };
                    ToolTip.SetTip(cell, string.Format("{0}: {1}, najlepiej {2}",
                        Reports.DayName(day.Date, now), FishingAdvisor.RatingText(day.Score), day.BestWindowText));
                    Grid.SetColumn(cell, i + 1);
                    row.Children.Add(cell);
                }
                best = r.Best.Score > 0 ? string.Format("{0}, {1}", Reports.ShortDayName(r.Best.Date, now), r.Best.BestWindowText) : "–";
            }

            var bestText = new TextBlock
            {
                Text = best,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(6, 0, 4, 0)
            };
            if (r.Error != null) ToolTip.SetTip(bestText, r.Error);
            Grid.SetColumn(bestText, dates.Count + 1);
            row.Children.Add(bestText);

            return new ListBoxItem { Content = row, Tag = r.City, Padding = new Thickness(4, 0), MinHeight = 0 };
        }

        private void Choose(ListBox list)
        {
            var item = list.SelectedItem as ListBoxItem;
            if (item != null) Close(item.Tag as City);
        }

        /// <summary>Pokazuje porównanie; zwraca miejscowość wybraną dwuklikiem albo null.</summary>
        public static Task<City> ShowCompare(Window owner, WeatherService weather, List<City> cities, Species species)
        {
            return new CompareWindow(weather, cities, species).ShowDialog<City>(owner);
        }
    }

    /// <summary>
    /// Zestawienie okresów i wymiarów ochronnych wszystkich gatunków z zaznaczeniem, co jest dziś chronione.
    /// </summary>
    public class ProtectionWindow : Window
    {
        private const string Columns = "100*,110*,120*,200*";
        private static readonly IBrush ClosedBrush = Ui.Themed(Color.FromRgb(253, 226, 226), Color.FromRgb(92, 40, 40));
        private static readonly IBrush SoonBrush = Ui.Themed(Color.FromRgb(255, 243, 205), Color.FromRgb(84, 70, 30));

        private ProtectionWindow()
        {
            Title = "Okresy i wymiary ochronne";
            Width = 640;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Ui.WindowBackground;
            FontSize = 13;

            var table = new StackPanel();
            table.Children.Add(Row(new[] { "Gatunek", "Okres ochronny", "Wymiar ochronny", "Dziś" }, Ui.HeaderBackground, true));
            var today = DateTime.Today;
            foreach (var sp in Species.All.Where(sp => !sp.IsGeneral))
            {
                string status = Reports.ProtectionStatus(sp, today);
                bool closed = sp.IsClosed(today);
                IBrush back = closed ? ClosedBrush : status.Contains("ochrona") ? SoonBrush : Ui.Surface;
                table.Children.Add(Row(new[] { sp.Name, sp.ClosedText ?? "brak", Reports.MinLengthText(sp), status }, back, false, closed));
            }

            var ok = new Button { Content = "OK", IsDefault = true, IsCancel = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            ok.Click += (s, e) => Close();

            var panel = new StackPanel { Margin = new Thickness(14), Spacing = 10 };
            panel.Children.Add(new Border
            {
                BorderBrush = Ui.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Child = table
            });
            panel.Children.Add(new TextBlock { Text = Reports.ProtectionNote, TextWrapping = TextWrapping.Wrap, Foreground = Ui.DimGray, FontSize = 12 });
            panel.Children.Add(ok);
            Content = panel;
        }

        private static Grid Row(string[] values, IBrush background, bool header, bool boldStatus = false)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions(Columns), Height = 30, Background = background };
            for (int i = 0; i < values.Length; i++)
            {
                var cell = new TextBlock
                {
                    Text = values[i],
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 4, 0),
                    FontWeight = header || (boldStatus && i == values.Length - 1) ? FontWeight.Bold : FontWeight.Normal
                };
                Grid.SetColumn(cell, i);
                row.Children.Add(cell);
            }
            return row;
        }

        public static Task ShowProtection(Window owner)
        {
            return new ProtectionWindow().ShowDialog(owner);
        }
    }
}

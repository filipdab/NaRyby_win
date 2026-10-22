using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using NaRyby_win;

namespace NaRyby_mac
{
    public class MainWindow : Window
    {
        private static readonly CultureInfo Polish = new CultureInfo("pl-PL");

        // Szerokości kolumn tabeli dni (jak FillWeight w wersji Windows).
        private const string DayColumns = "150*,90*,55*,95*,150*,110*,90*,100*,100*,100*";

        private readonly WeatherService _weather = new WeatherService();
        private readonly CityStore _store = new CityStore();
        private Forecast _forecast;
        private bool _suppressCityChange;
        private bool _busy;

        private ComboBox cmbCity;
        private ComboBox cmbSpecies;
        private Button btnRefresh;
        private Button btnRemove;
        private TextBox txtSearch;
        private Button btnAdd;
        private Button btnCompare;
        private Button btnShare;
        private Border bannerRecommendation;
        private TextBlock lblRecommendation;
        private TextBlock lblCurrent;
        private ListBox grid;
        private TextBlock lblChart;
        private ScoreChart chart;
        private SelectableTextBlock txtDayDetails;
        private TabItem tabTips;
        private SelectableTextBlock txtTips;
        private TextBlock lblStatus;
        private DispatcherTimer refreshTimer;

        public MainWindow()
        {
            BuildUi();
            LoadCities();
        }

        #region Budowa interfejsu

        private void BuildUi()
        {
            Title = "NaRyby – kiedy jechać na ryby?";
            Width = 1250;
            Height = 820;
            MinWidth = 900;
            MinHeight = 650;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Ui.WindowBackground;
            FontSize = 13;
            try
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://NaRyby/Assets/icon.png")));
            }
            catch (Exception)
            {
                // Brak ikony nie przeszkadza w działaniu programu.
            }

            // Pasek wyboru miejscowości.
            var top = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 8, 8, 4) };
            cmbCity = new ComboBox { Width = 230, Margin = new Thickness(3) };
            cmbCity.SelectionChanged += async (s, e) => { if (!_suppressCityChange) await LoadForecastAsync(); };
            btnRefresh = MakeButton("⟳ Odśwież");
            ToolTip.SetTip(btnRefresh, "Odśwież prognozę (⌘R)");
            btnRefresh.Click += async (s, e) => await LoadForecastAsync();
            btnRemove = MakeButton("Usuń z listy");
            btnRemove.Click += async (s, e) => await RemoveCityAsync();
            cmbSpecies = new ComboBox { Width = 180, Margin = new Thickness(3) };
            cmbSpecies.ItemsSource = Species.All;
            cmbSpecies.SelectedItem = Species.All.FirstOrDefault(sp => sp.Name == "Leszcz") ?? Species.All[0];
            cmbSpecies.SelectionChanged += (s, e) => OnSpeciesChanged();
            txtSearch = new TextBox { Width = 180, Margin = new Thickness(3), Watermark = "np. Zegrze, Mikołajki" };
            // Tunelowanie, żeby Enter dotarł do nas zanim obsłuży go TextBox.
            txtSearch.AddHandler(KeyDownEvent, new EventHandler<KeyEventArgs>(TxtSearch_KeyDown), RoutingStrategies.Tunnel);
            btnAdd = MakeButton("Szukaj i dodaj");
            btnAdd.Click += async (s, e) => await AddCityAsync();
            btnCompare = MakeButton("Porównaj łowiska");
            btnCompare.Click += async (s, e) => await CompareSpotsAsync();
            var btnProtection = MakeButton("Okresy ochronne");
            btnProtection.Click += async (s, e) => await ProtectionWindow.ShowProtection(this);
            btnShare = MakeButton("Udostępnij ▾");
            var copyText = new MenuItem { Header = "Kopiuj prognozę jako tekst" };
            copyText.Click += async (s, e) => await CopyForecastTextAsync();
            var saveImage = new MenuItem { Header = "Zapisz obraz okna (PNG)..." };
            saveImage.Click += async (s, e) => await SaveWindowImageAsync();
            var shareMenu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
            shareMenu.Items.Add(copyText);
            shareMenu.Items.Add(saveImage);
            btnShare.Flyout = shareMenu;
            var btnAbout = MakeButton("ⓘ O mnie");
            btnAbout.Click += async (s, e) => await AboutWindow.ShowAbout(this);

            top.Children.Add(MakeLabel("Miejscowość:"));
            top.Children.Add(cmbCity);
            top.Children.Add(btnRemove);
            top.Children.Add(btnCompare);
            top.Children.Add(new Control { Width = 12 });
            top.Children.Add(MakeLabel("Dodaj miejscowość:"));
            top.Children.Add(txtSearch);
            top.Children.Add(btnAdd);
            top.Children.Add(new Control { Width = 20 });
            top.Children.Add(MakeLabel("Gatunek ryby:"));
            top.Children.Add(cmbSpecies);
            top.Children.Add(btnRefresh);
            top.Children.Add(new Control { Width = 12 });
            top.Children.Add(btnProtection);
            top.Children.Add(btnShare);
            top.Children.Add(btnAbout);

            // Baner z rekomendacją.
            lblRecommendation = new TextBlock
            {
                Text = "Wybierz miejscowość, aby sprawdzić warunki.",
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            bannerRecommendation = new Border
            {
                Height = 68,
                Padding = new Thickness(16, 6),
                Background = Ui.SteelBlue,
                Child = lblRecommendation
            };

            // Środek: bieżące warunki + tabela dni.
            lblCurrent = new TextBlock { FontSize = 13.5, TextWrapping = TextWrapping.Wrap, LineHeight = 20 };
            var grpCurrent = Group("Warunki teraz", new ScrollViewer { Content = lblCurrent, Padding = new Thickness(10, 4, 10, 10) });

            var daysPanel = new DockPanel();
            var header = DayHeader();
            DockPanel.SetDock(header, Dock.Top);
            daysPanel.Children.Add(header);
            grid = new ListBox { Background = Brushes.White, BorderThickness = new Thickness(0), SelectionMode = SelectionMode.Single };
            grid.SelectionChanged += (s, e) => ShowSelectedDay();
            daysPanel.Children.Add(grid);
            var grpDays = Group("Prognoza brań na 7 dni (kliknij dzień, aby zobaczyć szczegóły)", daysPanel);

            var middle = new Grid { ColumnDefinitions = new ColumnDefinitions("290,8,*"), Margin = new Thickness(8, 8, 8, 0) };
            Grid.SetColumn(grpDays, 2);
            middle.Children.Add(grpCurrent);
            middle.Children.Add(grpDays);

            // Dół: wykres godzinowy + uzasadnienie.
            chart = new ScoreChart { Margin = new Thickness(6, 0, 6, 6) };
            TextBlock chartHeader;
            var grpChart = Group("Prognoza godzinowa", chart, out chartHeader);
            lblChart = chartHeader;

            txtDayDetails = MakeReadOnlyText();
            txtTips = MakeReadOnlyText();
            var tabs = new TabControl { Padding = new Thickness(0) };
            tabs.Items.Add(MakeTab("Ocena dnia", txtDayDetails));
            tabTips = MakeTab("Porady", txtTips);
            tabs.Items.Add(tabTips);
            var tabsBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = Ui.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Child = tabs
            };

            var bottom = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,440"), Height = 308, Margin = new Thickness(8, 8, 8, 0) };
            Grid.SetColumn(tabsBorder, 2);
            bottom.Children.Add(grpChart);
            bottom.Children.Add(tabsBorder);

            lblStatus = new TextBlock { Text = "Dane pogodowe: Open-Meteo.com", Foreground = Ui.DimGray, FontSize = 12 };
            var status = new Border
            {
                Padding = new Thickness(12, 5),
                Margin = new Thickness(0, 8, 0, 0),
                BorderBrush = Ui.BorderBrush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = lblStatus
            };

            var root = new DockPanel();
            DockPanel.SetDock(top, Dock.Top);
            DockPanel.SetDock(bannerRecommendation, Dock.Top);
            DockPanel.SetDock(status, Dock.Bottom);
            DockPanel.SetDock(bottom, Dock.Bottom);
            root.Children.Add(top);
            root.Children.Add(bannerRecommendation);
            root.Children.Add(status);
            root.Children.Add(bottom);
            root.Children.Add(middle);
            Content = root;

            // ⌘R / Ctrl+R / F5 – odświeżenie prognozy, F1 – okno „O mnie”.
            KeyDown += async (s, e) =>
            {
                if (e.Key == Key.F1)
                {
                    e.Handled = true;
                    await AboutWindow.ShowAbout(this);
                    return;
                }
                bool refresh = e.Key == Key.F5 ||
                               (e.Key == Key.R && (e.KeyModifiers & (KeyModifiers.Meta | KeyModifiers.Control)) != 0);
                if (!refresh) return;
                e.Handled = true;
                await LoadForecastAsync();
            };

            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            refreshTimer.Tick += async (s, e) => await LoadForecastAsync();
            refreshTimer.Start();
        }

        private static Border Group(string title, Control content)
        {
            TextBlock header;
            return Group(title, content, out header);
        }

        /// <summary>Ramka z nagłówkiem (odpowiednik GroupBox).</summary>
        private static Border Group(string title, Control content, out TextBlock header)
        {
            header = new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, Margin = new Thickness(10, 7, 10, 6) };
            var panel = new DockPanel();
            DockPanel.SetDock(header, Dock.Top);
            panel.Children.Add(header);
            panel.Children.Add(content);
            return new Border
            {
                Background = Brushes.White,
                BorderBrush = Ui.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Child = panel
            };
        }

        private static TabItem MakeTab(string header, SelectableTextBlock text)
        {
            return new TabItem
            {
                Header = header,
                FontSize = 14,
                MinHeight = 34,
                Padding = new Thickness(12, 4),
                Content = new ScrollViewer { Content = text, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }
            };
        }

        private static SelectableTextBlock MakeReadOnlyText()
        {
            return new SelectableTextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                LineHeight = 19,
                Margin = new Thickness(12, 8, 12, 12)
            };
        }

        private static Button MakeButton(string text)
        {
            return new Button { Content = text, Margin = new Thickness(3), VerticalAlignment = VerticalAlignment.Center };
        }

        private static TextBlock MakeLabel(string text)
        {
            return new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 3, 4, 3) };
        }

        private static Grid DayHeader()
        {
            var header = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions(DayColumns),
                Background = Ui.HeaderBackground,
                Height = 30
            };
            string[] titles = { "Dzień", "Ocena", "Wynik", "Najlepsza pora", "Na co łowić", "Temp. powietrza", "Temp. wody", "Wiatr max", "Opady", "Ciśnienie" };
            for (int i = 0; i < titles.Length; i++)
            {
                var cell = new TextBlock
                {
                    Text = titles[i],
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(i == 0 ? 10 : 6, 0, 4, 0),
                    HorizontalAlignment = i == 2 ? HorizontalAlignment.Center : HorizontalAlignment.Left
                };
                Grid.SetColumn(cell, i);
                header.Children.Add(cell);
            }
            return header;
        }

        private static ListBoxItem DayRow(DayData d, string[] values, bool isBest)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions(DayColumns), Height = 30 };
            for (int i = 0; i < values.Length; i++)
            {
                Control cell;
                if (i == 2)
                {
                    // Kolumna „Wynik” – liczba na kolorowym tle.
                    cell = new Border
                    {
                        Background = Ui.Brush(FishingAdvisor.ScoreColor(d.Score)),
                        Margin = new Thickness(2, 3),
                        CornerRadius = new CornerRadius(3),
                        Child = new TextBlock
                        {
                            Text = values[i],
                            Foreground = Brushes.White,
                            FontWeight = FontWeight.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    };
                }
                else
                {
                    var text = new TextBlock
                    {
                        Text = values[i],
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Margin = new Thickness(6, 0, 4, 0)
                    };
                    if (i == 1)
                    {
                        text.Foreground = Ui.Gold;
                        text.FontSize = 15;
                    }
                    if (i == 0 && isBest) text.FontWeight = FontWeight.SemiBold;
                    ToolTip.SetTip(text, values[i]);
                    cell = text;
                }
                Grid.SetColumn(cell, i);
                row.Children.Add(cell);
            }

            var item = new ListBoxItem { Content = row, Tag = d, Padding = new Thickness(4, 0), MinHeight = 0 };
            if (isBest) item.Background = Ui.Brush(FishingAdvisor.LightScoreColor(d.Score));
            return item;
        }

        #endregion

        #region Miejscowości

        private void LoadCities()
        {
            _store.Load();
            var lastSpecies = Species.All.FirstOrDefault(sp => sp.Name == _store.LastSpecies);
            if (lastSpecies != null) cmbSpecies.SelectedItem = lastSpecies;
            RefreshCityList(_store.Cities.FirstOrDefault(c => c.Name == _store.LastCity) ?? _store.Cities.FirstOrDefault());
        }

        private void RefreshCityList(City select)
        {
            _suppressCityChange = true;
            cmbCity.ItemsSource = _store.Cities.ToList();
            if (select != null) cmbCity.SelectedItem = select;
            _suppressCityChange = false;
        }

        private async Task AddCityAsync()
        {
            if (_busy) return;
            string query = (txtSearch.Text ?? "").Trim();
            if (query.Length < 2) return;

            List<City> found;
            SetBusy(true, "Szukam miejscowości „" + query + "”...");
            try
            {
                found = await _weather.SearchCitiesAsync(query);
            }
            catch (Exception ex)
            {
                SetBusy(false, "Błąd wyszukiwania.");
                await Dialogs.Info(this, "Nie udało się wyszukać miejscowości:\n" + ex.Message);
                return;
            }
            SetBusy(false, "Dane pogodowe: Open-Meteo.com");

            if (found.Count == 0)
            {
                await Dialogs.Info(this, "Nie znaleziono miejscowości „" + query + "”.");
                return;
            }

            City chosen = found.Count == 1 ? found[0] : await CityPickerWindow.Pick(this, found);
            if (chosen == null) return;

            City added = _store.Add(chosen);
            txtSearch.Text = "";
            RefreshCityList(added);
            await LoadForecastAsync();
        }

        private async void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            await AddCityAsync();
        }

        private async Task RemoveCityAsync()
        {
            var city = cmbCity.SelectedItem as City;
            if (city == null || _busy) return;
            if (_store.Cities.Count <= 1)
            {
                await Dialogs.Info(this, "Na liście musi zostać co najmniej jedna miejscowość.");
                return;
            }
            if (!await Dialogs.Ask(this, "Usunąć „" + city.Name + "” z listy?")) return;

            _store.Remove(city);
            RefreshCityList(_store.Cities.First());
            await LoadForecastAsync();
        }

        #endregion

        #region Prognoza

        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            await LoadForecastAsync();
        }

        private async Task LoadForecastAsync()
        {
            var city = cmbCity.SelectedItem as City;
            if (city == null || _busy) return;

            SetBusy(true, "Pobieram prognozę dla: " + city.Name + "...");
            try
            {
                var forecast = await _weather.GetForecastAsync(city);
                FishingAdvisor.Analyze(forecast, SelectedSpecies);
                _forecast = forecast;

                _store.LastCity = city.Name;
                _store.Save();

                ShowForecast();
                SetBusy(false, string.Format("Dane pogodowe: Open-Meteo.com   ·   {0}   ·   Zaktualizowano: {1:HH:mm}   ·   Strefa czasowa: {2}",
                    city.Name, forecast.FetchedAt, forecast.TimeZone));
            }
            catch (Exception ex)
            {
                SetBusy(false, "Nie udało się pobrać prognozy.");
                await Dialogs.Info(this, "Nie udało się pobrać prognozy pogody.\nSprawdź połączenie z internetem.\n\n" + ex.Message);
            }
        }

        private void SetBusy(bool busy, string status)
        {
            _busy = busy;
            Cursor = busy ? new Cursor(StandardCursorType.Wait) : Cursor.Default;
            cmbCity.IsEnabled = !busy;
            cmbSpecies.IsEnabled = !busy;
            btnRefresh.IsEnabled = !busy;
            btnRemove.IsEnabled = !busy;
            btnAdd.IsEnabled = !busy;
            btnCompare.IsEnabled = !busy;
            btnShare.IsEnabled = !busy;
            txtSearch.IsEnabled = !busy;
            if (status != null) lblStatus.Text = status;
        }

        private void ShowForecast()
        {
            var f = _forecast;
            ShowCurrent(f);

            // Rekomendacja: dzień z najwyższą oceną (przy remisie wcześniejszy).
            var best = f.Days.OrderByDescending(d => d.Score).ThenBy(d => d.Date).First();
            var species = SelectedSpecies;
            bannerRecommendation.Background = new SolidColorBrush(Ui.Darken(Ui.ToAvalonia(FishingAdvisor.ScoreColor(best.Score)), 0.2));
            if (best.Score == 0 && species.IsClosed(best.Date))
            {
                var alternative = best.SpeciesRanking.First();
                lblRecommendation.Text = string.Format("Okres ochronny {0} ({1}) – nie wolno go teraz łowić.\nW tym tygodniu najlepiej zapowiada się: {2} ({3}/100).",
                    species.Genitive, species.ClosedText, alternative.Species.Name.ToLower(), alternative.Score);
            }
            else
            {
                lblRecommendation.Text = string.Format("Najlepiej jechać na {0}: {1}, {2}   —   {3} ({4}/100)\n{5}",
                    species.Accusative, DayName(best.Date, f.Current.Time), best.BestWindowText,
                    FishingAdvisor.RatingText(best.Score), best.Score,
                    best.Reasons.FirstOrDefault(r => r.StartsWith("✔")) ?? best.Reasons.FirstOrDefault());
            }

            grid.Items.Clear();
            foreach (var d in f.Days)
            {
                double dp = d.Hours.Last().Pressure - d.Hours.First().Pressure;
                string arrow = dp <= -2 ? "↓" : dp >= 2 ? "↑" : "→";
                string dayName = DayName(d.Date, f.Current.Time);
                grid.Items.Add(DayRow(d, new[]
                {
                    d == best ? "★ " + dayName : dayName,
                    FishingAdvisor.Stars(d.Score),
                    d.Score.ToString(),
                    d.BestWindowText,
                    string.Join(", ", d.SpeciesRanking.Take(3).Select(x => x.Species.Name)),
                    string.Format("{0:0}° / {1:0}°C", d.TempMin, d.TempMax),
                    string.Format("ok. {0:0}°C", d.WaterTemperature),
                    string.Format("{0:0} km/h", d.WindMax),
                    string.Format("{0:0.#} mm", d.PrecipitationSum),
                    string.Format("{0:0} hPa {1}", d.Hours.Average(h => h.Pressure), arrow)
                }, d == best));
            }

            grid.SelectedIndex = f.Days.IndexOf(best);
            ShowSelectedDay();
        }

        private void ShowCurrent(Forecast f)
        {
            var c = f.Current;
            var nowHour = FishingAdvisor.HourAt(f, c.Time);
            var today = f.Days.FirstOrDefault(d => d.Date == c.Time.Date);
            double moonAge = MoonPhase.Age(c.Time);

            var sb = new StringBuilder();
            sb.AppendLine(f.City.Name);
            sb.AppendLine(c.Time.ToString("dddd, d MMMM, HH:mm", Polish));
            sb.AppendLine();
            sb.AppendLine(FishingAdvisor.WeatherDescription(c.WeatherCode));
            sb.AppendLine(string.Format("Temperatura: {0:0.0} °C", c.Temperature));
            if (nowHour != null)
                sb.AppendLine(string.Format("Woda (szacunkowo): ok. {0:0} °C", nowHour.WaterTemperature));

            string trend = "";
            double? dp = nowHour != null ? FishingAdvisor.PressureTrend3h(f, nowHour) : null;
            if (dp.HasValue)
                trend = string.Format("  {0} ({1}{2:0.0} hPa/3h)", dp <= -1 ? "↓" : dp >= 1 ? "↑" : "→", dp > 0 ? "+" : "", dp);
            sb.AppendLine(string.Format("Ciśnienie: {0:0} hPa{1}", c.Pressure, trend));
            sb.AppendLine(string.Format("Wiatr: {0:0} km/h, {1}", c.WindSpeed, FishingAdvisor.WindDirectionName(c.WindDirection)));
            sb.AppendLine(string.Format("Wilgotność: {0:0}%", c.Humidity));
            sb.AppendLine(string.Format("Zachmurzenie: {0:0}%", c.CloudCover));
            sb.AppendLine(string.Format("Opady: {0:0.0} mm", c.Precipitation));
            if (today != null)
                sb.AppendLine(string.Format("Wschód słońca {0:HH:mm}, zachód {1:HH:mm}", today.Sunrise, today.Sunset));
            sb.AppendLine(string.Format("Księżyc: {0} ({1}%)", MoonPhase.Name(moonAge), MoonPhase.Illumination(moonAge)));
            sb.AppendLine();
            if (nowHour != null)
                sb.Append(string.Format("Brania teraz ({0}): {1}/100\n{2} {3}", SelectedSpecies.Name.ToLower(), nowHour.Score,
                    FishingAdvisor.Stars(nowHour.Score), FishingAdvisor.RatingText(nowHour.Score)));

            lblCurrent.Text = sb.ToString().TrimEnd();
        }

        private void ShowSelectedDay()
        {
            var item = grid.SelectedItem as ListBoxItem;
            var day = item != null ? item.Tag as DayData : null;
            if (day == null || _forecast == null) return;

            chart.Day = day;
            lblChart.Text = "Prognoza godzinowa – " + DayName(day.Date, _forecast.Current.Time)
                            + "   (najedź na słupek, aby zobaczyć szczegóły)";

            var species = SelectedSpecies;
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("{0} – {1}: {2}/100 ({3})", DayName(day.Date, _forecast.Current.Time),
                species.Name, day.Score, FishingAdvisor.RatingText(day.Score)));
            sb.AppendLine();
            foreach (var reason in day.Reasons) sb.AppendLine(reason);
            sb.AppendLine();
            sb.AppendLine("Ranking gatunków na ten dzień:");
            foreach (var r in day.SpeciesRanking)
                sb.AppendLine(string.Format("   {0,3}  {1}{2}", r.Score, r.Species.Name,
                    r.Species.IsClosed(day.Date) ? " (okres ochronny)" : ""));
            txtDayDetails.Text = sb.ToString().TrimEnd();

            ShowTips(day, species);
        }

        private void ShowTips(DayData day, Species species)
        {
            tabTips.Header = species.IsGeneral ? "Porady" : "Porady: " + species.Name.ToLower();

            var sb = new StringBuilder();
            sb.AppendLine(species.Name.ToUpper());
            sb.AppendLine();
            if (!species.IsGeneral)
            {
                sb.AppendLine(string.Format("Optymalna temperatura wody: {0:0}–{1:0} °C", species.WaterOptMin, species.WaterOptMax));
                sb.AppendLine(species.ClosedText != null
                    ? string.Format("Okres ochronny: {0} (sprawdź też regulamin swojego okręgu PZW).", species.ClosedText)
                    : "Brak ustawowego okresu ochronnego (sprawdź regulamin swojego okręgu PZW).");

                var bestDay = _forecast.Days.OrderByDescending(d => d.Score).ThenBy(d => d.Date).First();
                if (bestDay.Score > 0)
                    sb.AppendLine(string.Format("Najlepszy dzień w tym tygodniu: {0}, {1} ({2}/100).",
                        DayName(bestDay.Date, _forecast.Current.Time), bestDay.BestWindowText, bestDay.Score));
                sb.AppendLine();
            }

            sb.AppendLine(string.Format("{0} (woda ok. {1:0} °C):", DayName(day.Date, _forecast.Current.Time), day.WaterTemperature));
            sb.AppendLine("• " + FishingAdvisor.WaterTip(species, day.WaterTemperature));
            sb.AppendLine();
            sb.AppendLine("Wskazówki:");
            foreach (var tip in species.Tips) sb.AppendLine("• " + tip);
            txtTips.Text = sb.ToString().TrimEnd();
        }

        private Species SelectedSpecies
        {
            get { return (cmbSpecies.SelectedItem as Species) ?? Species.All[0]; }
        }

        private void OnSpeciesChanged()
        {
            _store.LastSpecies = SelectedSpecies.Name;
            _store.Save();
            if (_forecast == null) return;

            // Zmiana gatunku nie wymaga ponownego pobierania pogody – wystarczy przeliczyć oceny.
            FishingAdvisor.Analyze(_forecast, SelectedSpecies);
            ShowForecast();
        }

        private static string DayName(DateTime date, DateTime now)
        {
            return Reports.DayName(date, now);
        }

        #endregion

        #region Porównanie łowisk i udostępnianie

        private async Task CompareSpotsAsync()
        {
            if (_busy) return;
            var chosen = await CompareWindow.ShowCompare(this, _weather, _store.Cities.ToList(), SelectedSpecies);
            if (chosen == null || chosen == cmbCity.SelectedItem) return;
            RefreshCityList(chosen);
            await LoadForecastAsync();
        }

        private async Task CopyForecastTextAsync()
        {
            if (_forecast == null || Clipboard == null) return;
            await Clipboard.SetTextAsync(Reports.ForecastText(_forecast, SelectedSpecies));
            lblStatus.Text = "Skopiowano prognozę do schowka – wklej ją w komunikatorze lub e-mailu.";
        }

        private async Task SaveWindowImageAsync()
        {
            var city = cmbCity.SelectedItem as City;
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Zapisz obraz prognozy",
                SuggestedFileName = string.Format("NaRyby {0} {1:yyyy-MM-dd}.png", city != null ? city.Name : "", DateTime.Now),
                DefaultExtension = "png",
                FileTypeChoices = new[] { new FilePickerFileType("Obraz PNG") { Patterns = new[] { "*.png" } } }
            });
            if (file == null) return;

            // Renderowanie w rozdzielczości ekranu (na Retinie 2×), żeby obraz był ostry.
            double scale = RenderScaling;
            var size = new PixelSize((int)(Bounds.Width * scale), (int)(Bounds.Height * scale));
            using (var bitmap = new RenderTargetBitmap(size, new Vector(96 * scale, 96 * scale)))
            {
                bitmap.Render(this);
                using (var stream = await file.OpenWriteAsync())
                {
                    bitmap.Save(stream);
                }
            }
            lblStatus.Text = "Zapisano obraz: " + file.Name;
        }

        #endregion
    }
}

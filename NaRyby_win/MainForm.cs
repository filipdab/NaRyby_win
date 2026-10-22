using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NaRyby_win
{
    public class MainForm : Form
    {
        private static readonly CultureInfo Polish = new CultureInfo("pl-PL");

        // Szary tekst podpowiedzi w pustym polu (TextBox w .NET Framework nie ma PlaceholderText).
        private const int EM_SETCUEBANNER = 0x1501;

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private readonly WeatherService _weather = new WeatherService();
        private readonly CityStore _store = new CityStore();
        private Forecast _forecast;
        private bool _suppressCityChange;

        private ComboBox cmbCity;
        private ComboBox cmbSpecies;
        private Button btnRefresh;
        private Button btnRemove;
        private TextBox txtSearch;
        private Button btnAdd;
        private Button btnCompare;
        private Button btnShare;
        private Label lblRecommendation;
        private Label lblCurrent;
        private DataGridView grid;
        private GroupBox grpChart;
        private ScoreChart chart;
        private TextBox txtDayDetails;
        private TabPage tabTips;
        private TextBox txtTips;
        private ToolStripStatusLabel lblStatus;
        private Timer refreshTimer;

        public MainForm()
        {
            BuildUi();
            LoadCities();
        }

        #region Budowa interfejsu

        private void BuildUi()
        {
            Text = "NaRyby – kiedy jechać na ryby?";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = new Font("Segoe UI", 9f);
            Size = new Size(1250, 820);
            MinimumSize = new Size(900, 650);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 247, 250);

            // Pasek wyboru miejscowości.
            var top = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(8, 8, 8, 4),
                WrapContents = true
            };
            cmbCity = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Margin = new Padding(3, 4, 3, 3) };
            cmbCity.SelectedIndexChanged += async (s, e) => { if (!_suppressCityChange) await LoadForecastAsync(); };
            btnRefresh = new Button { Text = "⟳ Odśwież", AutoSize = true };
            btnRefresh.Click += async (s, e) => await LoadForecastAsync();
            btnRemove = new Button { Text = "Usuń z listy", AutoSize = true };
            btnRemove.Click += BtnRemove_Click;
            cmbSpecies = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170, Margin = new Padding(3, 4, 3, 3) };
            foreach (var sp in Species.All) cmbSpecies.Items.Add(sp);
            cmbSpecies.SelectedItem = Species.All.FirstOrDefault(sp => sp.Name == "Leszcz") ?? Species.All[0];
            cmbSpecies.SelectedIndexChanged += (s, e) => OnSpeciesChanged();
            txtSearch = new TextBox { Width = 170, Margin = new Padding(3, 5, 3, 3) };
            txtSearch.HandleCreated += (s, e) => SendMessage(txtSearch.Handle, EM_SETCUEBANNER, IntPtr.Zero, "np. Zegrze, Mikołajki");
            txtSearch.KeyDown += async (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                await AddCityAsync();
            };
            btnAdd = new Button { Text = "Szukaj i dodaj", AutoSize = true };
            btnAdd.Click += async (s, e) => await AddCityAsync();
            btnCompare = new Button { Text = "Porównaj łowiska", AutoSize = true };
            btnCompare.Click += async (s, e) => await CompareSpotsAsync();
            var btnProtection = new Button { Text = "Okresy ochronne", AutoSize = true };
            btnProtection.Click += (s, e) => ProtectionForm.ShowProtection(this);
            btnShare = new Button { Text = "Udostępnij ▾", AutoSize = true };
            var shareMenu = new ContextMenuStrip();
            shareMenu.Items.Add("Kopiuj prognozę jako tekst", null, (s, e) => CopyForecastText());
            shareMenu.Items.Add("Kopiuj obraz okna", null, (s, e) => CopyWindowImage());
            shareMenu.Items.Add("Zapisz obraz okna (PNG)...", null, (s, e) => SaveWindowImage());
            btnShare.Click += (s, e) => shareMenu.Show(btnShare, new Point(0, btnShare.Height));
            var btnAbout = new Button { Text = "ⓘ O mnie", AutoSize = true };
            btnAbout.Click += (s, e) => AboutForm.ShowAbout(this);

            top.Controls.Add(MakeLabel("Miejscowość:"));
            top.Controls.Add(cmbCity);
            top.Controls.Add(btnRemove);
            top.Controls.Add(btnCompare);
            top.Controls.Add(new Label { Width = 12 });
            top.Controls.Add(MakeLabel("Dodaj miejscowość:"));
            top.Controls.Add(txtSearch);
            top.Controls.Add(btnAdd);
            top.Controls.Add(new Label { Width = 20 });
            top.Controls.Add(MakeLabel("Gatunek ryby:"));
            top.Controls.Add(cmbSpecies);
            top.Controls.Add(btnRefresh);
            top.Controls.Add(new Label { Width = 12 });
            top.Controls.Add(btnProtection);
            top.Controls.Add(btnShare);
            top.Controls.Add(btnAbout);

            // F1 – okno „O mnie”.
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.F1) return;
                e.Handled = true;
                AboutForm.ShowAbout(this);
            };

            // Baner z rekomendacją.
            lblRecommendation = new Label
            {
                Dock = DockStyle.Top,
                Height = 64,
                Padding = new Padding(14, 6, 14, 6),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.SteelBlue,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Wybierz miejscowość, aby sprawdzić warunki."
            };

            // Środek: bieżące warunki + tabela dni.
            var grpCurrent = new GroupBox { Text = "Warunki teraz", Dock = DockStyle.Left, Width = 290, Padding = new Padding(10) };
            lblCurrent = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.75f) };
            grpCurrent.Controls.Add(lblCurrent);

            var grpDays = new GroupBox { Text = "Prognoza brań na 7 dni (kliknij dzień, aby zobaczyć szczegóły)", Dock = DockStyle.Fill, Padding = new Padding(8) };
            grid = CreateGrid();
            grpDays.Controls.Add(grid);

            var middle = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 8, 8, 0) };
            middle.Controls.Add(grpDays);
            middle.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 8 });
            middle.Controls.Add(grpCurrent);

            // Dół: wykres godzinowy + uzasadnienie.
            grpChart = new GroupBox { Text = "Prognoza godzinowa", Dock = DockStyle.Fill, Padding = new Padding(8) };
            chart = new ScoreChart { Dock = DockStyle.Fill };
            var tabs = new TabControl { Dock = DockStyle.Right, Width = 440 };
            txtDayDetails = MakeReadOnlyText();
            txtTips = MakeReadOnlyText();
            var tabDay = new TabPage("Ocena dnia");
            tabDay.Controls.Add(txtDayDetails);
            tabTips = new TabPage("Porady");
            tabTips.Controls.Add(txtTips);
            tabs.TabPages.Add(tabDay);
            tabs.TabPages.Add(tabTips);
            grpChart.Controls.Add(chart);
            grpChart.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
            grpChart.Controls.Add(tabs);

            var bottomWrap = new Panel { Dock = DockStyle.Bottom, Height = 308, Padding = new Padding(8, 8, 8, 0) };
            bottomWrap.Controls.Add(grpChart);

            var status = new StatusStrip();
            lblStatus = new ToolStripStatusLabel { Text = "Dane pogodowe: Open-Meteo.com", Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            status.Items.Add(lblStatus);

            // Kolejność dodawania ma znaczenie dla dokowania (ostatni dodany dokuje się pierwszy).
            Controls.Add(middle);
            Controls.Add(bottomWrap);
            Controls.Add(lblRecommendation);
            Controls.Add(top);
            Controls.Add(status);

            refreshTimer = new Timer { Interval = 30 * 60 * 1000 };
            refreshTimer.Tick += async (s, e) => await LoadForecastAsync();
            refreshTimer.Start();
        }

        private static TextBox MakeReadOnlyText()
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9.5f)
            };
        }

        private static Label MakeLabel(string text)
        {
            return new Label { Text = text, AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
        }

        private DataGridView CreateGrid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                EnableHeadersVisualStyles = false
            };
            g.RowTemplate.Height = 30;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 235, 242);
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 235, 242);
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.Black;
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(200, 220, 245);
            g.DefaultCellStyle.SelectionForeColor = Color.Black;

            g.Columns.Add("day", "Dzień");
            g.Columns.Add("stars", "Ocena");
            g.Columns.Add("score", "Wynik");
            g.Columns.Add("best", "Najlepsza pora");
            g.Columns.Add("species", "Na co łowić");
            g.Columns.Add("temp", "Temp. powietrza");
            g.Columns.Add("water", "Temp. wody");
            g.Columns.Add("wind", "Wiatr max");
            g.Columns.Add("rain", "Opady");
            g.Columns.Add("pressure", "Ciśnienie");

            g.Columns["day"].FillWeight = 150;
            g.Columns["stars"].FillWeight = 90;
            g.Columns["stars"].DefaultCellStyle.Font = new Font("Segoe UI Symbol", 11f);
            g.Columns["stars"].DefaultCellStyle.ForeColor = Color.DarkGoldenrod;
            g.Columns["score"].FillWeight = 55;
            g.Columns["score"].DefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            g.Columns["score"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            g.Columns["best"].FillWeight = 95;
            g.Columns["species"].FillWeight = 150;
            foreach (DataGridViewColumn c in g.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;

            g.SelectionChanged += (s, e) => ShowSelectedDay();
            return g;
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
            cmbCity.Items.Clear();
            foreach (var c in _store.Cities) cmbCity.Items.Add(c);
            if (select != null) cmbCity.SelectedItem = select;
            _suppressCityChange = false;
        }

        private async Task AddCityAsync()
        {
            string query = txtSearch.Text.Trim();
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
                MessageBox.Show(this, "Nie udało się wyszukać miejscowości:\n" + ex.Message, "NaRyby",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SetBusy(false, "Dane pogodowe: Open-Meteo.com");

            if (found.Count == 0)
            {
                MessageBox.Show(this, "Nie znaleziono miejscowości „" + query + "”.", "NaRyby",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            City chosen = found.Count == 1 ? found[0] : CityPickerForm.Pick(this, found);
            if (chosen == null) return;

            City added = _store.Add(chosen);
            txtSearch.Clear();
            RefreshCityList(added);
            await LoadForecastAsync();
        }

        private async void BtnRemove_Click(object sender, EventArgs e)
        {
            var city = cmbCity.SelectedItem as City;
            if (city == null) return;
            if (_store.Cities.Count <= 1)
            {
                MessageBox.Show(this, "Na liście musi zostać co najmniej jedna miejscowość.", "NaRyby",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, "Usunąć „" + city.Name + "” z listy?", "NaRyby",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            _store.Remove(city);
            RefreshCityList(_store.Cities.First());
            await LoadForecastAsync();
        }

        #endregion

        #region Prognoza

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await LoadForecastAsync();
        }

        private async Task LoadForecastAsync()
        {
            var city = cmbCity.SelectedItem as City;
            if (city == null || !btnRefresh.Enabled) return;

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
                MessageBox.Show(this, "Nie udało się pobrać prognozy pogody.\nSprawdź połączenie z internetem.\n\n" + ex.Message,
                    "NaRyby", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SetBusy(bool busy, string status)
        {
            UseWaitCursor = busy;
            cmbCity.Enabled = !busy;
            cmbSpecies.Enabled = !busy;
            btnRefresh.Enabled = !busy;
            btnRemove.Enabled = !busy;
            btnAdd.Enabled = !busy;
            btnCompare.Enabled = !busy;
            btnShare.Enabled = !busy;
            txtSearch.Enabled = !busy;
            if (status != null) lblStatus.Text = status;
        }

        private void ShowForecast()
        {
            var f = _forecast;
            ShowCurrent(f);

            // Rekomendacja: dzień z najwyższą oceną (przy remisie wcześniejszy).
            var best = f.Days.OrderByDescending(d => d.Score).ThenBy(d => d.Date).First();
            var species = SelectedSpecies;
            lblRecommendation.BackColor = ControlPaint.Dark(FishingAdvisor.ScoreColor(best.Score), 0.15f);
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

            grid.Rows.Clear();
            foreach (var d in f.Days)
            {
                double dp = d.Hours.Last().Pressure - d.Hours.First().Pressure;
                string arrow = dp <= -2 ? "↓" : dp >= 2 ? "↑" : "→";
                int row = grid.Rows.Add(
                    DayName(d.Date, f.Current.Time),
                    FishingAdvisor.Stars(d.Score),
                    d.Score,
                    d.BestWindowText,
                    string.Join(", ", d.SpeciesRanking.Take(3).Select(x => x.Species.Name)),
                    string.Format("{0:0}° / {1:0}°C", d.TempMin, d.TempMax),
                    string.Format("ok. {0:0}°C", d.WaterTemperature),
                    string.Format("{0:0} km/h", d.WindMax),
                    string.Format("{0:0.#} mm", d.PrecipitationSum),
                    string.Format("{0:0} hPa {1}", d.Hours.Average(h => h.Pressure), arrow));

                var r = grid.Rows[row];
                r.Tag = d;
                r.Cells["score"].Style.BackColor = FishingAdvisor.ScoreColor(d.Score);
                r.Cells["score"].Style.SelectionBackColor = FishingAdvisor.ScoreColor(d.Score);
                r.Cells["score"].Style.ForeColor = Color.White;
                r.Cells["score"].Style.SelectionForeColor = Color.White;
                if (d == best)
                {
                    r.DefaultCellStyle.BackColor = FishingAdvisor.LightScoreColor(d.Score);
                    r.Cells["day"].Value = "★ " + r.Cells["day"].Value;
                }
            }

            int bestIndex = f.Days.IndexOf(best);
            grid.ClearSelection();
            grid.Rows[bestIndex].Selected = true;
            grid.CurrentCell = grid.Rows[bestIndex].Cells[0];
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
                sb.AppendLine(string.Format("Brania teraz ({0}): {1}/100\n{2} {3}", SelectedSpecies.Name.ToLower(), nowHour.Score,
                    FishingAdvisor.Stars(nowHour.Score), FishingAdvisor.RatingText(nowHour.Score)));

            lblCurrent.Text = sb.ToString();
        }

        private void ShowSelectedDay()
        {
            if (grid.SelectedRows.Count == 0) return;
            var day = grid.SelectedRows[0].Tag as DayData;
            if (day == null) return;

            chart.Day = day;
            grpChart.Text = "Prognoza godzinowa – " + DayName(day.Date, _forecast.Current.Time)
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
            txtDayDetails.Text = sb.ToString();

            ShowTips(day, species);
        }

        private void ShowTips(DayData day, Species species)
        {
            tabTips.Text = species.IsGeneral ? "Porady" : "Porady: " + species.Name.ToLower();

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
            txtTips.Text = sb.ToString();
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
            var chosen = CompareForm.ShowCompare(this, _weather, _store.Cities.ToList(), SelectedSpecies);
            if (chosen == null || chosen == cmbCity.SelectedItem) return;
            RefreshCityList(chosen);
            await LoadForecastAsync();
        }

        private void CopyForecastText()
        {
            if (_forecast == null) return;
            Clipboard.SetText(Reports.ForecastText(_forecast, SelectedSpecies));
            lblStatus.Text = "Skopiowano prognozę do schowka – wklej ją w komunikatorze lub e-mailu.";
        }

        private Bitmap CaptureWindow()
        {
            var bmp = new Bitmap(ClientSize.Width, ClientSize.Height);
            DrawToBitmap(bmp, new Rectangle(Point.Empty, ClientSize));
            return bmp;
        }

        private void CopyWindowImage()
        {
            using (var bmp = CaptureWindow())
            {
                Clipboard.SetImage(bmp);
            }
            lblStatus.Text = "Skopiowano obraz okna do schowka.";
        }

        private void SaveWindowImage()
        {
            var city = cmbCity.SelectedItem as City;
            using (var dlg = new SaveFileDialog
            {
                Title = "Zapisz obraz prognozy",
                Filter = "Obraz PNG (*.png)|*.png",
                FileName = string.Format("NaRyby {0} {1:yyyy-MM-dd}.png", city != null ? city.Name : "", DateTime.Now)
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                using (var bmp = CaptureWindow())
                {
                    bmp.Save(dlg.FileName, System.Drawing.Imaging.ImageFormat.Png);
                }
                lblStatus.Text = "Zapisano obraz: " + dlg.FileName;
            }
        }

        #endregion
    }
}

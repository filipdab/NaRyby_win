using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NaRyby_win
{
    /// <summary>
    /// Porównanie łowisk: oceny na 7 dni dla wszystkich zapisanych miejscowości w jednej tabeli.
    /// </summary>
    public class CompareForm : Form
    {
        private readonly WeatherService _weather;
        private readonly List<City> _cities;
        private readonly Species _species;
        private readonly DataGridView _grid;
        private readonly Label _status;

        /// <summary>Miejscowość wybrana dwuklikiem – główne okno przełącza się na nią.</summary>
        public City ChosenCity { get; private set; }

        private CompareForm(WeatherService weather, List<City> cities, Species species)
        {
            _weather = weather;
            _cities = cities;
            _species = species;

            Text = "Porównanie łowisk – " + species.Name.ToLower();
            Font = new Font("Segoe UI", 9f);
            Size = new Size(980, 420);
            MinimumSize = new Size(700, 300);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Padding = new Padding(10);

            _grid = new DataGridView
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
                BorderStyle = BorderStyle.FixedSingle,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                EnableHeadersVisualStyles = false
            };
            _grid.RowTemplate.Height = 30;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _grid.CellDoubleClick += (s, e) => ChooseRow(e.RowIndex);
            _grid.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter || _grid.CurrentRow == null) return;
                e.SuppressKeyPress = true;
                ChooseRow(_grid.CurrentRow.Index);
            };

            _status = Theme.Dim(new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Pobieram prognozy dla " + cities.Count + " miejscowości..."
            });
            var info = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                Text = "Ocena brań (0–100) dla każdego dnia. Kliknij dwukrotnie miejscowość, aby otworzyć jej prognozę."
            };

            Controls.Add(_grid);
            Controls.Add(info);
            Controls.Add(_status);
            Theme.Apply(this);
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UseWaitCursor = true;
            var results = await Reports.CompareSpotsAsync(_weather, _cities, _species);
            UseWaitCursor = false;
            if (IsDisposed) return;
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

            _grid.Columns.Add("city", "Miejscowość");
            _grid.Columns["city"].FillWeight = 160;
            foreach (var date in dates)
            {
                var col = _grid.Columns[_grid.Columns.Add("d" + date.ToString("MMdd"), Reports.ShortDayName(date, now))];
                col.FillWeight = 55;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                col.DefaultCellStyle.ForeColor = Color.White;
                col.DefaultCellStyle.SelectionForeColor = Color.White;
            }
            _grid.Columns.Add("best", "Najlepiej");
            _grid.Columns["best"].FillWeight = 190;
            foreach (DataGridViewColumn c in _grid.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;

            foreach (var r in results)
            {
                int row = _grid.Rows.Add();
                var cells = _grid.Rows[row].Cells;
                _grid.Rows[row].Tag = r.City;
                cells["city"].Value = r.City.Name;
                if (r.Forecast == null)
                {
                    cells["best"].Value = "błąd pobierania";
                    cells["best"].ToolTipText = r.Error;
                    continue;
                }

                for (int i = 0; i < dates.Count; i++)
                {
                    var day = r.DayAt(dates[i]);
                    if (day == null) continue;
                    var cell = cells[i + 1];
                    cell.Value = day.Score;
                    cell.Style.BackColor = FishingAdvisor.ScoreColor(day.Score);
                    cell.Style.SelectionBackColor = ControlPaint.Dark(FishingAdvisor.ScoreColor(day.Score), 0.1f);
                    cell.ToolTipText = string.Format("{0}: {1}, najlepiej {2}",
                        Reports.DayName(day.Date, now), FishingAdvisor.RatingText(day.Score), day.BestWindowText);
                }

                var best = r.Best;
                cells["best"].Value = best.Score > 0
                    ? string.Format("{0}, {1}", Reports.ShortDayName(best.Date, now), best.BestWindowText)
                    : "–";
            }

            var top = results.First(r => r.Forecast != null);
            int errors = results.Count(r => r.Forecast == null);
            _status.Text = top.Best.Score > 0
                ? string.Format("Najlepsze łowisko w tym tygodniu: {0} – {1}, {2} ({3}/100).",
                    top.City.Name, Reports.DayName(top.Best.Date, now), top.Best.BestWindowText, top.Best.Score)
                : string.Format("Okres ochronny {0} – w tym tygodniu nie wolno go łowić.", _species.Genitive);
            if (errors > 0) _status.Text += string.Format("   Nie udało się pobrać: {0}.", errors);
        }

        private void ChooseRow(int rowIndex)
        {
            if (rowIndex < 0) return;
            ChosenCity = _grid.Rows[rowIndex].Tag as City;
            DialogResult = DialogResult.OK;
        }

        /// <summary>Pokazuje porównanie; zwraca miejscowość wybraną dwuklikiem albo null.</summary>
        public static City ShowCompare(IWin32Window owner, WeatherService weather, List<City> cities, Species species)
        {
            using (var form = new CompareForm(weather, cities, species))
            {
                return form.ShowDialog(owner) == DialogResult.OK ? form.ChosenCity : null;
            }
        }
    }
}

using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NaRyby_win
{
    /// <summary>
    /// Zestawienie okresów i wymiarów ochronnych wszystkich gatunków z zaznaczeniem, co jest dziś chronione.
    /// </summary>
    public class ProtectionForm : Form
    {
        private readonly DataGridView grid;

        private ProtectionForm()
        {
            Text = "Okresy i wymiary ochronne";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(620, 420);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Padding = new Padding(10);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                EnableHeadersVisualStyles = false
            };
            grid.RowTemplate.Height = 28;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

            grid.Columns.Add("name", "Gatunek");
            grid.Columns.Add("period", "Okres ochronny");
            grid.Columns.Add("length", "Wymiar ochronny");
            grid.Columns.Add("status", "Dziś");
            grid.Columns["name"].FillWeight = 70;
            grid.Columns["period"].FillWeight = 80;
            grid.Columns["length"].FillWeight = 80;
            grid.Columns["status"].FillWeight = 140;
            foreach (DataGridViewColumn c in grid.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;

            var today = DateTime.Today;
            foreach (var sp in Species.All.Where(sp => !sp.IsGeneral))
            {
                string status = Reports.ProtectionStatus(sp, today);
                int row = grid.Rows.Add(sp.Name, sp.ClosedText ?? "brak", Reports.MinLengthText(sp), status);
                grid.Rows[row].Tag = sp;
                if (sp.IsClosed(today)) grid.Rows[row].Cells["status"].Style.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            }
            // DataGridView zaznacza pierwszy wiersz przy tworzeniu – to tylko zestawienie, więc bez zaznaczenia.
            Shown += (s, e) => grid.ClearSelection();

            var note = Theme.Dim(new Label
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                Padding = new Padding(0, 8, 0, 0),
                Text = Reports.ProtectionNote
            });
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(80, 27) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            buttons.Controls.Add(ok);

            Controls.Add(grid);
            Controls.Add(note);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = ok;

            ApplyTheme(null, EventArgs.Empty);
            Theme.Changed += ApplyTheme;
            FormClosed += (s, e) => Theme.Changed -= ApplyTheme;
        }

        private void ApplyTheme(object sender, EventArgs e)
        {
            Theme.Apply(this);
            var today = DateTime.Today;
            foreach (DataGridViewRow row in grid.Rows)
            {
                var sp = (Species)row.Tag;
                Color back = sp.IsClosed(today) ? Theme.ClosedRow
                    : Reports.ProtectionStatus(sp, today).Contains("ochrona") ? Theme.SoonRow : Theme.Surface;
                row.DefaultCellStyle.BackColor = back;
                row.DefaultCellStyle.SelectionBackColor = Theme.IsDark ? ControlPaint.Light(back, 0.1f) : ControlPaint.Dark(back, 0.05f);
            }
        }

        public static void ShowProtection(IWin32Window owner)
        {
            using (var form = new ProtectionForm())
            {
                form.ShowDialog(owner);
            }
        }
    }
}

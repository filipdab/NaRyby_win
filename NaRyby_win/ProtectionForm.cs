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
        private static readonly Color ClosedColor = Color.FromArgb(253, 226, 226);
        private static readonly Color SoonColor = Color.FromArgb(255, 243, 205);

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

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                EnableHeadersVisualStyles = false
            };
            grid.RowTemplate.Height = 28;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 235, 242);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 235, 242);
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;

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
                Color back = sp.IsClosed(today) ? ClosedColor : status.Contains("ochrona") ? SoonColor : Color.White;
                grid.Rows[row].DefaultCellStyle.BackColor = back;
                grid.Rows[row].DefaultCellStyle.SelectionBackColor = ControlPaint.Dark(back, 0.05f);
                if (sp.IsClosed(today)) grid.Rows[row].Cells["status"].Style.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            }
            // DataGridView zaznacza pierwszy wiersz przy tworzeniu – to tylko zestawienie, więc bez zaznaczenia.
            Shown += (s, e) => grid.ClearSelection();

            var note = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                Padding = new Padding(0, 8, 0, 0),
                ForeColor = Color.DimGray,
                Text = Reports.ProtectionNote
            };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(80, 27) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            buttons.Controls.Add(ok);

            Controls.Add(grid);
            Controls.Add(note);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = ok;
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

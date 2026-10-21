using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace NaRyby_win
{
    /// <summary>
    /// Okno wyboru, gdy wyszukiwanie zwróciło kilka miejscowości o tej samej nazwie.
    /// </summary>
    public class CityPickerForm : Form
    {
        private readonly ListBox _list;

        private CityPickerForm(List<City> cities)
        {
            Text = "Wybierz miejscowość";
            Font = new Font("Segoe UI", 9f);
            Size = new Size(460, 340);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Padding = new Padding(10);

            _list = new ListBox { Dock = DockStyle.Fill, DisplayMember = "Description", IntegralHeight = false };
            foreach (var c in cities) _list.Items.Add(c);
            _list.SelectedIndex = 0;
            _list.DoubleClick += (s, e) => { DialogResult = DialogResult.OK; };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            var ok = new Button { Text = "Dodaj", DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Anuluj", DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            Controls.Add(_list);
            Controls.Add(new Label { Text = "Znaleziono kilka miejscowości – wybierz właściwą:", Dock = DockStyle.Top, Height = 24 });
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        public static City Pick(IWin32Window owner, List<City> cities)
        {
            using (var form = new CityPickerForm(cities))
            {
                return form.ShowDialog(owner) == DialogResult.OK ? form._list.SelectedItem as City : null;
            }
        }
    }
}

using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace NaRyby_win
{
    /// <summary>
    /// Okno „O mnie” – informacje o programie, autorze i źródle danych.
    /// </summary>
    public class AboutForm : Form
    {
        private const string WeatherUrl = "https://open-meteo.com";

        private AboutForm()
        {
            Text = "O mnie";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(440, 330);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;

            var icon = new PictureBox
            {
                Location = new Point(20, 20),
                Size = new Size(64, 64),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = new Icon(Icon.ExtractAssociatedIcon(Application.ExecutablePath), 64, 64).ToBitmap()
            };
            var title = new Label
            {
                Text = "NaRyby",
                Location = new Point(100, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.SteelBlue
            };
            var subtitle = new Label
            {
                Text = "kiedy jechać na ryby?",
                Location = new Point(102, 58),
                AutoSize = true,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.DimGray
            };
            var description = new Label
            {
                Text = "Prognoza brań na 7 dni dla wędkarzy – na podstawie pogody, ciśnienia, " +
                       "wiatru, fazy księżyca i wybranego gatunku ryby.",
                Location = new Point(20, 100),
                Size = new Size(400, 40)
            };
            var author = new Label
            {
                Text = "Autor: Filip Dąbrowski",
                Location = new Point(20, 150),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            var repo = MakeLink("Kod źródłowy: ", "github.com/filipdab/NaRyby_win", Reports.RepoUrl, new Point(20, 176));
            var weather = MakeLink("Dane pogodowe: ", "Open-Meteo.com", WeatherUrl, new Point(20, 200));
            var license = new Label
            {
                Text = "Licencja MIT · © 2026 Filip Dąbrowski",
                Location = new Point(20, 224),
                AutoSize = true,
                ForeColor = Color.DimGray
            };
            var note = new Label
            {
                Text = "Prognoza ma charakter orientacyjny – ryby nie zawsze czytają prognozy. Połamania kija!",
                Location = new Point(20, 252),
                Size = new Size(400, 34),
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic)
            };

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(80, 27), Location = new Point(340, 292) };

            Controls.AddRange(new Control[] { icon, title, subtitle, description, author, repo, weather, license, note, ok });
            AcceptButton = ok;
            CancelButton = ok;
        }

        private static LinkLabel MakeLink(string prefix, string linkText, string url, Point location)
        {
            var link = new LinkLabel
            {
                Text = prefix + linkText,
                Location = location,
                AutoSize = true,
                LinkArea = new LinkArea(prefix.Length, linkText.Length)
            };
            link.LinkClicked += (s, e) => Process.Start(url);
            return link;
        }

        public static void ShowAbout(IWin32Window owner)
        {
            using (var form = new AboutForm())
            {
                form.ShowDialog(owner);
            }
        }
    }
}

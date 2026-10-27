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
        private const string HydroUrl = "https://danepubliczne.imgw.pl";

        private AboutForm()
        {
            Text = "O mnie";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(440, 354);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            Theme.UseSurface(this);

            var icon = new PictureBox
            {
                Location = new Point(20, 20),
                Size = new Size(64, 64),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = new Icon(Icon.ExtractAssociatedIcon(Application.ExecutablePath), 64, 64).ToBitmap()
            };
            var title = Theme.Accented(new Label
            {
                Text = "NaRyby",
                Location = new Point(100, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 18f, FontStyle.Bold)
            });
            var subtitle = Theme.Dim(new Label
            {
                Text = "kiedy jechać na ryby?",
                Location = new Point(102, 58),
                AutoSize = true,
                Font = new Font("Segoe UI", 10f)
            });
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
            var hydro = MakeLink("Stan wody: ", "Instytut Meteorologii i Gospodarki Wodnej – PIB", HydroUrl, new Point(20, 224));
            var license = Theme.Dim(new Label
            {
                Text = "Licencja MIT · © 2026 Filip Dąbrowski",
                Location = new Point(20, 248),
                AutoSize = true
            });
            var note = Theme.Dim(new Label
            {
                Text = "Prognoza ma charakter orientacyjny – ryby nie zawsze czytają prognozy. Połamania kija!",
                Location = new Point(20, 276),
                Size = new Size(400, 34),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic)
            });

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(80, 27), Location = new Point(340, 316) };

            Controls.AddRange(new Control[] { icon, title, subtitle, description, author, repo, weather, hydro, license, note, ok });
            AcceptButton = ok;
            CancelButton = ok;
            Theme.Apply(this);
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

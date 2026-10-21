using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace NaRyby_win
{
    /// <summary>
    /// Wykres godzinowej oceny warunków (słupki) z linią ciśnienia i zacienioną nocą.
    /// </summary>
    public class ScoreChart : Control
    {
        private static readonly Color NightColor = Color.FromArgb(232, 236, 246);
        private static readonly Color BestColor = Color.FromArgb(215, 240, 215);
        private static readonly Color GridColor = Color.FromArgb(225, 225, 225);
        private static readonly Color PressureColor = Color.FromArgb(40, 90, 200);

        private readonly ToolTip _toolTip = new ToolTip();
        private DayData _day;
        private int _hoverIndex = -1;

        public ScoreChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 8.25f);
        }

        public DayData Day
        {
            get { return _day; }
            set
            {
                _day = value;
                _hoverIndex = -1;
                _toolTip.Hide(this);
                Invalidate();
            }
        }

        private Rectangle PlotArea
        {
            get { return new Rectangle(36, 22, Math.Max(10, Width - 36 - 52), Math.Max(10, Height - 22 - 24)); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);

            if (_day == null || _day.Hours.Count == 0)
            {
                TextRenderer.DrawText(g, "Brak danych – wybierz miejscowość.", Font, ClientRectangle, Color.Gray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            var area = PlotArea;
            var hours = _day.Hours;
            float slot = area.Width / (float)hours.Count;

            // Tło: noc i najlepsze okno.
            for (int i = 0; i < hours.Count; i++)
            {
                var h = hours[i];
                var column = new RectangleF(area.Left + i * slot, area.Top, slot, area.Height);
                bool isNight = h.Time.AddHours(1) <= _day.Sunrise || h.Time >= _day.Sunset;
                bool isBest = _day.BestLength > 0 && h.Time >= _day.BestStart && h.Time < _day.BestStart.AddHours(_day.BestLength);
                if (isBest)
                    using (var b = new SolidBrush(BestColor)) g.FillRectangle(b, column);
                else if (isNight)
                    using (var b = new SolidBrush(NightColor)) g.FillRectangle(b, column);
            }

            // Siatka i oś ocen.
            using (var pen = new Pen(GridColor))
            {
                for (int v = 0; v <= 100; v += 25)
                {
                    float y = ScoreY(area, v);
                    g.DrawLine(pen, area.Left, y, area.Right, y);
                    TextRenderer.DrawText(g, v.ToString(), Font, new Rectangle(0, (int)y - 8, area.Left - 4, 16), Color.DimGray,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                }
            }

            // Słupki ocen.
            for (int i = 0; i < hours.Count; i++)
            {
                var h = hours[i];
                float y = ScoreY(area, h.Score);
                var bar = new RectangleF(area.Left + i * slot + slot * 0.15f, y, slot * 0.7f, area.Bottom - y);
                Color color = FishingAdvisor.ScoreColor(h.Score);
                if (i == _hoverIndex) color = ControlPaint.Dark(color, 0.1f);
                using (var b = new SolidBrush(color)) g.FillRectangle(b, bar);
            }

            // Linia ciśnienia z osią po prawej.
            double pMin = Math.Floor(hours.Min(h => h.Pressure)) - 1;
            double pMax = Math.Ceiling(hours.Max(h => h.Pressure)) + 1;
            var points = hours.Select((h, i) => new PointF(
                area.Left + i * slot + slot / 2,
                (float)(area.Bottom - (h.Pressure - pMin) / (pMax - pMin) * area.Height))).ToArray();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (points.Length > 1)
                using (var pen = new Pen(PressureColor, 2f)) g.DrawLines(pen, points);
            g.SmoothingMode = SmoothingMode.Default;
            TextRenderer.DrawText(g, pMax.ToString("0"), Font, new Point(area.Right + 4, area.Top - 7), PressureColor);
            TextRenderer.DrawText(g, pMin.ToString("0"), Font, new Point(area.Right + 4, area.Bottom - 7), PressureColor);
            TextRenderer.DrawText(g, "hPa", Font, new Point(area.Right + 4, area.Top + area.Height / 2 - 7), PressureColor);

            // Godziny na osi X.
            int step = slot < 22 ? 3 : 1;
            for (int i = 0; i < hours.Count; i += step)
            {
                var rect = new Rectangle((int)(area.Left + i * slot - 10), area.Bottom + 3, (int)slot + 20, 16);
                TextRenderer.DrawText(g, hours[i].Time.ToString("HH"), Font, rect, Color.DimGray, TextFormatFlags.HorizontalCenter);
            }

            // Legenda.
            int lx = area.Left;
            DrawLegendBox(g, ref lx, FishingAdvisor.ScoreColor(80), "ocena brań");
            DrawLegendBox(g, ref lx, BestColor, "najlepsze okno");
            DrawLegendBox(g, ref lx, NightColor, "noc");
            using (var pen = new Pen(PressureColor, 2f)) g.DrawLine(pen, lx, 10, lx + 14, 10);
            TextRenderer.DrawText(g, "ciśnienie", Font, new Point(lx + 18, 3), Color.DimGray);
        }

        private void DrawLegendBox(Graphics g, ref int x, Color color, string text)
        {
            using (var b = new SolidBrush(color)) g.FillRectangle(b, x, 4, 12, 12);
            g.DrawRectangle(Pens.Silver, x, 4, 12, 12);
            TextRenderer.DrawText(g, text, Font, new Point(x + 16, 3), Color.DimGray);
            x += 16 + TextRenderer.MeasureText(text, Font).Width + 12;
        }

        private static float ScoreY(Rectangle area, int score)
        {
            return area.Bottom - score / 100f * area.Height;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_day == null || _day.Hours.Count == 0) return;

            var area = PlotArea;
            float slot = area.Width / (float)_day.Hours.Count;
            int index = e.X >= area.Left && e.X < area.Right ? (int)((e.X - area.Left) / slot) : -1;
            if (index == _hoverIndex) return;

            _hoverIndex = index;
            Invalidate();
            if (index < 0 || index >= _day.Hours.Count)
            {
                _toolTip.Hide(this);
                return;
            }

            var h = _day.Hours[index];
            string text = string.Format(
                "{0:HH:mm}–{1:HH:mm}\nOcena: {2}/100 ({3})\n{4}\nTemp.: {5:0.0}°C\nCiśnienie: {6:0} hPa\nWiatr: {7:0} km/h, {8} (porywy {9:0})\nOpady: {10:0.0} mm, zachmurzenie {11:0}%",
                h.Time, h.Time.AddHours(1), h.Score, FishingAdvisor.RatingText(h.Score),
                FishingAdvisor.WeatherDescription(h.WeatherCode), h.Temperature, h.Pressure,
                h.WindSpeed, FishingAdvisor.WindDirectionName(h.WindDirection), h.WindGusts,
                h.Precipitation, h.CloudCover);
            _toolTip.Show(text, this, e.X + 14, e.Y + 14);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverIndex = -1;
            _toolTip.Hide(this);
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _toolTip.Dispose();
            base.Dispose(disposing);
        }
    }
}

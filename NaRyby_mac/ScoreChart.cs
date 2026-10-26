using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using NaRyby_win;

namespace NaRyby_mac
{
    /// <summary>
    /// Wykres godzinowej oceny warunków (słupki) z linią ciśnienia i zacienioną nocą.
    /// Dymek z pogodą dla godziny pod kursorem jest rysowany bezpośrednio na wykresie.
    /// </summary>
    public class ScoreChart : Control
    {
        private const double TextSize = 11;

        private static readonly IBrush NightBrush = Ui.Themed(Color.FromRgb(232, 236, 246), Color.FromRgb(46, 52, 68));
        private static readonly IBrush BestBrush = Ui.Themed(Color.FromRgb(215, 240, 215), Color.FromRgb(40, 72, 46));
        private static readonly IPen GridPen = new Pen(Ui.Themed(Color.FromRgb(225, 225, 225), Color.FromRgb(62, 66, 72)), 1);
        private static readonly IBrush PressureBrush = Ui.Themed(Color.FromRgb(40, 90, 200), Color.FromRgb(110, 160, 255));
        private static readonly IPen PressurePen = new Pen(PressureBrush, 2);
        private static readonly IPen LegendPen = new Pen(Ui.Themed(Color.FromRgb(192, 192, 192), Color.FromRgb(100, 104, 110)), 1);
        private static readonly IBrush TipBackground = Ui.Themed(Color.FromRgb(255, 255, 240), Color.FromRgb(52, 55, 61));
        private static readonly IPen TipPen = new Pen(Ui.Themed(Color.FromRgb(160, 160, 160), Color.FromRgb(110, 114, 120)), 1);

        private DayData _day;
        private int _hoverIndex = -1;
        private Point _mouse;

        public ScoreChart()
        {
            ClipToBounds = true;
            // Wykres rysuje sam, więc po zmianie motywu musi się przerysować.
            Ui.ThemeChanged += (s, e) => InvalidateVisual();
        }

        public DayData Day
        {
            get { return _day; }
            set
            {
                _day = value;
                _hoverIndex = -1;
                InvalidateVisual();
            }
        }

        private Rect PlotArea
        {
            get { return new Rect(36, 24, Math.Max(10, Bounds.Width - 36 - 52), Math.Max(10, Bounds.Height - 24 - 24)); }
        }

        private static FormattedText Text(string text, IBrush brush)
        {
            return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default), TextSize, brush);
        }

        public override void Render(DrawingContext ctx)
        {
            var bounds = new Rect(Bounds.Size);
            ctx.FillRectangle(Ui.Surface, bounds);

            if (_day == null || _day.Hours.Count == 0)
            {
                var empty = Text("Brak danych – wybierz miejscowość.", Ui.DimGray);
                ctx.DrawText(empty, new Point((bounds.Width - empty.Width) / 2, (bounds.Height - empty.Height) / 2));
                return;
            }

            var area = PlotArea;
            var hours = _day.Hours;
            double slot = area.Width / hours.Count;

            // Tło: noc i najlepsze okno.
            for (int i = 0; i < hours.Count; i++)
            {
                var h = hours[i];
                var column = new Rect(area.Left + i * slot, area.Top, slot, area.Height);
                bool isNight = h.Time.AddHours(1) <= _day.Sunrise || h.Time >= _day.Sunset;
                bool isBest = _day.BestLength > 0 && h.Time >= _day.BestStart && h.Time < _day.BestStart.AddHours(_day.BestLength);
                if (isBest) ctx.FillRectangle(BestBrush, column);
                else if (isNight) ctx.FillRectangle(NightBrush, column);
            }

            // Siatka i oś ocen.
            for (int v = 0; v <= 100; v += 25)
            {
                double y = ScoreY(area, v);
                ctx.DrawLine(GridPen, new Point(area.Left, y), new Point(area.Right, y));
                var label = Text(v.ToString(), Ui.DimGray);
                ctx.DrawText(label, new Point(area.Left - 4 - label.Width, y - label.Height / 2));
            }

            // Słupki ocen.
            for (int i = 0; i < hours.Count; i++)
            {
                double y = ScoreY(area, hours[i].Score);
                var bar = new Rect(area.Left + i * slot + slot * 0.15, y, slot * 0.7, area.Bottom - y);
                Color color = Ui.ToAvalonia(FishingAdvisor.ScoreColor(hours[i].Score));
                if (i == _hoverIndex) color = Ui.Darken(color, 0.15);
                ctx.FillRectangle(new SolidColorBrush(color), bar);
            }

            // Linia ciśnienia z osią po prawej.
            double pMin = Math.Floor(hours.Min(h => h.Pressure)) - 1;
            double pMax = Math.Ceiling(hours.Max(h => h.Pressure)) + 1;
            var points = hours.Select((h, i) => new Point(
                area.Left + i * slot + slot / 2,
                area.Bottom - (h.Pressure - pMin) / (pMax - pMin) * area.Height)).ToArray();
            if (points.Length > 1)
            {
                var line = new StreamGeometry();
                using (var g = line.Open())
                {
                    g.BeginFigure(points[0], false);
                    for (int i = 1; i < points.Length; i++) g.LineTo(points[i]);
                    g.EndFigure(false);
                }
                ctx.DrawGeometry(null, PressurePen, line);
            }
            var maxText = Text(pMax.ToString("0"), PressureBrush);
            var minText = Text(pMin.ToString("0"), PressureBrush);
            var hpaText = Text("hPa", PressureBrush);
            ctx.DrawText(maxText, new Point(area.Right + 4, area.Top - maxText.Height / 2));
            ctx.DrawText(minText, new Point(area.Right + 4, area.Bottom - minText.Height / 2));
            ctx.DrawText(hpaText, new Point(area.Right + 4, area.Top + area.Height / 2 - hpaText.Height / 2));

            // Godziny na osi X.
            int step = slot < 22 ? 3 : 1;
            for (int i = 0; i < hours.Count; i += step)
            {
                var label = Text(hours[i].Time.ToString("HH"), Ui.DimGray);
                ctx.DrawText(label, new Point(area.Left + i * slot + slot / 2 - label.Width / 2, area.Bottom + 4));
            }

            // Legenda.
            double lx = area.Left;
            DrawLegendBox(ctx, ref lx, new SolidColorBrush(Ui.ToAvalonia(FishingAdvisor.ScoreColor(80))), "ocena brań");
            DrawLegendBox(ctx, ref lx, BestBrush, "najlepsze okno");
            DrawLegendBox(ctx, ref lx, NightBrush, "noc");
            ctx.DrawLine(PressurePen, new Point(lx, 11), new Point(lx + 14, 11));
            ctx.DrawText(Text("ciśnienie", Ui.DimGray), new Point(lx + 18, 4));

            if (_hoverIndex >= 0 && _hoverIndex < hours.Count) DrawTip(ctx, bounds, hours[_hoverIndex]);
        }

        private static void DrawLegendBox(DrawingContext ctx, ref double x, IBrush brush, string text)
        {
            var box = new Rect(x, 5, 12, 12);
            ctx.FillRectangle(brush, box);
            ctx.DrawRectangle(null, LegendPen, box);
            var label = Text(text, Ui.DimGray);
            ctx.DrawText(label, new Point(x + 16, 4));
            x += 16 + label.Width + 12;
        }

        private void DrawTip(DrawingContext ctx, Rect bounds, HourData h)
        {
            string text = string.Format(
                "{0:HH:mm}–{1:HH:mm}\nOcena: {2}/100 ({3})\n{4}\nTemp.: {5:0.0}°C\nCiśnienie: {6:0} hPa\nWiatr: {7:0} km/h, {8} (porywy {9:0})\nOpady: {10:0.0} mm, zachmurzenie {11:0}%",
                h.Time, h.Time.AddHours(1), h.Score, FishingAdvisor.RatingText(h.Score),
                FishingAdvisor.WeatherDescription(h.WeatherCode), h.Temperature, h.Pressure,
                h.WindSpeed, FishingAdvisor.WindDirectionName(h.WindDirection), h.WindGusts,
                h.Precipitation, h.CloudCover);
            var tip = Text(text, Ui.Text);

            double w = tip.Width + 16, hgt = tip.Height + 12;
            double x = _mouse.X + 14, y = _mouse.Y + 14;
            if (x + w > bounds.Right - 2) x = _mouse.X - 14 - w;
            if (y + hgt > bounds.Bottom - 2) y = bounds.Bottom - 2 - hgt;
            x = Math.Max(2, x);
            y = Math.Max(2, y);

            var box = new Rect(x, y, w, hgt);
            ctx.DrawRectangle(TipBackground, TipPen, box, 4, 4);
            ctx.DrawText(tip, new Point(x + 8, y + 6));
        }

        private static double ScoreY(Rect area, int score)
        {
            return area.Bottom - score / 100.0 * area.Height;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (_day == null || _day.Hours.Count == 0) return;

            _mouse = e.GetPosition(this);
            var area = PlotArea;
            double slot = area.Width / _day.Hours.Count;
            int index = _mouse.X >= area.Left && _mouse.X < area.Right ? (int)((_mouse.X - area.Left) / slot) : -1;
            _hoverIndex = index < _day.Hours.Count ? index : -1;
            InvalidateVisual();
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            _hoverIndex = -1;
            InvalidateVisual();
        }
    }
}

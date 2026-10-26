using System;
using System.Collections.Generic;
using Avalonia.Media;

namespace NaRyby_mac
{
    /// <summary>
    /// Kolory wspólne dla okien oraz przejście z System.Drawing.Color (wspólna logika) na Avalonię.
    /// Pędzle mają wariant jasny i ciemny – przy zmianie motywu systemu zmienia się ich kolor,
    /// więc kontrolki, które ich używają, przemalowują się same.
    /// </summary>
    internal static class Ui
    {
        private static readonly List<Tuple<SolidColorBrush, Color, Color>> ThemedBrushes = new List<Tuple<SolidColorBrush, Color, Color>>();

        public static readonly SolidColorBrush WindowBackground = Themed(Color.FromRgb(245, 247, 250), Color.FromRgb(30, 32, 36));
        public static readonly SolidColorBrush Surface = Themed(Colors.White, Color.FromRgb(42, 45, 50));
        public static readonly SolidColorBrush BorderBrush = Themed(Color.FromRgb(208, 214, 222), Color.FromRgb(68, 73, 80));
        public static readonly SolidColorBrush HeaderBackground = Themed(Color.FromRgb(230, 235, 242), Color.FromRgb(54, 59, 67));
        public static readonly SolidColorBrush Text = Themed(Colors.Black, Color.FromRgb(232, 232, 232));
        public static readonly SolidColorBrush SteelBlue = Themed(Color.FromRgb(70, 130, 180), Color.FromRgb(120, 175, 225));
        public static readonly SolidColorBrush Gold = Themed(Color.FromRgb(184, 134, 11), Color.FromRgb(230, 180, 60));
        public static readonly SolidColorBrush DimGray = Themed(Color.FromRgb(105, 105, 105), Color.FromRgb(160, 164, 170));

        // Tło banera pod białym tekstem – takie samo w obu motywach.
        public static readonly IBrush Banner = new SolidColorBrush(Color.FromRgb(70, 130, 180));

        public static bool IsDark { get; private set; }

        /// <summary>Wywoływane po przełączeniu motywu – dla elementów, których nie da się przemalować samą zmianą pędzla.</summary>
        public static event EventHandler ThemeChanged;

        public static SolidColorBrush Themed(Color light, Color dark)
        {
            var brush = new SolidColorBrush(IsDark ? dark : light);
            ThemedBrushes.Add(Tuple.Create(brush, light, dark));
            return brush;
        }

        public static void ApplyTheme(bool dark)
        {
            if (dark == IsDark) return;
            IsDark = dark;
            foreach (var t in ThemedBrushes) t.Item1.Color = dark ? t.Item3 : t.Item2;
            var handler = ThemeChanged;
            if (handler != null) handler(null, EventArgs.Empty);
        }

        public static Color ToAvalonia(System.Drawing.Color c)
        {
            return Color.FromRgb(c.R, c.G, c.B);
        }

        public static IBrush Brush(System.Drawing.Color c)
        {
            return new SolidColorBrush(ToAvalonia(c));
        }

        /// <summary>Delikatne tło w kolorze oceny (wyróżnienie najlepszego dnia) – rozjaśnione albo przyciemnione do tła.</summary>
        public static IBrush SoftBrush(System.Drawing.Color c)
        {
            Color back = Surface.Color;
            return new SolidColorBrush(Color.FromRgb(
                (byte)(back.R + (c.R - back.R) / 3),
                (byte)(back.G + (c.G - back.G) / 3),
                (byte)(back.B + (c.B - back.B) / 3)));
        }

        public static Color Darken(Color c, double amount)
        {
            return Color.FromRgb((byte)(c.R * (1 - amount)), (byte)(c.G * (1 - amount)), (byte)(c.B * (1 - amount)));
        }
    }
}

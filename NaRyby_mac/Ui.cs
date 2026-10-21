using Avalonia.Media;

namespace NaRyby_mac
{
    /// <summary>Kolory wspólne dla okien oraz przejście z System.Drawing.Color (wspólna logika) na Avalonię.</summary>
    internal static class Ui
    {
        public static readonly IBrush WindowBackground = new SolidColorBrush(Color.FromRgb(245, 247, 250));
        public static readonly IBrush BorderBrush = new SolidColorBrush(Color.FromRgb(208, 214, 222));
        public static readonly IBrush HeaderBackground = new SolidColorBrush(Color.FromRgb(230, 235, 242));
        public static readonly IBrush SteelBlue = new SolidColorBrush(Color.FromRgb(70, 130, 180));
        public static readonly IBrush Gold = new SolidColorBrush(Color.FromRgb(184, 134, 11));
        public static readonly IBrush DimGray = new SolidColorBrush(Color.FromRgb(105, 105, 105));

        public static Color ToAvalonia(System.Drawing.Color c)
        {
            return Color.FromRgb(c.R, c.G, c.B);
        }

        public static IBrush Brush(System.Drawing.Color c)
        {
            return new SolidColorBrush(ToAvalonia(c));
        }

        public static Color Darken(Color c, double amount)
        {
            return Color.FromRgb((byte)(c.R * (1 - amount)), (byte)(c.G * (1 - amount)), (byte)(c.B * (1 - amount)));
        }
    }
}

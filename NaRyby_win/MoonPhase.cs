using System;

namespace NaRyby_win
{
    public static class MoonPhase
    {
        private const double SynodicMonth = 29.530588853;
        private static readonly DateTime ReferenceNewMoon = new DateTime(2000, 1, 6, 18, 14, 0);

        /// <summary>Wiek Księżyca w dniach (0 = nów, ~14.77 = pełnia).</summary>
        public static double Age(DateTime date)
        {
            double days = (date - ReferenceNewMoon).TotalDays;
            double age = days % SynodicMonth;
            return age < 0 ? age + SynodicMonth : age;
        }

        /// <summary>Oświetlona część tarczy w procentach.</summary>
        public static int Illumination(double age)
        {
            return (int)Math.Round((1 - Math.Cos(2 * Math.PI * age / SynodicMonth)) / 2 * 100);
        }

        /// <summary>Nów i pełnia są tradycyjnie uważane za okresy wzmożonego żerowania.</summary>
        public static bool IsFavorable(double age)
        {
            return age < 2 || age > SynodicMonth - 2 || Math.Abs(age - SynodicMonth / 2) < 2;
        }

        public static string Name(double age)
        {
            if (age < 1.85) return "Nów";
            if (age < 5.54) return "Przybywający sierp";
            if (age < 9.23) return "Pierwsza kwadra";
            if (age < 12.92) return "Przybywający garb";
            if (age < 16.61) return "Pełnia";
            if (age < 20.30) return "Ubywający garb";
            if (age < 23.99) return "Ostatnia kwadra";
            if (age < 27.68) return "Ubywający sierp";
            return "Nów";
        }
    }
}

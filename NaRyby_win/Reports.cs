using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NaRyby_win
{
    /// <summary>Prognoza dla jednej miejscowości w porównaniu łowisk.</summary>
    public class SpotResult
    {
        public City City { get; set; }
        public Forecast Forecast { get; set; }
        /// <summary>Komunikat błędu, gdy nie udało się pobrać prognozy (wtedy Forecast == null).</summary>
        public string Error { get; set; }

        public DayData Best
        {
            get { return Forecast == null ? null : Forecast.Days.OrderByDescending(d => d.Score).ThenBy(d => d.Date).First(); }
        }

        public DayData DayAt(DateTime date)
        {
            return Forecast == null ? null : Forecast.Days.FirstOrDefault(d => d.Date == date);
        }
    }

    /// <summary>
    /// Zestawienia wspólne dla wersji Windows i macOS: porównanie łowisk, okresy ochronne, tekst do udostępnienia.
    /// </summary>
    public static class Reports
    {
        private static readonly CultureInfo Polish = new CultureInfo("pl-PL");

        public const string RepoUrl = "https://github.com/filipdab/NaRyby_win";

        public static string DayName(DateTime date, DateTime now)
        {
            string name = date.ToString("dddd d.MM", Polish);
            if (date == now.Date) return "Dziś, " + name;
            if (date == now.Date.AddDays(1)) return "Jutro, " + name;
            return char.ToUpper(name[0]) + name.Substring(1);
        }

        private static readonly string[] ShortDays = { "Nd", "Pn", "Wt", "Śr", "Cz", "Pt", "Sb" };

        /// <summary>Krótka nazwa dnia do nagłówka kolumny, np. „Dziś”, „Jutro”, „Pt 24.10”.</summary>
        public static string ShortDayName(DateTime date, DateTime now)
        {
            if (date == now.Date) return "Dziś";
            if (date == now.Date.AddDays(1)) return "Jutro";
            return ShortDays[(int)date.DayOfWeek] + " " + date.ToString("d.MM");
        }

        #region Porównanie łowisk

        /// <summary>
        /// Pobiera prognozy dla wszystkich miejscowości równolegle i ocenia je dla gatunku.
        /// Błąd jednej miejscowości nie przerywa pozostałych. Wynik jest posortowany od najlepszego łowiska.
        /// </summary>
        public static async Task<List<SpotResult>> CompareSpotsAsync(WeatherService weather, IEnumerable<City> cities, Species species)
        {
            var tasks = cities.Select(async city =>
            {
                var result = new SpotResult { City = city };
                try
                {
                    var forecast = await weather.GetForecastAsync(city);
                    FishingAdvisor.Analyze(forecast, species);
                    result.Forecast = forecast;
                }
                catch (Exception ex)
                {
                    result.Error = ex.Message;
                }
                return result;
            }).ToList();

            var results = await Task.WhenAll(tasks);
            return results
                .OrderBy(r => r.Forecast == null)
                .ThenByDescending(r => r.Best != null ? r.Best.Score : 0)
                .ThenBy(r => r.City.Name)
                .ToList();
        }

        #endregion

        #region Okresy ochronne

        /// <summary>Stan ochrony gatunku w danym dniu, np. „Okres ochronny do 30.04” albo „Można łowić”.</summary>
        public static string ProtectionStatus(Species species, DateTime today)
        {
            if (species.ClosedFrom == 0) return "Można łowić";
            if (species.IsClosed(today))
                return string.Format("Okres ochronny do {0}.{1:00}", species.ClosedTo % 100, species.ClosedTo / 100);

            int days = DaysUntilClosed(species, today);
            if (days <= 30)
                return days == 1 ? "Można łowić – ochrona od jutra" : string.Format("Można łowić – ochrona za {0} dni", days);
            return "Można łowić";
        }

        private static int DaysUntilClosed(Species species, DateTime today)
        {
            var start = new DateTime(today.Year, species.ClosedFrom / 100, species.ClosedFrom % 100);
            if (start < today.Date) start = start.AddYears(1);
            return (int)(start - today.Date).TotalDays;
        }

        public static string MinLengthText(Species species)
        {
            return species.MinLength > 0 ? species.MinLength + " cm" : "brak ustawowego";
        }

        public const string ProtectionNote =
            "Okresy i wymiary ochronne według ogólnopolskich przepisów – dane orientacyjne. " +
            "Regulaminy okręgów PZW i łowisk często wprowadzają własne wymiary, dłuższe okresy ochronne " +
            "i limity dzienne. Przed wyjazdem zawsze sprawdź regulamin wody, na której łowisz.";

        #endregion

        #region Stan wody

        /// <summary>Opis najbliższego wodowskazu do panelu „Warunki teraz” albo null, gdy stacji brak.</summary>
        public static string HydroText(HydroStation h)
        {
            if (h == null) return null;
            var sb = new StringBuilder();
            sb.AppendLine("Stan wody (" + HydroService.SourceName + "): " + h.Name);
            sb.Append(string.Format("{0}{1} cm – {2}, pomiar {3:d.MM HH:mm}",
                h.IsWarning ? "⚠ " : "", h.Level, h.LevelStatusText, h.LevelTime));
            if (h.WaterTemperature.HasValue)
                sb.Append(string.Format("\nWoda (pomiar na stacji): {0:0.0} °C", h.WaterTemperature.Value));
            return sb.ToString();
        }

        #endregion

        #region Tekst do udostępnienia

        /// <summary>Prognoza na 7 dni jako zwykły tekst – do wklejenia w komunikatorze lub e-mailu.</summary>
        public static string ForecastText(Forecast f, Species species)
        {
            var now = f.Current.Time;
            var best = f.Days.OrderByDescending(d => d.Score).ThenBy(d => d.Date).First();

            var sb = new StringBuilder();
            sb.AppendLine(string.Format("🎣 NaRyby – prognoza brań: {0} ({1})", f.City.Name, species.Name.ToLower()));
            if (best.Score == 0 && species.IsClosed(best.Date))
                sb.AppendLine(string.Format("Okres ochronny {0} ({1}) – nie wolno go teraz łowić.", species.Genitive, species.ClosedText));
            else
                sb.AppendLine(string.Format("Najlepiej: {0}, {1} – {2} ({3}/100)",
                    DayName(best.Date, now), best.BestWindowText, FishingAdvisor.RatingText(best.Score).ToLower(), best.Score));
            sb.AppendLine();

            foreach (var d in f.Days)
            {
                sb.AppendLine(string.Format("{0}{1}: {2} {3}/100, najlepiej {4}",
                    d == best ? "★ " : "", DayName(d.Date, now), FishingAdvisor.Stars(d.Score), d.Score, d.BestWindowText));
                sb.AppendLine(string.Format("   powietrze {0:0}°/{1:0}°C, woda ok. {2:0}°C, wiatr do {3:0} km/h, opady {4:0.#} mm",
                    d.TempMin, d.TempMax, d.WaterTemperature, d.WindMax, d.PrecipitationSum));
            }

            string hydro = HydroText(f.Hydro);
            if (hydro != null)
            {
                sb.AppendLine();
                sb.AppendLine(hydro);
            }

            sb.AppendLine();
            sb.AppendLine(string.Format("Pogoda: Open-Meteo.com, stan na {0:d.MM HH:mm}", f.FetchedAt));
            sb.Append(RepoUrl.Replace("https://", ""));
            return sb.ToString();
        }

        #endregion
    }
}

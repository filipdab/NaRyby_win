using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace NaRyby_win
{
    /// <summary>
    /// Ocenia warunki wędkarskie (0–100) dla wybranego gatunku na podstawie ciśnienia i jego trendu, wiatru,
    /// szacowanej temperatury wody, opadów, zachmurzenia, pory dnia, sezonu i fazy Księżyca.
    /// </summary>
    public static class FishingAdvisor
    {
        private const int WindowLength = 3;
        private const int WaterLagHours = 72;

        public static void Analyze(Forecast forecast, Species species)
        {
            EstimateWaterTemperature(forecast.Hours);

            // Wczorajsze dni są potrzebne tylko do trendów; godziny zostają w forecast.Hours.
            DateTime now = forecast.Current.Time;
            forecast.Days.RemoveAll(d => d.Date < now.Date || d.Hours.Count == 0);

            int[] scores = ScoreHours(forecast, species);
            for (int i = 0; i < scores.Length; i++) forecast.Hours[i].Score = scores[i];

            var ranking = Species.All.Where(s => !s.IsGeneral)
                .Select(s => new { Species = s, Scores = s == species ? scores : ScoreHours(forecast, s) })
                .ToList();
            var indexOf = forecast.Hours.Select((h, i) => new { h, i }).ToDictionary(x => x.h, x => x.i);

            foreach (var day in forecast.Days)
            {
                var candidates = Candidates(day, now);
                ScoreDay(day, candidates);
                BuildReasons(day, species);

                day.SpeciesRanking.Clear();
                foreach (var r in ranking)
                {
                    int score = r.Species.IsClosed(day.Date) ? 0 : TopAverage(candidates.Select(h => r.Scores[indexOf[h]]));
                    day.SpeciesRanking.Add(new SpeciesScore(r.Species, score));
                }
                day.SpeciesRanking.Sort((a, b) => b.Score.CompareTo(a.Score));
            }
        }

        public static HourData HourAt(Forecast forecast, DateTime time)
        {
            DateTime hour = new DateTime(time.Year, time.Month, time.Day, time.Hour, 0, 0);
            return forecast.Hours.FirstOrDefault(h => h.Time == hour);
        }

        public static double? PressureTrend3h(Forecast forecast, HourData hour)
        {
            int i = forecast.Hours.IndexOf(hour);
            return i >= 3 ? forecast.Hours[i].Pressure - forecast.Hours[i - 3].Pressure : (double?)null;
        }

        /// <summary>
        /// Woda nagrzewa się i stygnie wolniej niż powietrze – przybliżamy ją średnią temperaturą
        /// powietrza z ostatnich 3 dni (to szacunek, nie pomiar).
        /// </summary>
        private static void EstimateWaterTemperature(List<HourData> hours)
        {
            double sum = 0;
            for (int i = 0; i < hours.Count; i++)
            {
                sum += hours[i].Temperature;
                if (i >= WaterLagHours) sum -= hours[i - WaterLagHours].Temperature;
                int count = Math.Min(i + 1, WaterLagHours);
                hours[i].WaterTemperature = Math.Max(1, sum / count);
            }
        }

        private static int[] ScoreHours(Forecast forecast, Species species)
        {
            var hours = forecast.Hours;
            var days = forecast.Days.ToDictionary(d => d.Date);
            var scores = new int[hours.Count];
            for (int i = 0; i < hours.Count; i++)
            {
                DayData day;
                days.TryGetValue(hours[i].Time.Date, out day);
                scores[i] = ScoreHour(hours, i, day, species);
            }
            return scores;
        }

        private static int ScoreHour(List<HourData> hours, int i, DayData day, Species sp)
        {
            var h = hours[i];
            if (sp.IsClosed(h.Time)) return 0;

            double s = 25;

            // Ciśnienie: stabilne lub powoli spadające sprzyja, gwałtowne zmiany szkodzą.
            double pressure = 0;
            if (i >= 3)
            {
                double dp3 = h.Pressure - hours[i - 3].Pressure;
                if (dp3 <= -3) pressure -= 5;
                else if (dp3 <= -1) pressure += 12;
                else if (dp3 < 1) pressure += 8;
                else if (dp3 < 2.5) pressure -= 3;
                else pressure -= 12;
            }
            if (i >= 24 && Math.Abs(h.Pressure - hours[i - 24].Pressure) > 10) pressure -= 10;
            if (h.Pressure >= 1008 && h.Pressure <= 1020) pressure += 5;
            else if (h.Pressure > 1027 || h.Pressure < 995) pressure -= 5;
            s += pressure * sp.PressureSensitivity;

            // Wiatr: drapieżniki lubią falę, ryby spokojnej wody (np. lin) – ciszę.
            if (h.WindSpeed < 3) s -= 5 * sp.WindPreference;
            else if (h.WindSpeed <= 18) s += 5 + 5 * sp.WindPreference;
            else if (h.WindSpeed > 40) s -= 30;
            else if (h.WindSpeed > 28) s -= 15;
            if (h.WindGusts > 60) s -= 15;

            if (h.WindSpeed >= 3)
            {
                if (h.WindDirection >= 45 && h.WindDirection <= 135) s -= 5;          // wschodni
                else if (h.WindDirection >= 180 && h.WindDirection <= 290) s += 3;    // południowo-zachodni / zachodni
            }

            // Temperatura wody względem preferencji gatunku, a także skrajne i nagłe zmiany temperatury powietrza.
            s += WaterScore(h.WaterTemperature, sp);
            if (h.Temperature < -5) s -= 10;
            else if (h.Temperature > 32) s -= 5;
            if (i >= 24 && Math.Abs(h.Temperature - hours[i - 24].Temperature) > 8) s -= 8;

            // Opady: mżawka pomaga, ulewa przeszkadza.
            if (h.Precipitation > 4) s -= 20;
            else if (h.Precipitation > 1) s -= 5;
            else if (h.Precipitation >= 0.1) s += 5 * sp.RainPreference;

            // Burza – zagrożenie dla wędkarza, niezależnie od brań.
            if (h.WeatherCode >= 95) s -= 40;

            if (day != null)
            {
                DateTime mid = h.Time.AddMinutes(30);
                bool night = mid < day.Sunrise.AddHours(-1.5) || mid > day.Sunset.AddHours(1.5);
                bool twilight = Math.Abs((mid - day.Sunrise).TotalHours) <= 1.5
                                || Math.Abs((mid - day.Sunset).TotalHours) <= 1.5;
                bool midday = mid > day.Sunrise.AddHours(3) && mid < day.Sunset.AddHours(-3);

                if (twilight) s += sp.TwilightBonus;
                else if (night) s += sp.NightBonus;
                else if (midday) s += sp.MiddayBonus;

                if (!night)
                {
                    if (h.CloudCover >= 40 && h.CloudCover <= 90) s += 5 * sp.CloudPreference;
                    else if (h.CloudCover < 15 && midday) s -= 5 * sp.CloudPreference;
                }

                if (midday && h.Temperature > 22 && h.CloudCover < 30) s -= 5;
            }

            s += 0.6 * sp.SeasonBonus[h.Time.Month - 1];
            if (MoonPhase.IsFavorable(MoonPhase.Age(h.Time))) s += 5;

            return (int)Math.Round(Math.Max(0, Math.Min(100, s)));
        }

        private static double WaterScore(double water, Species sp)
        {
            if (water >= sp.WaterOptMin && water <= sp.WaterOptMax) return 10;
            if (water >= sp.WaterMin && water < sp.WaterOptMin)
                return 10 * (water - sp.WaterMin) / (sp.WaterOptMin - sp.WaterMin);
            if (water > sp.WaterOptMax && water <= sp.WaterMax)
                return 10 * (sp.WaterMax - water) / (sp.WaterMax - sp.WaterOptMax);
            if (water < sp.WaterMin) return -Math.Min(35, 15 + 3 * (sp.WaterMin - water));
            return -10;
        }

        /// <summary>Dla dzisiejszego dnia bierzemy pod uwagę tylko godziny, które jeszcze nie minęły.</summary>
        private static List<HourData> Candidates(DayData day, DateTime now)
        {
            DateTime currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
            var candidates = day.Hours.Where(h => h.Time >= currentHour).ToList();
            return candidates.Count > 0 ? candidates : day.Hours;
        }

        private static int TopAverage(IEnumerable<int> scores)
        {
            return (int)Math.Round(scores.OrderByDescending(x => x).Take(4).Average());
        }

        private static void ScoreDay(DayData day, List<HourData> candidates)
        {
            day.Score = TopAverage(candidates.Select(h => h.Score));

            int len = Math.Min(WindowLength, candidates.Count);
            double best = -1;
            for (int i = 0; i + len <= candidates.Count; i++)
            {
                double avg = candidates.Skip(i).Take(len).Average(h => h.Score);
                if (avg > best)
                {
                    best = avg;
                    day.BestStart = candidates[i].Time;
                }
            }
            day.BestLength = len;
            day.BestScore = (int)Math.Round(best);
            day.MoonAge = MoonPhase.Age(day.Date.AddHours(12));
            day.WaterTemperature = day.Hours.Average(h => h.WaterTemperature);
        }

        private static void BuildReasons(DayData day, Species sp)
        {
            var r = day.Reasons;
            r.Clear();

            if (sp.IsClosed(day.Date))
                r.Add(string.Format("✖ Okres ochronny {0} ({1}) – połów zabroniony!", sp.Genitive, sp.ClosedText));

            double dp = day.Hours.Last().Pressure - day.Hours.First().Pressure;
            double avgPressure = day.Hours.Average(h => h.Pressure);
            string p = string.Format("{0:0} hPa", avgPressure);
            if (dp <= -10) r.Add("⚠ Gwałtowny spadek ciśnienia (" + Signed(dp) + ") – nadciąga front, brania mogą ustać.");
            else if (dp <= -2) r.Add("✔ Ciśnienie powoli spada (" + Signed(dp) + ") – ryby często intensywnie żerują przed zmianą pogody.");
            else if (dp < 2) r.Add("✔ Ciśnienie stabilne (" + p + ") – ryby żerują regularnie.");
            else if (dp < 8) r.Add("• Ciśnienie rośnie (" + Signed(dp) + ") – ryby mogą być nieco ospałe.");
            else r.Add("✖ Szybki wzrost ciśnienia (" + Signed(dp) + ") – zwykle słabe brania.");

            double water = day.WaterTemperature;
            if (water >= sp.WaterOptMin && water <= sp.WaterOptMax)
                r.Add(string.Format("✔ Woda ok. {0:0}°C – optymalna dla {1}.", water, sp.Genitive));
            else if (water < sp.WaterMin)
                r.Add(string.Format("✖ Woda ok. {0:0}°C – za zimna dla {1}, ryby mało aktywne.", water, sp.Genitive));
            else if (water > sp.WaterMax)
                r.Add(string.Format("✖ Woda ok. {0:0}°C – za ciepła dla {1}, mało tlenu.", water, sp.Genitive));
            else
                r.Add(string.Format("• Woda ok. {0:0}°C – {1} dla {2}.", water,
                    water < sp.WaterOptMin ? "nieco za chłodna" : "nieco za ciepła", sp.Genitive));

            if (day.WindMax > 40) r.Add(string.Format("✖ Bardzo silny wiatr (do {0:0} km/h) – trudne i niebezpieczne warunki.", day.WindMax));
            else if (day.WindMax > 28) r.Add(string.Format("• Silny wiatr (do {0:0} km/h) – szukaj osłoniętych miejsc.", day.WindMax));
            else if (day.WindMax >= 5)
                r.Add(sp.WindPreference < 0
                    ? string.Format("• Wiatr do {0:0} km/h – {1} woli spokojną wodę, szukaj osłoniętych zatok.", day.WindMax, sp.Name.ToLower())
                    : string.Format("✔ Umiarkowany wiatr (do {0:0} km/h) – woda dobrze natleniona.", day.WindMax));
            else r.Add("• Prawie bezwietrznie – lustro wody, ryby ostrożniejsze.");

            if (day.PrecipitationSum >= 10) r.Add(string.Format("✖ Obfite opady ({0:0.#} mm).", day.PrecipitationSum));
            else if (day.PrecipitationSum >= 0.5) r.Add(string.Format("• Przelotne opady ({0:0.#} mm) – lekki deszcz potrafi pobudzić żerowanie.", day.PrecipitationSum));
            else r.Add("✔ Bez opadów.");

            if (day.Hours.Any(h => h.WeatherCode >= 95)) r.Add("⚠ Ryzyko burzy – nie łów z wędką węglową w czasie burzy!");

            int season = sp.SeasonBonus[day.Date.Month - 1];
            if (!sp.IsGeneral && season >= 6) r.Add(string.Format("✔ {0} to szczyt sezonu na {1}.", Capitalize(MonthNominative(day.Date.Month)), sp.Accusative));
            else if (!sp.IsGeneral && season <= -8) r.Add(string.Format("✖ Poza sezonem – {0} rzadko żeruje w tym okresie.", sp.Name.ToLower()));

            string moon = MoonPhase.Name(day.MoonAge);
            r.Add(MoonPhase.IsFavorable(day.MoonAge)
                ? "✔ Księżyc: " + moon + " – okres wzmożonej aktywności ryb."
                : "• Księżyc: " + moon + ".");

            r.Add(string.Format("Najlepiej: {0} (ocena {1}/100). Wschód {2:HH:mm}, zachód {3:HH:mm}.",
                day.BestWindowText, day.BestScore, day.Sunrise, day.Sunset));
        }

        private static string MonthNominative(int month)
        {
            string[] names = { "styczeń", "luty", "marzec", "kwiecień", "maj", "czerwiec", "lipiec", "sierpień", "wrzesień", "październik", "listopad", "grudzień" };
            return names[month - 1];
        }

        private static string Capitalize(string s)
        {
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        /// <summary>Porada zależna od szacowanej temperatury wody.</summary>
        public static string WaterTip(Species sp, double water)
        {
            if (water < sp.WaterOptMin) return sp.ColdTip;
            if (water > sp.WaterOptMax) return sp.WarmTip;
            return string.Format("Woda ok. {0:0}°C to optymalna temperatura – ryby powinny aktywnie żerować w całym łowisku.", water);
        }

        private static string Signed(double value)
        {
            return (value > 0 ? "+" : "") + value.ToString("0.0") + " hPa";
        }

        public static string RatingText(int score)
        {
            if (score >= 75) return "Doskonale";
            if (score >= 62) return "Bardzo dobrze";
            if (score >= 50) return "Dobrze";
            if (score >= 38) return "Przeciętnie";
            return "Słabo";
        }

        public static string Stars(int score)
        {
            int n = score >= 75 ? 5 : score >= 62 ? 4 : score >= 50 ? 3 : score >= 38 ? 2 : 1;
            return new string('★', n) + new string('☆', 5 - n);
        }

        public static Color ScoreColor(int score)
        {
            if (score >= 75) return Color.FromArgb(46, 160, 67);
            if (score >= 62) return Color.FromArgb(120, 190, 60);
            if (score >= 50) return Color.FromArgb(230, 190, 40);
            if (score >= 38) return Color.FromArgb(240, 140, 40);
            return Color.FromArgb(215, 70, 60);
        }

        public static string WeatherDescription(int code)
        {
            if (code == 0) return "Bezchmurnie";
            if (code == 1) return "Przeważnie pogodnie";
            if (code == 2) return "Częściowe zachmurzenie";
            if (code == 3) return "Pochmurno";
            if (code == 45 || code == 48) return "Mgła";
            if (code >= 51 && code <= 57) return "Mżawka";
            if (code >= 61 && code <= 67) return "Deszcz";
            if (code >= 71 && code <= 77) return "Śnieg";
            if (code >= 80 && code <= 82) return "Przelotny deszcz";
            if (code >= 85 && code <= 86) return "Przelotny śnieg";
            if (code >= 95) return "Burza";
            return "—";
        }

        public static string WindDirectionName(double degrees)
        {
            string[] names = { "północny", "płn.-wsch.", "wschodni", "płd.-wsch.", "południowy", "płd.-zach.", "zachodni", "płn.-zach." };
            int index = (int)Math.Round(((degrees % 360) + 360) % 360 / 45.0) % 8;
            return names[index];
        }
    }
}

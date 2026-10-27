using System;
using System.Collections.Generic;

namespace NaRyby_win
{
    public class City
    {
        public City(string name, double latitude, double longitude)
        {
            Name = name;
            Latitude = latitude;
            Longitude = longitude;
            Description = name;
        }

        public string Name { get; private set; }
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }

        // Pełny opis (np. "Mikołajki, Warmińsko-Mazurskie (Polska)") używany przy wyborze wyniku wyszukiwania.
        public string Description { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public class HourData
    {
        public DateTime Time { get; set; }
        public double Temperature { get; set; }
        public double Pressure { get; set; }
        public double WindSpeed { get; set; }
        public double WindDirection { get; set; }
        public double WindGusts { get; set; }
        public double Precipitation { get; set; }
        public double CloudCover { get; set; }
        public int WeatherCode { get; set; }

        /// <summary>Szacowana temperatura wody (patrz FishingAdvisor.EstimateWaterTemperature).</summary>
        public double WaterTemperature { get; set; }
        public int Score { get; set; }
    }

    public class SpeciesScore
    {
        public SpeciesScore(Species species, int score)
        {
            Species = species;
            Score = score;
        }

        public Species Species { get; private set; }
        public int Score { get; private set; }
    }

    public class DayData
    {
        public DayData()
        {
            Hours = new List<HourData>();
            Reasons = new List<string>();
            SpeciesRanking = new List<SpeciesScore>();
        }

        public DateTime Date { get; set; }
        public DateTime Sunrise { get; set; }
        public DateTime Sunset { get; set; }
        public double TempMin { get; set; }
        public double TempMax { get; set; }
        public double PrecipitationSum { get; set; }
        public double WindMax { get; set; }
        public List<HourData> Hours { get; private set; }

        public int Score { get; set; }
        public DateTime BestStart { get; set; }
        public int BestLength { get; set; }
        public int BestScore { get; set; }
        public double MoonAge { get; set; }
        public double WaterTemperature { get; set; }
        public List<string> Reasons { get; private set; }
        public List<SpeciesScore> SpeciesRanking { get; private set; }

        public string BestWindowText
        {
            get
            {
                if (BestLength == 0) return "-";
                return BestStart.ToString("HH:mm") + "–" + BestStart.AddHours(BestLength).ToString("HH:mm");
            }
        }
    }

    public class CurrentData
    {
        public DateTime Time { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public double Pressure { get; set; }
        public double WindSpeed { get; set; }
        public double WindDirection { get; set; }
        public double CloudCover { get; set; }
        public double Precipitation { get; set; }
        public int WeatherCode { get; set; }
    }

    public class Forecast
    {
        public Forecast()
        {
            Hours = new List<HourData>();
            Days = new List<DayData>();
        }

        public City City { get; set; }
        public string TimeZone { get; set; }
        public CurrentData Current { get; set; }
        public List<HourData> Hours { get; private set; }
        public List<DayData> Days { get; private set; }
        public DateTime FetchedAt { get; set; }

        /// <summary>Najbliższy wodowskaz IMGW albo null (brak stacji w pobliżu lub błąd pobierania).</summary>
        public HydroStation Hydro { get; set; }
    }
}

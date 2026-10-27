using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NaRyby_win
{
    /// <summary>Ostatni pomiar na wodowskazie IMGW najbliższym łowiska.</summary>
    public class HydroStation
    {
        public string Station { get; set; }
        public string River { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double DistanceKm { get; set; }

        /// <summary>Stan wody w cm na wodowskazie (względny – porównywalny tylko ze stanami ostrzegawczym i alarmowym tej stacji).</summary>
        public int Level { get; set; }
        public DateTime LevelTime { get; set; }
        public int? WarningLevel { get; set; }
        public int? AlarmLevel { get; set; }

        /// <summary>Zmierzona temperatura wody – tylko jeśli stacja ją mierzy, a pomiar jest świeży.</summary>
        public double? WaterTemperature { get; set; }

        public bool IsAlarm
        {
            get { return AlarmLevel.HasValue && Level >= AlarmLevel.Value; }
        }

        public bool IsWarning
        {
            get { return WarningLevel.HasValue && Level >= WarningLevel.Value; }
        }

        /// <summary>Krótki opis stanu, np. „stan ostrzegawczy” albo „120 cm poniżej ostrzegawczego”.</summary>
        public string LevelStatusText
        {
            get
            {
                if (IsAlarm) return "stan alarmowy!";
                if (IsWarning) return "stan ostrzegawczy";
                if (WarningLevel.HasValue) return string.Format("{0} cm poniżej ostrzegawczego", WarningLevel.Value - Level);
                return "brak stanów granicznych";
            }
        }

        /// <summary>Nazwa stacji z rzeką i odległością, np. „Bug – Wyszków, 23 km”.</summary>
        public string Name
        {
            get
            {
                // Stacje morskie mają zamiast rzeki „-”.
                bool hasRiver = !string.IsNullOrWhiteSpace(River) && River != "-";
                return string.Format("{0}{1}, {2:0} km", hasRiver ? River + " – " : "", Station, DistanceKm);
            }
        }
    }

    /// <summary>
    /// Stan wody z publicznego API IMGW (danepubliczne.imgw.pl, nie wymaga klucza).
    /// API zwraca ostatnie pomiary ze wszystkich wodowskazów naraz, więc lista jest pobierana raz
    /// i trzymana w pamięci – porównanie wielu łowisk nie wysyła wielu zapytań.
    /// </summary>
    public static class HydroService
    {
        public const string SourceName = "IMGW-PIB";

        /// <summary>Dalsze wodowskazy nie mówią wiele o łowisku, więc ich nie pokazujemy.</summary>
        public const double MaxDistanceKm = 25;

        private const string Url = "https://danepubliczne.imgw.pl/api/data/hydro";
        private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";
        private static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan MaxLevelAge = TimeSpan.FromHours(48);
        private static readonly TimeSpan MaxTemperatureAge = TimeSpan.FromHours(36);

        // Zapytania idą przez klienta z WeatherService (TLS 1.2 na .NET Framework, nagłówek User-Agent).
        private static HttpClient Http
        {
            get { return WeatherService.Http; }
        }
        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);
        private static StationDto[] _stations;
        private static DateTime _fetchedAt;

        /// <summary>Najbliższy wodowskaz z aktualnym pomiarem albo null, gdy w pobliżu nie ma żadnego.</summary>
        public static async Task<HydroStation> NearestAsync(City city, DateTime now)
        {
            var stations = await GetStationsAsync().ConfigureAwait(false);

            HydroStation best = null;
            foreach (var s in stations)
            {
                double lat, lon;
                int level;
                DateTime levelTime;
                // Część stacji (np. jeziorowych) ma współrzędne 0/0 – pomijamy je.
                if (!TryDouble(s.Lat, out lat) || !TryDouble(s.Lon, out lon) || (lat == 0 && lon == 0)) continue;
                if (!TryInt(s.Level, out level) || !TryTime(s.LevelTime, out levelTime)) continue;
                if (now - levelTime > MaxLevelAge) continue;

                double distance = DistanceKm(city.Latitude, city.Longitude, lat, lon);
                if (distance > MaxDistanceKm || (best != null && distance >= best.DistanceKm)) continue;

                best = new HydroStation
                {
                    Station = s.Station,
                    River = s.River,
                    Latitude = lat,
                    Longitude = lon,
                    DistanceKm = distance,
                    Level = level,
                    LevelTime = levelTime,
                    WarningLevel = Int(s.WarningLevel),
                    AlarmLevel = Int(s.AlarmLevel)
                };

                double temperature;
                DateTime temperatureTime;
                // Czujniki potrafią zwracać bzdury (np. 90 °C), więc odrzucamy wartości spoza zakresu.
                if (TryDouble(s.WaterTemperature, out temperature) && TryTime(s.WaterTemperatureTime, out temperatureTime)
                    && now - temperatureTime <= MaxTemperatureAge && temperature >= 0 && temperature <= 35)
                    best.WaterTemperature = temperature;
            }
            return best;
        }

        private static async Task<StationDto[]> GetStationsAsync()
        {
            await Lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_stations == null || DateTime.Now - _fetchedAt > CacheTime)
                {
                    using (var response = await Http.GetAsync(Url).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        {
                            var serializer = new DataContractJsonSerializer(typeof(StationDto[]));
                            _stations = (StationDto[])serializer.ReadObject(stream);
                        }
                    }
                    _fetchedAt = DateTime.Now;
                }
                return _stations;
            }
            finally
            {
                Lock.Release();
            }
        }

        private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLon = (lon2 - lon1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                       + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * R * Math.Asin(Math.Sqrt(a));
        }

        // IMGW zwraca wszystkie liczby i daty jako tekst albo null.
        private static bool TryDouble(string s, out double value)
        {
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryInt(string s, out int value)
        {
            double d;
            bool ok = TryDouble(s, out d);
            value = ok ? (int)Math.Round(d) : 0;
            return ok;
        }

        private static bool TryTime(string s, out DateTime value)
        {
            return DateTime.TryParseExact(s, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }

        private static int? Int(string s)
        {
            int value;
            return TryInt(s, out value) ? value : (int?)null;
        }

        [DataContract]
        public class StationDto
        {
            [DataMember(Name = "stacja")] public string Station { get; set; }
            [DataMember(Name = "rzeka")] public string River { get; set; }
            [DataMember(Name = "lon")] public string Lon { get; set; }
            [DataMember(Name = "lat")] public string Lat { get; set; }
            [DataMember(Name = "stan_alarmowy")] public string AlarmLevel { get; set; }
            [DataMember(Name = "stan_ostrzegawczy")] public string WarningLevel { get; set; }
            [DataMember(Name = "stan_wody")] public string Level { get; set; }
            [DataMember(Name = "stan_wody_data_pomiaru")] public string LevelTime { get; set; }
            [DataMember(Name = "temperatura_wody")] public string WaterTemperature { get; set; }
            [DataMember(Name = "temperatura_wody_data_pomiaru")] public string WaterTemperatureTime { get; set; }
        }
    }
}

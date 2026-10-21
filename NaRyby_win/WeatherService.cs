using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;

namespace NaRyby_win
{
    /// <summary>
    /// Pobiera prognozę i wyszukuje miejscowości przez darmowe API Open-Meteo (nie wymaga klucza).
    /// </summary>
    public class WeatherService
    {
        private const string TimeFormat = "yyyy-MM-dd'T'HH:mm";

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NaRyby/1.0");
            return client;
        }

        public async Task<List<City>> SearchCitiesAsync(string name)
        {
            string url = "https://geocoding-api.open-meteo.com/v1/search?count=10&language=pl&format=json&name="
                         + Uri.EscapeDataString(name);
            var response = await GetJsonAsync<GeoResponse>(url).ConfigureAwait(false);

            var cities = new List<City>();
            if (response.Results == null) return cities;

            foreach (var r in response.Results)
            {
                string description = r.Name;
                if (!string.IsNullOrEmpty(r.Admin2)) description += ", " + r.Admin2;
                if (!string.IsNullOrEmpty(r.Admin1)) description += ", " + r.Admin1;
                if (!string.IsNullOrEmpty(r.Country)) description += " (" + r.Country + ")";
                cities.Add(new City(r.Name, r.Latitude, r.Longitude) { Description = description });
            }
            return cities;
        }

        public async Task<Forecast> GetForecastAsync(City city)
        {
            // past_days=3 daje historię potrzebną do trendu ciśnienia i oszacowania temperatury wody.
            string url = string.Format(CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0:0.####}&longitude={1:0.####}" +
                "&current=temperature_2m,relative_humidity_2m,pressure_msl,wind_speed_10m,wind_direction_10m,cloud_cover,precipitation,weather_code" +
                "&hourly=temperature_2m,pressure_msl,wind_speed_10m,wind_direction_10m,wind_gusts_10m,precipitation,cloud_cover,weather_code" +
                "&daily=sunrise,sunset,temperature_2m_max,temperature_2m_min,precipitation_sum,wind_speed_10m_max" +
                "&timezone=auto&past_days=3&forecast_days=7&wind_speed_unit=kmh",
                city.Latitude, city.Longitude);

            var dto = await GetJsonAsync<ForecastResponse>(url).ConfigureAwait(false);
            if (dto.Hourly == null || dto.Daily == null || dto.Current == null)
                throw new InvalidOperationException("Serwer zwrócił niekompletne dane.");

            var forecast = new Forecast
            {
                City = city,
                TimeZone = dto.TimeZone,
                FetchedAt = DateTime.Now,
                Current = new CurrentData
                {
                    Time = ParseTime(dto.Current.Time),
                    Temperature = dto.Current.Temperature,
                    Humidity = dto.Current.Humidity,
                    Pressure = dto.Current.Pressure,
                    WindSpeed = dto.Current.WindSpeed,
                    WindDirection = dto.Current.WindDirection,
                    CloudCover = dto.Current.CloudCover,
                    Precipitation = dto.Current.Precipitation,
                    WeatherCode = dto.Current.WeatherCode
                }
            };

            var h = dto.Hourly;
            for (int i = 0; i < h.Time.Length; i++)
            {
                // Pomijamy godziny bez kluczowych danych (zdarza się na końcu zakresu prognozy).
                if (Value(h.Pressure, i) == null || Value(h.Temperature, i) == null) continue;

                forecast.Hours.Add(new HourData
                {
                    Time = ParseTime(h.Time[i]),
                    Temperature = Value(h.Temperature, i) ?? 0,
                    Pressure = Value(h.Pressure, i) ?? 0,
                    WindSpeed = Value(h.WindSpeed, i) ?? 0,
                    WindDirection = Value(h.WindDirection, i) ?? 0,
                    WindGusts = Value(h.WindGusts, i) ?? 0,
                    Precipitation = Value(h.Precipitation, i) ?? 0,
                    CloudCover = Value(h.CloudCover, i) ?? 0,
                    WeatherCode = h.WeatherCode != null && i < h.WeatherCode.Length ? h.WeatherCode[i] ?? 0 : 0
                });
            }

            var d = dto.Daily;
            for (int i = 0; i < d.Time.Length; i++)
            {
                var day = new DayData
                {
                    Date = DateTime.ParseExact(d.Time[i], "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    TempMax = Value(d.TempMax, i) ?? 0,
                    TempMin = Value(d.TempMin, i) ?? 0,
                    PrecipitationSum = Value(d.PrecipitationSum, i) ?? 0,
                    WindMax = Value(d.WindMax, i) ?? 0
                };
                day.Sunrise = ParseTime(d.Sunrise[i]);
                day.Sunset = ParseTime(d.Sunset[i]);
                forecast.Days.Add(day);
            }

            foreach (var hour in forecast.Hours)
            {
                var day = forecast.Days.Find(x => x.Date == hour.Time.Date);
                if (day != null) day.Hours.Add(hour);
            }

            return forecast;
        }

        private static double? Value(double?[] values, int index)
        {
            return values != null && index < values.Length ? values[index] : null;
        }

        private static DateTime ParseTime(string s)
        {
            return DateTime.ParseExact(s, TimeFormat, CultureInfo.InvariantCulture);
        }

        private static async Task<T> GetJsonAsync<T>(string url)
        {
            using (var response = await Http.GetAsync(url).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    var serializer = new DataContractJsonSerializer(typeof(T));
                    return (T)serializer.ReadObject(stream);
                }
            }
        }

        #region Obiekty odpowiedzi JSON

        [DataContract]
        public class GeoResponse
        {
            [DataMember(Name = "results")] public GeoResult[] Results { get; set; }
        }

        [DataContract]
        public class GeoResult
        {
            [DataMember(Name = "name")] public string Name { get; set; }
            [DataMember(Name = "latitude")] public double Latitude { get; set; }
            [DataMember(Name = "longitude")] public double Longitude { get; set; }
            [DataMember(Name = "country")] public string Country { get; set; }
            [DataMember(Name = "admin1")] public string Admin1 { get; set; }
            [DataMember(Name = "admin2")] public string Admin2 { get; set; }
        }

        [DataContract]
        public class ForecastResponse
        {
            [DataMember(Name = "timezone")] public string TimeZone { get; set; }
            [DataMember(Name = "current")] public CurrentDto Current { get; set; }
            [DataMember(Name = "hourly")] public HourlyDto Hourly { get; set; }
            [DataMember(Name = "daily")] public DailyDto Daily { get; set; }
        }

        [DataContract]
        public class CurrentDto
        {
            [DataMember(Name = "time")] public string Time { get; set; }
            [DataMember(Name = "temperature_2m")] public double Temperature { get; set; }
            [DataMember(Name = "relative_humidity_2m")] public double Humidity { get; set; }
            [DataMember(Name = "pressure_msl")] public double Pressure { get; set; }
            [DataMember(Name = "wind_speed_10m")] public double WindSpeed { get; set; }
            [DataMember(Name = "wind_direction_10m")] public double WindDirection { get; set; }
            [DataMember(Name = "cloud_cover")] public double CloudCover { get; set; }
            [DataMember(Name = "precipitation")] public double Precipitation { get; set; }
            [DataMember(Name = "weather_code")] public int WeatherCode { get; set; }
        }

        [DataContract]
        public class HourlyDto
        {
            [DataMember(Name = "time")] public string[] Time { get; set; }
            [DataMember(Name = "temperature_2m")] public double?[] Temperature { get; set; }
            [DataMember(Name = "pressure_msl")] public double?[] Pressure { get; set; }
            [DataMember(Name = "wind_speed_10m")] public double?[] WindSpeed { get; set; }
            [DataMember(Name = "wind_direction_10m")] public double?[] WindDirection { get; set; }
            [DataMember(Name = "wind_gusts_10m")] public double?[] WindGusts { get; set; }
            [DataMember(Name = "precipitation")] public double?[] Precipitation { get; set; }
            [DataMember(Name = "cloud_cover")] public double?[] CloudCover { get; set; }
            [DataMember(Name = "weather_code")] public int?[] WeatherCode { get; set; }
        }

        [DataContract]
        public class DailyDto
        {
            [DataMember(Name = "time")] public string[] Time { get; set; }
            [DataMember(Name = "sunrise")] public string[] Sunrise { get; set; }
            [DataMember(Name = "sunset")] public string[] Sunset { get; set; }
            [DataMember(Name = "temperature_2m_max")] public double?[] TempMax { get; set; }
            [DataMember(Name = "temperature_2m_min")] public double?[] TempMin { get; set; }
            [DataMember(Name = "precipitation_sum")] public double?[] PrecipitationSum { get; set; }
            [DataMember(Name = "wind_speed_10m_max")] public double?[] WindMax { get; set; }
        }

        #endregion
    }
}

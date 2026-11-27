using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using NaRyby_win;

namespace NaRyby_web
{
    /// <summary>
    /// Serwer wersji webowej: API JSON nad wspólną logiką NaRyby + statyczna strona z wwwroot.
    /// Domyślnie nasłuchuje na http://0.0.0.0:5080 (zmiana: zmienna ASPNETCORE_URLS).
    /// </summary>
    public static class Program
    {
        private static readonly CultureInfo Polish = new CultureInfo("pl-PL");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static void Main(string[] args)
        {
            // Teksty ze wspólnej logiki (string.Format bez kultury) mają wyglądać jak w polskim Windows.
            CultureInfo.DefaultThreadCurrentCulture = Polish;
            CultureInfo.DefaultThreadCurrentUICulture = Polish;
            CultureInfo.CurrentCulture = Polish;
            CultureInfo.CurrentUICulture = Polish;

            var builder = WebApplication.CreateBuilder(args);
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")) && !args.Any(a => a.StartsWith("--urls")))
                builder.WebHost.UseUrls("http://0.0.0.0:5080");

            var app = builder.Build();

            var store = new CityStore();
            store.Load();
            var storeLock = new object();
            var weather = new WeatherService();
            var cache = new ForecastCache(weather);

            app.UseDefaultFiles();
            app.UseStaticFiles(new StaticFileOptions
            {
                // Telefon ma zawsze sprawdzać, czy strona się nie zmieniła (ETag), zamiast trzymać starą wersję.
                OnPrepareResponse = ctx => ctx.Context.Response.Headers["Cache-Control"] = "no-cache"
            });

            app.MapGet("/api/cities", () =>
            {
                lock (storeLock) return Results.Json(store.Cities.Select(CityDto).ToList());
            });

            app.MapGet("/api/search", async (string q) =>
            {
                if (string.IsNullOrWhiteSpace(q)) return Results.Json(new object[0]);
                try
                {
                    var found = await weather.SearchCitiesAsync(q.Trim());
                    return Results.Json(found.Select(c => new { name = c.Name, description = c.Description, latitude = c.Latitude, longitude = c.Longitude }));
                }
                catch (Exception ex)
                {
                    return Error("Nie udało się wyszukać miejscowości: " + ex.Message, 502);
                }
            });

            app.MapPost("/api/cities", (NewCity c) =>
            {
                if (c == null || string.IsNullOrWhiteSpace(c.Name) || Math.Abs(c.Latitude) > 90 || Math.Abs(c.Longitude) > 180)
                    return Error("Nieprawidłowa miejscowość.", 400);
                string name = c.Name.Replace("|", " ").Trim();
                string description = string.IsNullOrWhiteSpace(c.Description) ? name : c.Description.Replace("|", " ").Trim();
                lock (storeLock)
                {
                    var added = store.Add(new City(name, c.Latitude, c.Longitude) { Description = description });
                    return Results.Json(CityDto(added));
                }
            });

            app.MapDelete("/api/cities", (string name) =>
            {
                lock (storeLock)
                {
                    var city = store.Cities.FirstOrDefault(x => x.Name == name);
                    if (city == null) return Error("Nie ma takiej miejscowości na liście.", 404);
                    if (store.Cities.Count <= 1) return Error("Na liście musi zostać co najmniej jedna miejscowość.", 400);
                    store.Remove(city);
                    return Results.Json(store.Cities.Select(CityDto).ToList());
                }
            });

            app.MapGet("/api/species", () => Results.Json(Species.All.Select(s => new
            {
                name = s.Name,
                isGeneral = s.IsGeneral,
                closed = s.ClosedText,
                closedToday = s.IsClosed(DateTime.Today)
            })));

            app.MapGet("/api/forecast", async (string city, string species) =>
            {
                City c;
                lock (storeLock) c = FindCity(store, city);
                if (c == null) return Error("Nie ma takiej miejscowości na liście.", 404);
                var sp = FindSpecies(species);

                ForecastCache.Entry entry;
                try
                {
                    entry = await cache.GetAsync(c);
                }
                catch (Exception ex)
                {
                    return Error("Nie udało się pobrać prognozy pogody. " + ex.Message, 502);
                }

                lock (entry)
                {
                    FishingAdvisor.Analyze(entry.Forecast, sp);
                    // Serializacja jeszcze pod blokadą – inne zapytanie może zaraz przeliczyć tę prognozę dla innego gatunku.
                    return Results.Content(JsonSerializer.Serialize(ForecastDto(entry.Forecast, sp), JsonOptions), "application/json; charset=utf-8");
                }
            });

            app.MapGet("/api/compare", async (string species) =>
            {
                var sp = FindSpecies(species);
                List<City> cities;
                lock (storeLock) cities = store.Cities.ToList();

                var results = await Task.WhenAll(cities.Select(async c =>
                {
                    var r = new SpotResult { City = c };
                    try
                    {
                        var entry = await cache.GetAsync(c);
                        lock (entry)
                        {
                            FishingAdvisor.Analyze(entry.Forecast, sp);
                            // Kopia dni, bo obiekt prognozy jest współdzielony i może zostać przeliczony dla innego gatunku.
                            r.Forecast = Snapshot(entry.Forecast);
                        }
                    }
                    catch (Exception ex)
                    {
                        r.Error = ex.Message;
                    }
                    return r;
                }));

                var sorted = results
                    .OrderBy(r => r.Forecast == null)
                    .ThenByDescending(r => r.Best != null ? r.Best.Score : 0)
                    .ThenBy(r => r.City.Name)
                    .ToList();

                var ok = sorted.Where(r => r.Forecast != null).ToList();
                if (ok.Count == 0) return Error("Nie udało się pobrać żadnej prognozy. Sprawdź połączenie z internetem.", 502);

                var now = ok[0].Forecast.Current.Time;
                var dates = ok[0].Forecast.Days.Select(d => d.Date).ToList();
                var top = ok[0];
                string summary = top.Best.Score > 0
                    ? string.Format("Najlepsze łowisko w tym tygodniu: {0} – {1}, {2} ({3}/100).",
                        top.City.Name, Reports.DayName(top.Best.Date, now), top.Best.BestWindowText, top.Best.Score)
                    : string.Format("Okres ochronny {0} – w tym tygodniu nie wolno go łowić.", sp.Genitive);
                int errors = sorted.Count - ok.Count;
                if (errors > 0) summary += string.Format(" Nie udało się pobrać: {0}.", errors);

                return Results.Json(new
                {
                    species = sp.Name,
                    summary,
                    days = dates.Select(d => new { date = Iso(d), shortName = Reports.ShortDayName(d, now) }),
                    rows = sorted.Select(r => new
                    {
                        city = r.City.Name,
                        error = r.Error,
                        best = r.Forecast == null ? "błąd pobierania"
                            : r.Best.Score > 0 ? string.Format("{0}, {1}", Reports.ShortDayName(r.Best.Date, now), r.Best.BestWindowText) : "–",
                        scores = dates.Select(date =>
                        {
                            var d = r.DayAt(date);
                            if (d == null) return null;
                            return new
                            {
                                score = d.Score,
                                color = Hex(FishingAdvisor.ScoreColor(d.Score)),
                                title = string.Format("{0}: {1}, najlepiej {2}", Reports.DayName(d.Date, now), FishingAdvisor.RatingText(d.Score), d.BestWindowText),
                                isBest = r.Best == d
                            };
                        })
                    })
                });
            });

            app.MapGet("/api/protection", () =>
            {
                var today = DateTime.Today;
                return Results.Json(new
                {
                    note = Reports.ProtectionNote,
                    rows = Species.All.Where(s => !s.IsGeneral).Select(s =>
                    {
                        string status = Reports.ProtectionStatus(s, today);
                        return new
                        {
                            name = s.Name,
                            closed = s.ClosedText ?? "brak",
                            minLength = Reports.MinLengthText(s),
                            status,
                            isClosed = s.IsClosed(today),
                            soon = !s.IsClosed(today) && status.Contains("ochrona")
                        };
                    })
                });
            });

            app.Run();
        }

        public class NewCity
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }

        private static object CityDto(City c)
        {
            return new { name = c.Name, latitude = c.Latitude, longitude = c.Longitude };
        }

        private static IResult Error(string message, int status)
        {
            return Results.Json(new { error = message }, statusCode: status);
        }

        private static City FindCity(CityStore store, string name)
        {
            if (string.IsNullOrEmpty(name)) return store.Cities.FirstOrDefault();
            return store.Cities.FirstOrDefault(c => c.Name == name);
        }

        private static Species FindSpecies(string name)
        {
            return Species.All.FirstOrDefault(s => s.Name == name) ?? Species.All[0];
        }

        private static string Iso(DateTime t)
        {
            return t.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
        }

        private static string Hex(System.Drawing.Color c)
        {
            return string.Format("#{0:x2}{1:x2}{2:x2}", c.R, c.G, c.B);
        }

        /// <summary>Płytka kopia prognozy z wynikami ocen – do porównania łowisk.</summary>
        private static Forecast Snapshot(Forecast f)
        {
            var copy = new Forecast { City = f.City, Current = f.Current, TimeZone = f.TimeZone, FetchedAt = f.FetchedAt, Hydro = f.Hydro };
            foreach (var d in f.Days)
            {
                copy.Days.Add(new DayData
                {
                    Date = d.Date, Score = d.Score, BestStart = d.BestStart, BestLength = d.BestLength, BestScore = d.BestScore
                });
            }
            return copy;
        }

        private static object ForecastDto(Forecast f, Species sp)
        {
            var now = f.Current.Time;
            var c = f.Current;
            var nowHour = FishingAdvisor.HourAt(f, now);
            var today = f.Days.FirstOrDefault(d => d.Date == now.Date);
            double moonAge = MoonPhase.Age(now);
            var best = f.Days.OrderByDescending(d => d.Score).ThenBy(d => d.Date).First();

            string recommendation, recommendationReason;
            if (best.Score == 0 && sp.IsClosed(best.Date))
            {
                var alternative = best.SpeciesRanking.First();
                recommendation = string.Format("Okres ochronny {0} ({1}) – nie wolno go teraz łowić.", sp.Genitive, sp.ClosedText);
                recommendationReason = string.Format("W tym tygodniu najlepiej zapowiada się: {0} ({1}/100).",
                    alternative.Species.Name.ToLower(), alternative.Score);
            }
            else
            {
                recommendation = string.Format("Najlepiej jechać na {0}: {1}, {2} — {3} ({4}/100)",
                    sp.Accusative, Reports.DayName(best.Date, now), best.BestWindowText,
                    FishingAdvisor.RatingText(best.Score), best.Score);
                recommendationReason = best.Reasons.FirstOrDefault(r => r.StartsWith("✔")) ?? best.Reasons.FirstOrDefault();
            }

            double? dp = nowHour != null ? FishingAdvisor.PressureTrend3h(f, nowHour) : null;
            string trend = dp.HasValue
                ? string.Format("{0} ({1}{2:0.0} hPa/3h)", dp <= -1 ? "↓" : dp >= 1 ? "↑" : "→", dp > 0 ? "+" : "", dp)
                : "";

            var h = f.Hydro;
            return new
            {
                city = f.City.Name,
                species = sp.Name,
                speciesIsGeneral = sp.IsGeneral,
                fetchedAt = f.FetchedAt.ToString("HH:mm"),
                timeZone = f.TimeZone,
                now = Iso(now),
                nowText = now.ToString("dddd, d MMMM, HH:mm", Polish),
                bestIndex = f.Days.IndexOf(best),
                recommendation,
                recommendationReason,
                recommendationColor = Hex(FishingAdvisor.ScoreColor(best.Score)),
                current = new
                {
                    weather = FishingAdvisor.WeatherDescription(c.WeatherCode),
                    temperature = c.Temperature,
                    water = nowHour != null ? (double?)nowHour.WaterTemperature : null,
                    pressure = c.Pressure,
                    pressureTrend = trend,
                    wind = c.WindSpeed,
                    windDir = FishingAdvisor.WindDirectionName(c.WindDirection),
                    humidity = c.Humidity,
                    cloud = c.CloudCover,
                    precipitation = c.Precipitation,
                    sunrise = today != null ? today.Sunrise.ToString("HH:mm") : null,
                    sunset = today != null ? today.Sunset.ToString("HH:mm") : null,
                    moon = string.Format("{0} ({1}%)", MoonPhase.Name(moonAge), MoonPhase.Illumination(moonAge)),
                    score = nowHour != null ? (int?)nowHour.Score : null,
                    stars = nowHour != null ? FishingAdvisor.Stars(nowHour.Score) : null,
                    rating = nowHour != null ? FishingAdvisor.RatingText(nowHour.Score) : null,
                    color = nowHour != null ? Hex(FishingAdvisor.ScoreColor(nowHour.Score)) : null
                },
                hydro = h == null ? null : new
                {
                    source = HydroService.SourceName,
                    name = h.Name,
                    level = h.Level,
                    status = h.LevelStatusText,
                    time = h.LevelTime.ToString("d.MM HH:mm"),
                    isWarning = h.IsWarning,
                    isAlarm = h.IsAlarm,
                    waterTemperature = h.WaterTemperature
                },
                speciesInfo = new
                {
                    name = sp.Name,
                    isGeneral = sp.IsGeneral,
                    waterOpt = string.Format("{0:0}–{1:0} °C", sp.WaterOptMin, sp.WaterOptMax),
                    closed = sp.ClosedText != null
                        ? string.Format("Okres ochronny: {0} (sprawdź też regulamin swojego okręgu PZW).", sp.ClosedText)
                        : "Brak ustawowego okresu ochronnego (sprawdź regulamin swojego okręgu PZW).",
                    minLength = Reports.MinLengthText(sp),
                    bestDay = best.Score > 0
                        ? string.Format("Najlepszy dzień w tym tygodniu: {0}, {1} ({2}/100).", Reports.DayName(best.Date, now), best.BestWindowText, best.Score)
                        : null,
                    tips = sp.Tips
                },
                shareText = Reports.ForecastText(f, sp),
                days = f.Days.Select(d =>
                {
                    double dpDay = d.Hours.Last().Pressure - d.Hours.First().Pressure;
                    return new
                    {
                        date = Iso(d.Date),
                        name = Reports.DayName(d.Date, now),
                        shortName = Reports.ShortDayName(d.Date, now),
                        isBest = d == best,
                        score = d.Score,
                        stars = FishingAdvisor.Stars(d.Score),
                        rating = FishingAdvisor.RatingText(d.Score),
                        color = Hex(FishingAdvisor.ScoreColor(d.Score)),
                        bestWindow = d.BestWindowText,
                        bestScore = d.BestScore,
                        topSpecies = d.SpeciesRanking.Take(3).Select(x => x.Species.Name),
                        tempMin = d.TempMin,
                        tempMax = d.TempMax,
                        water = d.WaterTemperature,
                        windMax = d.WindMax,
                        precipitation = d.PrecipitationSum,
                        pressure = d.Hours.Average(x => x.Pressure),
                        pressureArrow = dpDay <= -2 ? "↓" : dpDay >= 2 ? "↑" : "→",
                        sunrise = d.Sunrise.ToString("HH:mm"),
                        sunset = d.Sunset.ToString("HH:mm"),
                        moon = MoonPhase.Name(d.MoonAge),
                        reasons = d.Reasons,
                        waterTip = FishingAdvisor.WaterTip(sp, d.WaterTemperature),
                        ranking = d.SpeciesRanking.Select(r => new
                        {
                            name = r.Species.Name,
                            score = r.Score,
                            color = Hex(FishingAdvisor.ScoreColor(r.Score)),
                            closed = r.Species.IsClosed(d.Date)
                        }),
                        hours = d.Hours.Select(x => new
                        {
                            hour = x.Time.Hour,
                            score = x.Score,
                            color = Hex(FishingAdvisor.ScoreColor(x.Score)),
                            temperature = x.Temperature,
                            water = x.WaterTemperature,
                            pressure = x.Pressure,
                            wind = x.WindSpeed,
                            gusts = x.WindGusts,
                            windDir = FishingAdvisor.WindDirectionName(x.WindDirection),
                            precipitation = x.Precipitation,
                            cloud = x.CloudCover,
                            weather = FishingAdvisor.WeatherDescription(x.WeatherCode),
                            night = x.Time.AddHours(1) <= d.Sunrise || x.Time >= d.Sunset,
                            best = d.BestLength > 0 && x.Time >= d.BestStart && x.Time < d.BestStart.AddHours(d.BestLength),
                            past = x.Time.AddHours(1) <= now
                        })
                    };
                })
            };
        }
    }

    /// <summary>
    /// Prognozy trzymane w pamięci przez 15 minut, żeby zmiana gatunku, odświeżenie strony czy porównanie łowisk
    /// nie wysyłały za każdym razem zapytań do Open-Meteo. Obiekt Forecast jest przeliczany (FishingAdvisor.Analyze)
    /// pod blokadą na wpisie, bo zmienia go w miejscu.
    /// </summary>
    public class ForecastCache
    {
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
        private readonly WeatherService _weather;
        private readonly ConcurrentDictionary<string, Lazy<Task<Entry>>> _entries = new ConcurrentDictionary<string, Lazy<Task<Entry>>>();

        public ForecastCache(WeatherService weather)
        {
            _weather = weather;
        }

        public class Entry
        {
            public Forecast Forecast { get; set; }
            public DateTime Created { get; set; }
        }

        public async Task<Entry> GetAsync(City city)
        {
            string key = string.Format(CultureInfo.InvariantCulture, "{0:0.####}|{1:0.####}", city.Latitude, city.Longitude);
            while (true)
            {
                var lazy = _entries.GetOrAdd(key, _ => new Lazy<Task<Entry>>(() => FetchAsync(city), LazyThreadSafetyMode.ExecutionAndPublication));
                Entry entry;
                try
                {
                    entry = await lazy.Value;
                }
                catch
                {
                    // Błąd nie zostaje w pamięci – następne zapytanie spróbuje ponownie.
                    _entries.TryRemove(new KeyValuePair<string, Lazy<Task<Entry>>>(key, lazy));
                    throw;
                }
                if (DateTime.Now - entry.Created < Lifetime) return entry;
                _entries.TryRemove(new KeyValuePair<string, Lazy<Task<Entry>>>(key, lazy));
            }
        }

        private async Task<Entry> FetchAsync(City city)
        {
            var forecast = await _weather.GetForecastAsync(city);
            return new Entry { Forecast = forecast, Created = DateTime.Now };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace NaRyby_win
{
    /// <summary>
    /// Lista ulubionych miejscowości zapisywana w %AppData%\NaRyby\miasta.txt
    /// (na macOS: ~/Library/Application Support/NaRyby/miasta.txt).
    /// </summary>
    public class CityStore
    {
        private const string LastPrefix = "#last=";
        private const string SpeciesPrefix = "#species=";
        private const string ThemePrefix = "#theme=";

        private readonly string _path = Path.Combine(DataFolder(), "NaRyby", "miasta.txt");

        private static string DataFolder()
        {
            // Na macOS ApplicationData wskazuje ~/.config, a aplikacje trzymają dane w Application Support.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        public CityStore()
        {
            Cities = new List<City>();
        }

        public List<City> Cities { get; private set; }
        public string LastCity { get; set; }
        public string LastSpecies { get; set; }

        /// <summary>Wybrany motyw: "light", "dark" albo null – zgodnie z systemem.</summary>
        public string ThemeMode { get; set; }

        public void Load()
        {
            Cities.Clear();
            try
            {
                if (File.Exists(_path))
                {
                    foreach (string line in File.ReadAllLines(_path, Encoding.UTF8))
                    {
                        if (line.StartsWith(LastPrefix))
                        {
                            LastCity = line.Substring(LastPrefix.Length);
                            continue;
                        }
                        if (line.StartsWith(SpeciesPrefix))
                        {
                            LastSpecies = line.Substring(SpeciesPrefix.Length);
                            continue;
                        }
                        if (line.StartsWith(ThemePrefix))
                        {
                            ThemeMode = line.Substring(ThemePrefix.Length);
                            continue;
                        }

                        string[] parts = line.Split('|');
                        double lat, lon;
                        if (parts.Length == 3
                            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
                            && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out lon))
                        {
                            Cities.Add(new City(parts[0], lat, lon));
                        }
                    }
                }
            }
            catch (IOException)
            {
                Cities.Clear();
            }

            if (Cities.Count == 0) AddDefaults();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                var lines = Cities.Select(c => string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}", c.Name, c.Latitude, c.Longitude)).ToList();
                if (!string.IsNullOrEmpty(ThemeMode)) lines.Insert(0, ThemePrefix + ThemeMode);
                if (!string.IsNullOrEmpty(LastSpecies)) lines.Insert(0, SpeciesPrefix + LastSpecies);
                if (!string.IsNullOrEmpty(LastCity)) lines.Insert(0, LastPrefix + LastCity);
                File.WriteAllLines(_path, lines, Encoding.UTF8);
            }
            catch (IOException)
            {
                // Brak zapisu listy nie powinien blokować działania programu.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>Dodaje miejscowość; zwraca istniejącą, jeśli taka już jest na liście.</summary>
        public City Add(City city)
        {
            var existing = Cities.FirstOrDefault(c =>
                Math.Abs(c.Latitude - city.Latitude) < 0.01 && Math.Abs(c.Longitude - city.Longitude) < 0.01);
            if (existing != null) return existing;

            string name = city.Name;
            if (Cities.Any(c => string.Equals(c.Name, name, StringComparison.CurrentCultureIgnoreCase)))
                name = city.Description;

            var added = new City(name, city.Latitude, city.Longitude);
            Cities.Add(added);
            Save();
            return added;
        }

        public void Remove(City city)
        {
            Cities.Remove(city);
            Save();
        }

        private void AddDefaults()
        {
            // Pierwsza pozycja jest domyślną lokalizacją przy pierwszym uruchomieniu.
            Cities.Add(new City("Arciechów (k. Radzymina)", 52.4880, 21.0835));
            Cities.Add(new City("Warszawa", 52.2297, 21.0122));
            Cities.Add(new City("Kraków", 50.0647, 19.9450));
            Cities.Add(new City("Gdańsk", 54.3520, 18.6466));
            Cities.Add(new City("Wrocław", 51.1079, 17.0385));
            Cities.Add(new City("Poznań", 52.4064, 16.9252));
            Cities.Add(new City("Łódź", 51.7592, 19.4560));
            Cities.Add(new City("Szczecin", 53.4285, 14.5528));
            Cities.Add(new City("Bydgoszcz", 53.1235, 18.0084));
            Cities.Add(new City("Lublin", 51.2465, 22.5684));
            Cities.Add(new City("Białystok", 53.1325, 23.1688));
            Cities.Add(new City("Olsztyn", 53.7784, 20.4801));
            Cities.Add(new City("Giżycko", 54.0381, 21.7661));
            Cities.Add(new City("Rzeszów", 50.0412, 21.9991));
            Cities.Add(new City("Zakopane", 49.2992, 19.9496));
        }
    }
}

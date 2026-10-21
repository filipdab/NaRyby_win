using System.Collections.Generic;

namespace NaRyby_win
{
    /// <summary>
    /// Preferencje gatunku używane przy ocenie warunków oraz porady wędkarskie.
    /// Wagi wynikają z popularnej wiedzy wędkarskiej – to wskazówki, nie prawa natury.
    /// </summary>
    public class Species
    {
        public string Name { get; set; }
        /// <summary>Forma do zdań typu „jechać na …” (biernik).</summary>
        public string Accusative { get; set; }
        /// <summary>Forma do zdań typu „optymalna dla …” (dopełniacz).</summary>
        public string Genitive { get; set; }
        public bool IsGeneral { get; set; }

        // Temperatura wody (°C): poza [Min, Max] ryba jest mało aktywna, w [OptMin, OptMax] żeruje najlepiej.
        public double WaterMin { get; set; }
        public double WaterOptMin { get; set; }
        public double WaterOptMax { get; set; }
        public double WaterMax { get; set; }

        // Premie za porę dnia.
        public double TwilightBonus { get; set; }
        public double NightBonus { get; set; }
        public double MiddayBonus { get; set; }

        // Mnożniki (1 = typowo, 0 = bez znaczenia, ujemne = odwrotna preferencja).
        public double PressureSensitivity { get; set; }
        public double WindPreference { get; set; }
        public double CloudPreference { get; set; }
        public double RainPreference { get; set; }

        /// <summary>Premia sezonowa dla miesięcy styczeń..grudzień.</summary>
        public int[] SeasonBonus { get; set; }

        // Okres ochronny w formacie MMDD (np. 101 = 1 stycznia, 430 = 30 kwietnia); 0 = brak.
        public int ClosedFrom { get; set; }
        public int ClosedTo { get; set; }

        public string ColdTip { get; set; }
        public string WarmTip { get; set; }
        public string[] Tips { get; set; }

        public bool IsClosed(System.DateTime date)
        {
            if (ClosedFrom == 0) return false;
            int md = date.Month * 100 + date.Day;
            return md >= ClosedFrom && md <= ClosedTo;
        }

        public string ClosedText
        {
            get
            {
                if (ClosedFrom == 0) return null;
                return string.Format("{0}.{1:00}–{2}.{3:00}", ClosedFrom % 100, ClosedFrom / 100, ClosedTo % 100, ClosedTo / 100);
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public static readonly List<Species> All = new List<Species>
        {
            new Species
            {
                Name = "Wszystkie gatunki", Accusative = "ryby", Genitive = "ryb", IsGeneral = true,
                WaterMin = 4, WaterOptMin = 10, WaterOptMax = 22, WaterMax = 28,
                TwilightBonus = 15, NightBonus = -12, MiddayBonus = 0,
                PressureSensitivity = 1, WindPreference = 1, CloudPreference = 1, RainPreference = 1,
                SeasonBonus = new[] { -5, -5, -2, 2, 5, 5, 3, 3, 5, 5, 2, -3 },
                ColdTip = "Zimna woda: ryby są ospałe – łów wolno, przy dnie, na mniejsze przynęty.",
                WarmTip = "Ciepła woda: najlepsze brania o świcie i wieczorem, w upał szukaj cienia i głębszej wody.",
                Tips = new[]
                {
                    "Wybierz konkretny gatunek z listy, aby zobaczyć dopasowaną ocenę i porady.",
                    "Kolumna „Na co łowić” w tabeli pokazuje gatunki z najlepszą prognozą na dany dzień."
                }
            },
            new Species
            {
                Name = "Szczupak", Accusative = "szczupaka", Genitive = "szczupaka",
                WaterMin = 2, WaterOptMin = 8, WaterOptMax = 18, WaterMax = 25,
                TwilightBonus = 12, NightBonus = -25, MiddayBonus = 3,
                PressureSensitivity = 1.3, WindPreference = 1, CloudPreference = 1.2, RainPreference = 1,
                SeasonBonus = new[] { 0, 0, 0, 0, 2, 0, -5, -3, 8, 10, 10, 3 },
                ClosedFrom = 101, ClosedTo = 430,
                ColdTip = "Zimna woda: prowadź przynętę bardzo wolno, blisko dna; świetnie sprawdzają się duże gumy i martwa rybka.",
                WarmTip = "Ciepła woda: szczupaki chowają się w chłodniejszej, głębszej wodzie – łów rano i wieczorem przy roślinności.",
                Tips = new[]
                {
                    "Przynęty: woblery, gumy (rippery, shady), wahadłówki, błystki obrotowe, martwa rybka.",
                    "Miejsca: skraje trzcin i grążeli, zatoki, spady przy roślinności, okolice zatopionych drzew.",
                    "Pochmurny, wietrzny dzień z lekko spadającym ciśnieniem to klasyczna „szczupakowa” pogoda.",
                    "Używaj przyponu stalowego lub z fluorocarbonu – zęby szczupaka tną zwykłą żyłkę."
                }
            },
            new Species
            {
                Name = "Sandacz", Accusative = "sandacza", Genitive = "sandacza",
                WaterMin = 4, WaterOptMin = 10, WaterOptMax = 20, WaterMax = 26,
                TwilightBonus = 18, NightBonus = 3, MiddayBonus = -6,
                PressureSensitivity = 1.2, WindPreference = 1, CloudPreference = 1.5, RainPreference = 1,
                SeasonBonus = new[] { 0, 0, 0, 0, 0, 3, 0, 3, 8, 10, 8, 3 },
                ClosedFrom = 101, ClosedTo = 531,
                ColdTip = "Zimna woda: łów metodą jigową bardzo wolno, tuż przy dnie, na głębszych spadach i w dołkach.",
                WarmTip = "Ciepła woda: sandacze żerują głównie nocą i o świcie, wychodząc na płycizny przy spadach.",
                Tips = new[]
                {
                    "Przynęty: gumy (twistery, rippery) na główce jigowej, wąskie woblery, martwa rybka.",
                    "Miejsca: twarde dno, spady, zatopione przeszkody, na rzekach główki i ostrogi.",
                    "Sandacz nie lubi światła – najlepsze są świty, zmierzchy, noce i pochmurne dni.",
                    "Brania bywają delikatne – używaj plecionki i czułej wędki, zacinaj zdecydowanie."
                }
            },
            new Species
            {
                Name = "Okoń", Accusative = "okonia", Genitive = "okonia",
                WaterMin = 4, WaterOptMin = 12, WaterOptMax = 22, WaterMax = 27,
                TwilightBonus = 6, NightBonus = -25, MiddayBonus = 6,
                PressureSensitivity = 1, WindPreference = 0.5, CloudPreference = 0, RainPreference = 0.5,
                SeasonBonus = new[] { 0, 0, 0, 2, 3, 5, 5, 6, 8, 8, 3, 0 },
                ColdTip = "Zimna woda: okonie zbijają się w ławice na głębszej wodzie – szukaj ich przy dnie, świetnie działa drop shot.",
                WarmTip = "Ciepła woda: okonie polują na narybek na płyciznach – obserwuj uciekającą drobnicę.",
                Tips = new[]
                {
                    "Przynęty: małe gumki, obrotówki (nr 0–2), mikrowoblery, czerwone robaki; zimą mormyszka.",
                    "Miejsca: górki podwodne, pomosty, kamienie, spady i ujścia rowów.",
                    "Okoń żeruje w dzień – nie trzeba wstawać przed świtem, a słońce mu nie przeszkadza.",
                    "Złowiłeś jednego? Zostań w miejscu – okonie pływają stadami."
                }
            },
            new Species
            {
                Name = "Karp", Accusative = "karpia", Genitive = "karpia",
                WaterMin = 12, WaterOptMin = 18, WaterOptMax = 26, WaterMax = 30,
                TwilightBonus = 10, NightBonus = 5, MiddayBonus = -3,
                PressureSensitivity = 1.4, WindPreference = 0.5, CloudPreference = 0.5, RainPreference = 0.5,
                SeasonBonus = new[] { -15, -15, -10, -3, 6, 10, 8, 8, 8, 2, -8, -15 },
                ColdTip = "Zimna woda: karp jest ospały – małe, bardzo atrakcyjne przynęty, niewiele zanęty, głębsza woda.",
                WarmTip = "Upał: łów nocą i wcześnie rano; w dzień karpie stoją przy powierzchni – spróbuj zig-riga.",
                Tips = new[]
                {
                    "Przynęty: kulki proteinowe, kukurydza, pellet, ziemniak, ciasto; zanęcaj z wyprzedzeniem.",
                    "Miejsca: brzeg, na który wieje ciepły wiatr, okolice trzcin, spady i blaty z twardym dnem.",
                    "Karp nie lubi gwałtownych zmian pogody – najlepiej sprawdza się kilka dni stabilnej, ciepłej aury.",
                    "Zabierz matę karpiową i podbierak – karpia wypuszczaj ostrożnie."
                }
            },
            new Species
            {
                Name = "Leszcz", Accusative = "leszcza", Genitive = "leszcza",
                WaterMin = 8, WaterOptMin = 14, WaterOptMax = 24, WaterMax = 28,
                TwilightBonus = 12, NightBonus = 6, MiddayBonus = -4,
                PressureSensitivity = 1, WindPreference = 0.5, CloudPreference = 0.5, RainPreference = 0.5,
                SeasonBonus = new[] { -8, -8, -5, 0, 5, 8, 6, 6, 6, 2, -3, -8 },
                ColdTip = "Zimna woda: leszcze stoją w najgłębszych miejscach – zmniejsz porcje zanęty i przynęty.",
                WarmTip = "Ciepła woda: najlepsze brania wieczorem i nocą, na głębszej wodzie przy mulistym dnie.",
                Tips = new[]
                {
                    "Przynęty: białe i czerwone robaki, kukurydza, pęczak, ochotka.",
                    "Metody: feeder lub spławik na głębszej wodzie z mulistym dnem.",
                    "Leszcze żerują stadami – obfite zanęcanie zatrzymuje ławicę w łowisku."
                }
            },
            new Species
            {
                Name = "Lin", Accusative = "lina", Genitive = "lina",
                WaterMin = 12, WaterOptMin = 18, WaterOptMax = 26, WaterMax = 30,
                TwilightBonus = 15, NightBonus = -2, MiddayBonus = -6,
                PressureSensitivity = 1.2, WindPreference = -1, CloudPreference = 0.5, RainPreference = 1,
                SeasonBonus = new[] { -15, -15, -12, -5, 6, 10, 8, 5, 2, -5, -12, -15 },
                ColdTip = "W zimnej wodzie lin praktycznie nie żeruje – lepiej wybierz inny gatunek.",
                WarmTip = "Ciepła woda: łów o świcie w zarośniętych zatokach – po wschodzie słońca brania szybko słabną.",
                Tips = new[]
                {
                    "Przynęty: czerwone robaki, kukurydza, ochotka, kawałki rosówki.",
                    "Miejsca: zarośnięte, muliste zatoki, okna w grążelach i rzęsie, skraje trzcin.",
                    "Lin lubi ciepłą, parną i bezwietrzną pogodę – szczególnie letni świt."
                }
            },
            new Species
            {
                Name = "Płoć", Accusative = "płoć", Genitive = "płoci",
                WaterMin = 4, WaterOptMin = 10, WaterOptMax = 22, WaterMax = 28,
                TwilightBonus = 8, NightBonus = -12, MiddayBonus = 3,
                PressureSensitivity = 0.8, WindPreference = 0.3, CloudPreference = 0.5, RainPreference = 0.5,
                SeasonBonus = new[] { 0, 0, 2, 5, 2, 2, 0, 2, 5, 6, 4, 2 },
                ColdTip = "Zimna woda: bardzo delikatny zestaw, małe haczyki; płocie stoją przy dnie.",
                WarmTip = "Ciepła woda: płocie trzymają się roślinności i płytszej wody, biorą też z powierzchni.",
                Tips = new[]
                {
                    "Przynęty: białe robaki, ochotka, pęczak, kasters, chleb.",
                    "Metody: delikatny spławik lub bat, drobna zanęta z dodatkiem kleiku.",
                    "Płoć bierze przez cały rok – dobry wybór, gdy inne gatunki są nieaktywne."
                }
            },
            new Species
            {
                Name = "Sum", Accusative = "suma", Genitive = "suma",
                WaterMin = 14, WaterOptMin = 20, WaterOptMax = 28, WaterMax = 32,
                TwilightBonus = 12, NightBonus = 15, MiddayBonus = -10,
                PressureSensitivity = 1.3, WindPreference = 0, CloudPreference = 1, RainPreference = 1.5,
                SeasonBonus = new[] { -20, -20, -15, -8, 2, 8, 10, 10, 5, -3, -12, -20 },
                ColdTip = "W zimnej wodzie sum zalega w dołach i praktycznie nie żeruje.",
                WarmTip = "Ciepłe, parne noce przy spadającym ciśnieniu to najlepszy czas – spróbuj kwoka.",
                Tips = new[]
                {
                    "Przynęty: pęczek rosówek, martwa lub żywa ryba, pellet halibutowy; na spinning duże gumy i woblery.",
                    "Miejsca: głębokie doły, przegłębienia, zwalone drzewa, na dużych rzekach rynny i łachy.",
                    "Sum lubi parne noce przed burzą – ale gdy burza nadchodzi, natychmiast zejdź z wody!",
                    "Używaj mocnego sprzętu: plecionka 0,40 mm+, mocne haki, solidny kołowrotek."
                }
            },
            new Species
            {
                Name = "Boleń", Accusative = "bolenia", Genitive = "bolenia",
                WaterMin = 8, WaterOptMin = 14, WaterOptMax = 24, WaterMax = 28,
                TwilightBonus = 8, NightBonus = -25, MiddayBonus = 4,
                PressureSensitivity = 1, WindPreference = 0.5, CloudPreference = -0.5, RainPreference = 0,
                SeasonBonus = new[] { -10, -10, -5, 0, 6, 8, 8, 8, 6, 2, -5, -10 },
                ClosedFrom = 101, ClosedTo = 430,
                ColdTip = "Zimna woda: bolenie schodzą głębiej i rzadko polują przy powierzchni – prowadź przynętę wolniej i niżej.",
                WarmTip = "Ciepła woda: bolenie atakują drobnicę przy powierzchni – szukaj „chlapania” i rzucaj daleko.",
                Tips = new[]
                {
                    "Przynęty: woblery powierzchniowe, cykady, wahadłówki i ciężkie obrotówki do dalekich rzutów.",
                    "Miejsca: rzeki – główki, ujścia dopływów, szybki nurt przy spływach i za przeszkodami.",
                    "Boleń poluje w dzień, także w słońcu – prowadź przynętę szybko."
                }
            }
        };
    }
}

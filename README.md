<img src="docs/ikona.png" width="96" align="right" alt="Ikona NaRyby">

# NaRyby – kiedy jechać na ryby?

Okienkowa aplikacja dla wędkarzy na Windows. Pobiera prognozę pogody dla wybranej
miejscowości i podpowiada, **kiedy najlepiej jechać na ryby** – ogólnie albo na konkretny
gatunek (szczupak, sandacz, okoń, karp, leszcz, lin, płoć, sum, boleń).

![Główne okno](docs/glowne-okno.png)

## Funkcje

- **Prognoza na 7 dni** z oceną brań 0–100, gwiazdkami i najlepszym 3-godzinnym oknem w każdym dniu.
- **Rekomendacja** – baner z najlepszym dniem i godziną wyjazdu oraz głównym powodem.
- **Gatunki ryb** – ocena i porady dopasowane do wybranego gatunku; kolumna „Na co łowić”
  pokazuje gatunki z najlepszą prognozą na dany dzień.
- **Wykres godzinowy** – ocena brań, ciśnienie, noc i najlepsze okno; po najechaniu na słupek
  widać pogodę w danej godzinie.
- **Porady** – przynęty, miejsca, zachowanie ryb, porada zależna od temperatury wody
  i informacja o okresie ochronnym.
- **Dowolne miejscowości** – wyszukiwarka dodaje dowolne miejsce (jezioro, wieś, miasto);
  lista jest zapamiętywana. Domyślnie: Arciechów koło Radzymina.
- Automatyczne odświeżanie co 30 minut.

| Ocena dnia dla gatunku | Porady dla gatunku |
|---|---|
| ![Ocena dnia – szczupak](docs/szczupak-ocena.png) | ![Porady – szczupak](docs/szczupak-porady.png) |

## Jak liczona jest ocena

Każda godzina dostaje punkty według popularnej wiedzy wędkarskiej:

| Czynnik | Wpływ |
|---|---|
| Ciśnienie | stabilne lub powoli spadające – plus; gwałtowne zmiany – minus |
| Wiatr | umiarkowany – plus (drapieżniki), cisza – plus dla lina; silny i porywisty – minus |
| Kierunek wiatru | zachodni / południowo-zachodni – plus, wschodni – minus |
| Temperatura wody | zgodna z preferencjami gatunku – plus; za zimna / za ciepła – minus |
| Opady | mżawka – plus, ulewa – minus, burza – duży minus |
| Pora dnia | świt i zmierzch – plus; noc lub południe zależnie od gatunku |
| Zachmurzenie | pochmurno – plus dla drapieżników |
| Sezon | szczyt sezonu danego gatunku – plus; okres ochronny – ocena 0 |
| Księżyc | nów i pełnia – plus |

Temperatura wody nie jest mierzona – jest szacowana ze średniej temperatury powietrza
z ostatnich 3 dni. Wagi poszczególnych gatunków są w [`Species.cs`](NaRyby_win/Species.cs),
a algorytm w [`FishingAdvisor.cs`](NaRyby_win/FishingAdvisor.cs).

> Ocena to szacunek, a nie gwarancja brań. Okresy ochronne mają charakter informacyjny –
> zawsze sprawdź aktualne przepisy i regulamin swojego okręgu PZW.

## Wymagania

- Windows 10 (1803+) lub Windows 11 – wbudowany .NET Framework 4.7.2 wystarcza.
- Połączenie z internetem.

## Budowanie

Otwórz `NaRyby_win.slnx` w Visual Studio i uruchom (F5) albo z wiersza poleceń:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
    NaRyby_win\NaRyby_win.csproj -p:Configuration=Release
```

Wynik: `NaRyby_win\bin\Release\NaRyby_win.exe` – pojedynczy plik (plus `.exe.config`),
bez dodatkowych bibliotek, gotowy do skopiowania na inny komputer.

Ikona (`NaRyby_win/NaRyby.ico`) jest generowana skryptem [`tools/make_icon.ps1`](tools/make_icon.ps1).

## Struktura

| Plik | Zawartość |
|---|---|
| `MainForm.cs` | główne okno |
| `ScoreChart.cs` | wykres godzinowy |
| `CityPickerForm.cs` | wybór miejscowości przy wielu wynikach wyszukiwania |
| `WeatherService.cs` | pobieranie prognozy i wyszukiwanie miejscowości (Open-Meteo) |
| `FishingAdvisor.cs` | ocena warunków i uzasadnienia |
| `Species.cs` | preferencje i porady dla gatunków |
| `MoonPhase.cs` | fazy Księżyca |
| `CityStore.cs` | lista miejscowości w `%AppData%\NaRyby\miasta.txt` |

## Dane

Prognoza pogody i wyszukiwanie miejscowości: [Open-Meteo.com](https://open-meteo.com/)
(licencja [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)). Nie wymaga klucza API.

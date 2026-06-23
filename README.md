# 🍅 Pomodoro

Minimalistyczny, zawsze-na-wierzchu widget Pomodoro dla Windows. WPF, .NET 10, zero zależności NuGet. Opcjonalna integracja z Todoist.

## Funkcje

- **Borderless + always-on-top** — brak ramki Windows (min/max/close). Okno przeciągasz klikając w tło.
- **Autorun** — startuje z Windowsem (wpis w `HKCU\...\Run`, przełącznik w ustawieniach).
- **Tryby** — Pomodoro / krótka przerwa / długa przerwa; auto-długa-przerwa co N pomodoro.
- **Auto-start** przerw i pomodoro (opcjonalnie), dźwięk na koniec.
- **Todoist** — lista zadań (API v1), klik = odznaczenie (zamknięcie taska). Opcjonalny filtr (np. `today`, `#Praca`).
- Konfiguracja i pozycja okna zapisywane w `%APPDATA%\Pomodoro\settings.json`.

## Wymagania

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (do budowania)

## Instalacja i uruchomienie

```powershell
git clone <repo-url>
cd Pomodoro

# Szybkie uruchomienie
dotnet run -c Release

# Albo zbuduj exe
dotnet build -c Release
# -> bin\Release\net10.0-windows\Pomodoro.exe
```

Zalecane do autostartu — pojedynczy, samodzielny plik:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -o publish
# -> publish\Pomodoro.exe
```

## Jak używać

### Timer

- **START / PAUZA** — uruchamia i zatrzymuje odliczanie.
- **Reset** — przywraca pełny czas bieżącego trybu.
- **Zakładki trybów** (Pomodoro / krótka / długa przerwa) — ręczne przełączanie. Aktywny tryb jest podświetlony, a tło okna zmienia kolor.
- Po zejściu do zera odtwarza się dźwięk (jeśli włączony) i timer przechodzi do następnego trybu. Co `LongBreakInterval` pomodoro zamiast krótkiej przerwy włącza się długa.
- **Przeciąganie okna** — kliknij i przytrzymaj na tle, przesuń. Pozycja zapisuje się przy zamknięciu.

### Ustawienia (⚙)

W oknie ustawień skonfigurujesz:

- Długości: Pomodoro, krótka przerwa, długa przerwa.
- `LongBreakInterval` — co ile pomodoro wpada długa przerwa.
- Auto-start przerw / auto-start pomodoro.
- Dźwięk na koniec (wł./wył.).
- Start z Windowsem.
- Token i filtr Todoist.

Zmiany zapisują się po zatwierdzeniu i od razu wchodzą w życie.

### Todoist (opcjonalnie)

1. Pobierz token API: Todoist → **Ustawienia → Integracje → Deweloper → API token**.
2. Wklej token w ustawieniach widgetu (⚙).
3. Lista zadań pojawi się w oknie. Wybierz projekt z listy rozwijanej albo zostaw „Wszystkie".
4. Opcjonalny **filtr** zawęża zadania składnią Todoist, np. `today`, `overdue`, `#Praca`. Wybór projektu ma pierwszeństwo nad filtrem.
5. **Klik w zadanie = odznaczenie** (zamknięcie taska w Todoist).
6. Przycisk sync odświeża listę.

> Token trzymany jest **tylko lokalnie** w `%APPDATA%\Pomodoro\settings.json`. Nie jest commitowany.

## Testy

```powershell
dotnet test Tests\Pomodoro.Tests.csproj
```

## Architektura

| Plik / katalog | Rola |
|----------------|------|
| `Models/` | `AppSettings`, `TimerMode`, `TodoistTask`, `TodoistProject` i strony paginacji |
| `Services/PomodoroEngine` | czysta maszyna stanu timera (bez UI) |
| `Services/PomodoroSession` | moduł sesji: tick → koniec → auto-start, zdarzenia `Changed`/`Finished` |
| `Services/HttpTodoistGateway` | Todoist API v1 (HttpClient, paginacja kursorowa) |
| `Services/TaskListModel` | logika listy zadań (projekty, filtr, zaznaczenie, zamykanie) |
| `Services/SettingsService` + `SettingsStore` | jedyne miejsce zapisu konfiguracji (JSON w `%APPDATA%`) |
| `Services/AutoStartManager` | wpis w rejestrze `Run` |
| `Presentation/ModeTheme` | kolory trybów |
| `MainWindow` / `SettingsWindow` | widget / okno ustawień |

## Uwagi

- Token Todoist trzymany **tylko lokalnie** — nie commituj go.
- Todoist REST API v2 zwraca `410 Gone` — używamy unified API v1 (`api.todoist.com/api/v1`).

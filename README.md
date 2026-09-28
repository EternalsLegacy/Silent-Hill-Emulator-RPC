# Silent Hill Universal Discord Rich Presence Service

Ein leichtgewichtiger, robuster C# (.NET) Windows System-Tray Hintergrunddienst (Infobereich / Taskleiste Kontextmenü) zur automatischen Erkennung und Anzeige von Silent Hill Spielen in Discord – sowohl für emulierte Titel (DuckStation, PCSX2 etc.) als auch für PC-Ports (Reloaded-II, Enhanced Edition, GOG).

---

## 1. Problemstellung & Lösung

### Fall A: Silent Hill 1 (DuckStation)
* **Problem:** Discords Standarderkennung sieht lediglich die Emulator-Executable (`duckstation-qt-x64-ReleaseLTCG.exe`). Discord zeigt entweder nur den Emulatornamen, keine Aktivität oder keine spielspezifischen Daten.
* **Lösung:** Zweistufige Erkennung: Der Daemon prüft nicht nur auf den Prozessnamen, sondern wertet den Fenstertitel (`MainWindowTitle` & Win32 EnumWindows) auf das Muster `Silent Hill` aus. Läuft DuckStation im Menü oder mit einem anderen Spiel, wird Silent Hill nicht getriggert.

### Fall B: Silent Hill 3 (Reloaded-II / PC)
* **Problem:** Silent Hill 3 läuft unter dem Prozessnamen `sh3.exe`. Discords interne Datenbank ordnet dies fälschlicherweise der U-Boot-Simulation *Silent Hunter 3* zu.
* **Lösung:** Der Daemon umgeht Discords native Erkennung und sendet über Discords lokale Named-Pipe-IPC direkt maßgeschneiderte Rich-Presence-Profile mit verifizierten Assets und Beschreibungen.

---

## 2. Minimalistischer Discord Status (Destiny 2 Layout)

Das Tool zeigt ganz ohne überflüssige Statuszeilen oder Textbloat genau das an, was zählt:
* **Spielt [Spieltitel]** (über den Namen der Discord Application im Developer Portal, z. B. *Silent Hill* oder *Silent Hill 3*)
* **Verstrichene Spielzeit** (mit Gamepad-Icon)
* **Großes Cover-Icon** (z. B. `sh1_cover`, `sh3_cover`)
* **Kleines Plattform-/Mod-Loader-Icon** (z. B. `ps1_icon`, `reloaded_icon`)
* Keine nervigen Zusatzzeilen ("In the Fog..." o. Ä. werden standardmäßig weggelassen).

---

## 3. Windows System-Tray Menü (Taskleiste)

Die Anwendung läuft im Hintergrund im Windows-Infobereich (System-Tray) neben der Uhr.

### Funktionen im Kontextmenü (Rechtsklick auf das Tray-Icon):
* 🎮 **Silent Hill Discord RPC** (App Header)
* ⚪/🟢 **Live-Status:** Zeigt an, ob das Tool auf ein Spiel wartet oder welches Spiel gerade aktiv erkannt wird.
* ☑️ **Spiele-Erkennung (Aktivieren / Deaktivieren):**
  * Checkboxen für jedes konfigurierte Spiel (z. B. *Silent Hill 1 (DuckStation)*, *Silent Hill 3 (PC / Reloaded-II)*).
  * Ein Klick schaltet die Erkennung für das jeweilige Spiel sofort ein oder aus.
  * Wird ein Spiel deaktiviert, während es läuft, wird die Discord Rich Presence sofort beendet.
  * Die Auswahl wird automatisch in `appsettings.json` gespeichert und bleibt nach Neustarts erhalten.
* ⚙️ **Konfigurationsdatei öffnen:** Öffnet die `appsettings.json` direkt im Texteditor.
* 📁 **Assets-Ordner öffnen:** Öffnet den Bildordner (`img/`) im Windows Explorer.
* ❌ **Beenden:** Beendet den Hintergrunddienst sauber und bereinigt die Discord-Präsenz.

---

## 4. Multi-App-Architektur & Assets im Discord Developer Portal

Discord zeigt als Hauptüberschrift immer: **„Spielt [Name der Discord-Applikation]“**.

### Schritt-für-Schritt Einrichtung:
1. Öffne das [Discord Developer Portal](https://discord.com/developers/applications).
2. Erstelle für jedes Spiel eine neue Applikation (**New Application**):
   * App 1: Name = `Silent Hill`
   * App 2: Name = `Silent Hill 3`
3. **Application ID kopieren & eintragen:**
   * Kopiere die **Application ID** aus dem Reiter *General Information*.
   * Die IDs für SH1 und SH3 sind in der `appsettings.json` hinterlegt:
     * SH1: `1554186053999534091`
     * SH3: `1554187016151769218`
4. **Art Assets hochladen:**
   * Gehe in der jeweiligen App zu **Rich Presence** $\rightarrow$ **Art Assets**.
   * Lade die Bilder aus dem Ordner `img/` hoch:
     * Für SH1: `sh1_cover` (Large Image) und `ps1_icon` (Small Image).
     * Für SH3: `sh3_cover` (Large Image) und `reloaded_icon` (Small Image).
   * Die Asset-Namen müssen exakt mit `LargeImageKey` und `SmallImageKey` übereinstimmen.
5. **Discord Standarderkennung deaktivieren:**
   * In Discord unter *Benutzereinstellungen* $\rightarrow$ *Aktivitätseinstellungen / Registrierte Spiele* die Erkennung für `sh3.exe` (Silent Hunter 3) entfernen bzw. das Overlay deaktivieren.

---

## 5. Projektstruktur & PascalCase-Architektur

```
SilentHillEmulatorRPC/
├── app.ico                         # Multi-Resolution Windows Tray Icon
├── img/                            # Bild-Assets (PNG/JPG für Discord Developer Portal)
│   ├── ps1_icon.png
│   ├── reloaded_icon.png
│   ├── sh1_cover.png
│   └── sh3_cover.jpg
├── src/SilentHillEmulatorRPC/
│   ├── Configuration/
│   │   ├── AppConfig.cs            # Root-Konfiguration mit ShowDetailsAndState
│   │   ├── GameProfile.cs          # Profil-Modell pro Spiel
│   │   ├── IProfileManager.cs      # Schnittstelle für Profilverwaltung & Persistenz
│   │   └── ProfileManager.cs       # Schreibt Toggle-Änderungen in appsettings.json
│   ├── Detection/
│   │   ├── IProcessProvider.cs     # Schnittstelle für System-Prozessabfragen
│   │   ├── SystemProcessProvider.cs# Win32 EnumWindows + Process Poller
│   │   ├── IGameDetector.cs        # Schnittstelle für Match-Evaluation
│   │   ├── GameDetector.cs         # Regex/Substring Fenstertitel- & Executable-Matcher
│   │   ├── ProcessSnapshot.cs      # Immutable Prozess-Snapshot
│   │   └── GameMatchResult.cs      # Match-Ergebnis
│   ├── Discord/
│   │   ├── IDiscordCoordinator.cs  # Schnittstelle für Discord IPC
│   │   └── DiscordCoordinator.cs   # Verwaltet DiscordRpcClient & Multi-App Switching
│   ├── State/
│   │   ├── ServiceState.cs         # Idle, ActiveGame, Terminating
│   │   ├── RpcStateMachine.cs      # Statusübergänge & Session-Timer
│   │   ├── IRpcStateTracker.cs     # Schnittstelle für UI-Status
│   │   └── RpcStateTracker.cs      # Synchronisiert Status zwischen Worker & Tray UI
│   ├── UI/
│   │   └── TrayApplicationContext.cs # Windows System Tray NotifyIcon & Kontextmenü
│   ├── Worker/
│   │   └── RpcWorkerService.cs     # BackgroundService Polling-Loop & Minimal Presence
│   ├── appsettings.json            # Vorkonfigurierte Spielprofile & IDs
│   └── Program.cs                  # [STAThread], Windows Forms Context & HostBuilder
├── tests/SilentHillEmulatorRPC.Tests/
│   ├── GameDetectorTests.cs        # 16 Unit-Tests für Matcher & Regex
│   ├── ProfileManagerTests.cs      # Unit-Tests für Toggle- & Persistenz-Logik
│   ├── RpcStateMachineTests.cs     # Unit-Tests für State Transitions & Timer
│   └── MockProcessProvider.cs      # Mock-Prozess-Provider für isolierte Tests
└── publish/
    ├── SilentHillEmulatorRPC.exe   # Eigenständige Single-File Windows Tray App
    ├── appsettings.json
    ├── app.ico
    └── img/
```

---

## 6. Starten & Verwenden

### Sofort ausführen (fertige Single-File Exe):
Die fertige, eigenständige Anwendung liegt in `publish/`:
```powershell
.\publish\SilentHillEmulatorRPC.exe
```
* Die Anwendung startet sofort lautlos in den Windows-Infobereich (System Tray) neben der Uhr.
* Es öffnet sich kein störendes Konsolenfenster (`WinExe`).

### Im Entwicklungsmodus starten:
```powershell
dotnet run --project src\SilentHillEmulatorRPC
```

### Tests ausführen:
```powershell
dotnet test
```

### Autostart mit Windows einrichten:
Erstelle eine Verknüpfung von `publish\SilentHillEmulatorRPC.exe` im Windows-Autostart-Ordner:
* Drücke `Win + R` $\rightarrow$ tippe `shell:startup` $\rightarrow$ Verknüpfung der `.exe` hineinkopieren.
* Beim Hochfahren des PCs startet das Tool automatisch im System-Tray.

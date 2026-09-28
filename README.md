# Silent Hill Discord Rich Presence

A lightweight Windows System Tray application that provides clean Discord Rich Presence for Silent Hill games (DuckStation, PC / Reloaded-II, etc.).

Displays strictly:
* **Playing [Game Title]**
* **Elapsed Time**
* **Large Cover Icon**
* **Small Platform / Mod Loader Icon**

---

## Quick Setup

1. **Make sure Discord is running.**
2. **Disable conflicting games in Discord:**
   * Go to **Discord Settings** → **Registered Games**.
   * Disable or remove detection for `sh3.exe` (so Discord does not falsely display *Silent Hunter 3*).
3. **Run the Application:**
   * Launch `SilentHillEmulatorRPC.exe`.
   * The app docks quietly in your **Windows System Tray** (notification overflow area next to the clock).
   * Right-click the tray icon to enable or disable detection for specific games.

---

## Custom Discord Applications *(Optional)*

If you prefer using your own Discord Developer Applications instead of the preconfigured defaults:
1. Create your application at [Discord Developer Portal](https://discord.com/developers/applications).
2. Insert your **Application ID** into `appsettings.json`.
3. Upload the cover and platform icons from the `img/` folder under **Rich Presence → Art Assets**.

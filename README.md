======================================================
  Silent Hill Discord Rich Presence (v1.0.0)
======================================================

A lightweight Windows System Tray application that provides clean Discord 
Rich Presence for Silent Hill games (DuckStation, Reloaded-II, etc.).

Displays strictly:
- Playing [Game Title]
- Elapsed Time
- Large Cover Icon
- Small Platform/Mod Loader Icon

------------------------------------------------------
Quick Setup:
------------------------------------------------------
1. Make sure Discord is running.
2. In Discord Settings -> "Registered Games", disable or remove any 
   conflicting detection for sh3.exe (so Discord doesn't display Silent Hunter 3).
3. (Optional) If you want to use your own Discord Developer Applications:
   - Create your apps at https://discord.com/developers/applications
   - Copy Application IDs into appsettings.json.
   - Upload the icons from the img/ folder under Rich Presence -> Art Assets.
4. Launch SilentHillEmulatorRPC.exe.
   - The app docks in your Windows System Tray.
   - Right-click the tray icon to enable/disable detection for specific games.
======================================================

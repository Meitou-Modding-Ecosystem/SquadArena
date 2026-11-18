# Play from Visual Studio

`Play` is the startup project. F5 (debug) or Ctrl+F5 builds the engine and every mod in this repo, copies the mods into the game's mods folder and
starts the game. `dotnet run --project Play` does the same from a terminal.

## Launch profiles

Pick one in the drop-down next to the Start button:

| Profile | Runs | Use it for |
| --- | --- | --- |
| Play (engine + mods) | `MeitouClient.exe` built from this repo's `engine` submodule, in `engine/src/Meitou.Game/bin/Release/<tfm>/` (its `log.txt` is there too) | testing and debugging your mod; breakpoints in mod code work |
| Meitou launcher (installed) | the launcher installed from the Meitou releases, `%LOCALAPPDATA%\MeitouLauncher\current\meitou-launcher.exe` | changing the mod list or load order (`data/mods.cfg`) and trusting a new mod's code |

Both build and copy the mods first. The launcher's Play button starts the release it installed, not this repo's engine: close it and use
"Play (engine + mods)" to test your build. `dotnet run` uses the first profile.

## What a play build does

- Finds the mods: every top-level folder holding `meitou.json` and `<Folder>/<Folder>.csproj`. A new mod needs no change to `Play`.
- Builds the engine and each mod in **Release**, whatever the solution configuration: Debug engine builds throw in `TaskStart.CheckStarts`
  once a mod starts a character's task. Breakpoints in mod code still work (Release symbols are deployed).
- Copies each mod's `<Mod>.dll`, `<Mod>.pdb` and `meitou.json` to `<Kenshi>\mods\<Mod>\`. Close the game first: a running game locks the dll.
- A mod with a data generator (`<Mod>.Data/<Mod>.Data.csproj`, run as `<generator> <Kenshi folder> <output .mod>`) gets its `<Mod>.mod`
  generated from the installed game; a failure is a build warning and the mod is deployed without it. Every other mod gets an empty
  `<Mod>.mod` when it has none; an existing one is never replaced.
- When the mods folder doesn't exist (CI, Linux), nothing is copied or built beyond the solution, and the build says so.

## Settings

Defaults: game `C:\Program Files (x86)\Steam\steamapps\common\Kenshi`, mods folder `<game>\mods`. Change them with the `KENSHI_PATH`
environment variable (the engine reads it too, so both agree), or per machine in a git-ignored `Meitou.user.props` in the repo's root folder:

```xml
<Project>
  <PropertyGroup>
    <KenshiDir>D:\Games\Kenshi</KenshiDir>           <!-- game folder; also sets the mods folder -->
    <KenshiModsDir>D:\Games\Kenshi\mods</KenshiModsDir> <!-- mods folder only -->
    <PlayConfiguration>Debug</PlayConfiguration>      <!-- engine and mods configuration (default Release) -->
    <PlayDeploy>false</PlayDeploy>                    <!-- build and run without copying -->
    <PlayArguments>--continue</PlayArguments>         <!-- engine command line -->
  </PropertyGroup>
</Project>
```

The engine finds the game on its own (Steam); `KenshiDir` set only in `Meitou.user.props` isn't passed to it.

## First run

- Open `MeitouMod.slnx` (renamed with your mod). Set `Play` as the startup project if Visual Studio didn't pick it (right-click > Set as
  Startup Project); its default profile is "Play (engine + mods)".
- Run "Meitou launcher (installed)" once: enable the mod and trust its code there.
- If the copy fails with "access denied", give your user Modify rights on `Kenshi\mods` (Properties > Security) rather than running Visual
  Studio as administrator.

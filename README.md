# Meitou mod template

Template for a [MeitouClient](https://github.com/Meitou-Modding-Ecosystem/MeitouClient) code mod repo in the Meitou-Modding-Ecosystem
org. GPL-3.0-or-later.

- Build and run in Visual Studio (launch profiles for this repo's engine or the installed launcher, deploy to the game): [docs/play.md](docs/play.md).
- Setting up the repos on your machine: [docs/local-development.md](docs/local-development.md).
- Instructing agents on a change that spans repos: [docs/agents-across-repos.md](docs/agents-across-repos.md).

## Use this template

1. Create the repo from this template on GitHub (Use this template > Create a new repository), then clone it with
   `git clone --recurse-submodules <your repo URL>`.
2. Rename `ExampleMod` to your mod's name: the folders `ExampleMod` and `ExampleMod.Tests`, the files inside them named `ExampleMod*`, the
   `AssemblyName`/`RootNamespace` in both `.csproj` files, the namespaces, and the paths in `MeitouMod.slnx` (rename that file too).
3. In `meitou.json` set `id` (`<author-or-mod>.<name>`), `assemblies` and `entry`; keep the version only there.
4. Open the `.slnx` in Visual Studio and press F5 ([docs/play.md](docs/play.md)).

## Layout

- `engine/`: MeitouClient as a git submodule, pinned to the engine commit the mod builds against. After a pull that moves it:
  `git submodule update --init`.
- `<Mod>/`: the mod's project and `meitou.json`. `<Mod>.Tests/`: its tests (synthetic worlds, no game install needed).
  `<Mod>.Data/` (optional): a generator that writes `<Mod>.mod` from the installed game.
- `Play/`: one-click build, deploy and run.
- `Directory.Build.props` imports the engine's, so mods build with the game's settings (warnings are errors).

## Branches

PRs into `beta`; CI (build and tests) must pass. `beta` is promoted to `main` once tested in play, and production goes through a PR to
`release`.

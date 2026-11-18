# Local development setup

Clone only the repos you work on into one folder. Each builds on its own with `dotnet build`, and the frameworks it needs download and
hash-check themselves.

## Repos

| Repo | Holds | PRs go to |
| --- | --- | --- |
| MeitouClient | the engine and its mod hooks | `fork` |
| MeitouMods | Rivalry Core, shared docs and skills | `beta` |
| LogisticsCore | Logistics Core framework | `beta` |
| WarOrchestration | War Orchestration framework (uses Logistics Core) | `beta` |
| BastFront | Bast Front melee (uses War Orchestration and Logistics Core) | `beta` |
| AshenHand | The Ashen Hand (uses Rivalry Core) | `beta` |
| Husbandry | Husbandry | `beta` |
| SquadRating | SquadArena mod and the rating tool | `beta` |
| GutenStructure (TheIvoryDisaster) | shared CI, packaging, release gate, framework lock target | `beta` |

## Folders

These steps use eight folders. Set them as variables in every new PowerShell window, then paste the commands below as they are:

```powershell
$Repos     = "C:\Users\<you>\source\repos"
$Engine    = "$Repos\MeitouClient"
$Mods      = "$Repos\MeitouMods"
$Ashen     = "$Repos\AshenHand"
$Husbandry = "$Repos\Husbandry"
$Logistics = "$Repos\LogisticsCore"
$War       = "$Repos\WarOrchestration"
$Bast      = "$Repos\BastFront"
```

## Prerequisites

- .NET 11 SDK (each repo's `global.json` names the version), Git, Visual Studio or Rider.
- Kenshi installed. If it is not in `C:\Program Files (x86)\Steam\steamapps\common\Kenshi`, set the `KENSHI_PATH` environment variable
  to its folder.

## Clone

```powershell
git clone --recurse-submodules https://github.com/Meitou-Modding-Ecosystem/MeitouMods.git $Mods
git clone --recurse-submodules https://github.com/Meitou-Modding-Ecosystem/AshenHand.git $Ashen
git clone --recurse-submodules https://github.com/Meitou-Modding-Ecosystem/Husbandry.git $Husbandry
git clone --recurse-submodules https://github.com/Meitou-Modding-Ecosystem/LogisticsCore.git $Logistics
git clone --recurse-submodules https://github.com/Meitou-Modding-Ecosystem/WarOrchestration.git $War
git clone --recurse-submodules https://github.com/Meitou-Modding-Ecosystem/BastFront.git $Bast
git clone https://github.com/Meitou-Modding-Ecosystem/MeitouClient.git $Engine
```

Every mod repo has its own `engine` submodule, so `$Engine` is needed only to write engine hooks.

## Build and play one mod

**Change to the mod's repo:**

```powershell
cd $Ashen
dotnet build
dotnet run --project Play
```

- The first build downloads each framework named in `meitou.lock.json` into `deps` and stops if its SHA-256 does not match.
- `dotnet run --project Play` (or F5 on the `Play` project in Visual Studio) copies the locked frameworks and the mod into `Kenshi\mods`
  and starts the engine.
- Enable the frameworks above the mod in the launcher, as for any dependency.

## Change a framework and a mod together

Point the mod repo at your framework clone:

```powershell
Set-Content "$Ashen\Meitou.user.props" "<Project><PropertyGroup><RivalryCoreSource>$Mods</RivalryCoreSource></PropertyGroup></Project>"
```

- Builds in `$Ashen` now compile Rivalry Core from `$Mods` and use the engine in `$Mods\engine`, and Play deploys that Rivalry Core build.
- `Meitou.user.props` is git-ignored. If it already holds your Kenshi settings, add the `RivalryCoreSource` line inside its
  `PropertyGroup` instead.
- Logistics Core works the same way with `<LogisticsCoreSource>$Logistics</LogisticsCoreSource>`.
- Delete the line to go back to the locked ZIP.

**Framework chains.** Bast Front's `meitou.lock.json` lists War Orchestration and Logistics Core. To change both frameworks and Bast Front
together, override both in Bast Front:

```powershell
Set-Content "$Bast\Meitou.user.props" "<Project><PropertyGroup><WarOrchestrationSource>$War</WarOrchestrationSource><LogisticsCoreSource>$Logistics</LogisticsCoreSource></PropertyGroup></Project>"
```

The override passes down, so War Orchestration builds against `$Logistics` too, and the whole build uses the engine in
`$Logistics\engine`. Override only War Orchestration and it builds against the Logistics Core named in Bast Front's lock.

To ship it: merge the framework PR into `beta` of `$Mods`, which publishes a prerelease `rivalry-core-<version>-beta.<commit>`. Then, in
`$Ashen`, set `meitou.lock.json` to that tag and the asset, SHA-256 and engine commit from its release notes, and open the mod PR. In a
chain, ship top-down: Logistics Core, then a War Orchestration build locking it, then Bast Front locking both. After the framework's
production release, move the lock to `rivalry-core-v<version>`.

## Change the engine and a mod together

Check out your engine branch inside the mod repo's submodule:

```powershell
git -C "$Ashen\engine" fetch origin <engine branch>
git -C "$Ashen\engine" checkout FETCH_HEAD
```

- Don't commit the new engine pointer until the engine PR is merged into `fork`.
- A mod that uses a framework also needs the framework rebuilt on that engine. Use the framework override above and check out the same
  engine branch in `$Mods\engine`.
- Order to land it: engine PR into `fork`, then the framework PR moving `$Mods\engine`, then the mod PR moving its lock and `engine`.

## Keep clones current

```powershell
git -C $Ashen pull
git -C $Ashen submodule update --init --recursive
```

The next build downloads a framework again only when `meitou.lock.json` changed.

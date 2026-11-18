# Squad rating

Rates any squad, vanilla or modded, as **how many Dust Bandits it beats half the time**. Two parts:

- **SquadArena** (`squad.arena`, `SquadArena/`): a test-only code mod. It fights two squads described in a request file next to the player
  and writes the result with a full fight log. Never enable it in a game you play.
- **SquadRating** (`SquadRating/`): the command-line tool. It reads squad files, finds names in your install and mod list, captures squads from
  saves, starts Meitou on an arena save, sends the fights and fits the ratings.

Nothing here changes the engine; it runs on any Meitou build with code mods.

## Setup

Folders: your MeitouMods clone and a folder of your own for squad files and results (here `squads`, beside the clone).

1. Build and deploy: open `MeitouMods.slnx` and build `Play` ([play.md](play.md)). It copies SquadArena into `Kenshi\mods\SquadArena` and
   writes `SquadArena.mod` (the arena factions).
2. In the Meitou launcher, enable **SquadArena** and trust its code.
3. Make the arena save: load any game, put your character on flat, empty ground away from towns and wildlife, and save it as `squad-arena`.
   Fights happen on a ring 250 units round that character. The engine's own battle plateau is at `-74028, 13428` (teleport there with F10).
4. **Go to your squads folder** (`cd ..\squads`) and write `squad-rating.json`:

```json
{ "gameExe": "C:\\Users\\you\\source\\repos\\MeitouMods\\engine\\src\\Meitou.Game\\bin\\Release\\net11.0\\MeitouClient.exe" }
```

Run the tool from that folder as `dotnet run --project ..\MeitouMods\SquadRating -- <command>` (below: `SquadRating <command>`).

## Commands

| Command | Does |
| --- | --- |
| `find <text> [--kind K]` | records whose name contains the text: id, type, name, defining file. Kinds: `character`, `template`, `race`, `item`, `maker`, `model` |
| `check <file>` | resolves a squad file and prints its roster |
| `capture --save <save> --squad <name> [--out <file>]` | writes a squad file from one of your squads in a save |
| `rate <file>... [--fights 12]` | rates each squad against the yardstick |
| `compare <A> <B> [--fights 10]` | head to head, sides swapped each fight, then both ratings |
| `bench [--filter <text>] [--fights 8] [--fresh]` | rates every town guard and patrol template at squad size 1; skips templates already rated unless `--fresh` |
| `report [--csv <file>]` | the ratings from every fight run in this folder |
| `write-mod <file>` | writes `SquadArena.mod` |

Fighting commands start Meitou on the arena save and quit it at the end. `--attach <folder>` uses a Meitou you started yourself with
`--control-dir <folder>`; `--headless` starts it without a window; `--save <name>` picks another arena save.

## Squad files

One JSON file per squad. Every record is an in-game name or an id; `find` lists both. A name that matches several records stops the tool and
lists them with their files; use the id then.

```json
{
  "name": "Ashen Hand hit squad",
  "members": [
    { "character": "Dust Bandit", "count": 3 },
    { "character": "Ashen Hand Hunter", "race": "Shek",
      "gear": [ { "item": "Katana", "maker": "Edgewalkers", "model": "Specialist", "level": 60 },
                { "item": "Samurai Armour", "quality": 80 } ],
      "stats": { "katanas": 55, "melee attack": 50, "toughness": 40 } },
    { "template": "Bandit raid small" }
  ]
}
```

| Field | Meaning |
| --- | --- |
| `character` | a CHARACTER or ANIMAL_CHARACTER: body, race, default gear and stats as the game spawns it |
| `template` | instead of `character`: one roll of a SQUAD_TEMPLATE's roster at squad size 1 (slaves and prisoners left out) |
| `count` | copies of the character, or rolls of the template (default 1) |
| `race` | draws and builds the body as this RACE |
| `gear` | the whole loadout: the character's own gear is destroyed first. Weapons take `maker`, `model`, `level`; anything else `quality` (0 to 100) |
| `stats` | stat values as the stats window shows them, set after the spawn |
| `name` | a note for readers; `capture` writes the character's name |

### Stats

Names of the engine's `StatsEnumerated`, any case, spaces ignored. Combat: `strength`, `toughness`, `dexterity`, `melee attack`,
`melee defence`, `dodge`, `martial arts`, `athletics`, `katanas`, `sabres`, `hackers`, `heavy weapons`, `blunt`, `polearms`, `crossbows`,
`turrets`, `perception`, `friendly fire`, `mass combat`. Others: `stealth`, `assassination`, `thieving`, `lockpicking`, `swimming`, `medic`,
`labouring`, `science`, `engineering`, `robotics`, `smithing weapon`, `smithing armour`, `smithing bow`, `farming`, `cooking`.

### Capture

`capture` reads the save as the game does: each member's character record, race, worn gear (weapons with maker, model and level; armour with
quality) and every stat. Backpacks, prosthetic limbs and grid items are left out. Build the squad in game (F10 tools help), save, capture.

## The yardstick

A squad's rating is the count of Dust Bandits (`"yardstick"` in settings) it beats half the time. `rate` runs a staircase: up after a win,
down after a loss, steps shrinking from ×1.6 to ×1.15, so fights gather where they say the most. The fit is Bradley–Terry: side A beats side B
with chance σ(s_A − s_B), a squad's strength s is fitted, n Dust Bandits have s = β·ln n, and the rating is exp(s/β). Every fight in the folder
counts, `compare` fights too, so squads that never met are still comparable. A draw counts half. The range shown is 90%.

## Bench

`bench` takes every SQUAD_TEMPLATE a TOWN lists as `residents` (guards) or `roaming squads` (patrols), leaving out traders and templates with
nobody armed, and rates one roll per fight at squad size 1. The player's Squad size setting never matters: the arena spawns exact rosters.

## Running fights

- Each fight takes the next spot on a ring of 8 round the player; the arena save is loaded again every 8 fights, so bodies don't pile up.
- The game runs at speed 5 while fighting; nothing pauses between ticks.
- Results stay in `squad-rating-runs/`: `fights.jsonl` (one line per fight, what the fit reads) and `results/<id>.json` (each full result).

## How a fight runs

Each fighter is spawned alone in an idle squad of its side's arena faction: side 0 south of the spot, side 1 north, ranks of up to 10 facing
each other 40 apart. The loadout and stats are applied on the next tick. After 30 ticks, once a tick, every standing fighter without a target is
ordered at the nearest standing enemy, and once a second a fighter circling a target with more than 5 attackers turns on the nearest enemy with
room (as the engine's `--battle` drives its blocks). A knocked-out fighter who gets up rejoins. The fight ends the first tick a side has nobody
standing, or as a draw after 300 game seconds; then every fighter is killed.

## The fight log

Each result's `log` lists, in game seconds from the first order:

| Kind | Fields |
| --- | --- |
| `hit` | `fighter` (victim), `by` (attacker; -1 for someone outside the fight), `weapon`, `source` (`melee`/`projectile`), `part`, `damage` and `absorbed` (cut, blunt, pierce), `onDowned` |
| `down`, `up`, `dead` | `fighter` |
| `severed`, `crushed` | `fighter`, `part` |

`fighters` maps each index to side, name, character, race and starting weapon; `sides` gives each side's standing, alive and damage dealt.

## Request and result

SquadArena reads `Kenshi/mods/SquadArena/arena/inbox/<id>.json` (oldest name first, deleted when taken) and writes
`arena/results/<id>.json`. A request is `{ id, at: [x, z], timeoutSeconds, seed, sides: [ { name, members } x2 ] }` with members as in squad
files but ids only. A result is `{ id, winner (0, 1, -1 draw), outcome (decided, timeout, refused), error, seconds, sides, fighters, log }`.

## The arena factions

`SquadArena.mod` holds two FACTION records, Arena Red (`1-SquadArena.mod`) and Arena Blue (`2-SquadArena.mod`): default relation 0, no roaming
population, relation -100 to each other. `write-mod` writes it; it holds no game content.

## Settings

`squad-rating.json` (all optional; the flags `--game`, `--kenshi`, `--save`, `--headless`, `--arena`, `--runs` win): `gameExe`, `kenshiDir` (else
found as the game finds it), `save`
(`squad-arena`), `headless`, `controlDir`, `arenaDir`, `runsDir` (`squad-rating-runs`), `fightsPerLoad` (8), `spotDistance` (250),
`timeoutSeconds` (300), `yardstick` (`Dust Bandit`).

---
name: squad-rating
description: Use when a mod author wants a squad rated, compared or benchmarked in Dust-bandit equivalents, or asks for a squad file (e.g. "3 Shek warriors in samurai armour with heavy jittes, skills around 50").
---

# Rate squads with SquadRating

SquadRating fights squads in a running Meitou through the SquadArena test mod and rates each as the number of Dust Bandits it beats half
the time. Read `docs/squad-rating.md` for setup, the file format and the commands. Run every command from the user's squads folder:
`dotnet run --project <MeitouMods>/SquadRating -- <command>`.

## Writing a squad file from a description

1. Pull out each kind of member, its count, race, weapon (with grade), armour pieces and skill levels.
2. Look every name up in the user's own data, never from memory: `find "<text>" --kind character|race|item|maker|model|template`. Records from
   their mods resolve too. Use the exact name when `find` shows one match of that name, else the id.
3. Pick a `character` close to the body and default gear wanted (for example "Dust Bandit", or a Shek record), then set `race`, `gear` and
   `stats`. `gear` replaces all of the character's gear, so list every piece. Weapons take `maker`, `model`, `level`; armour `quality` 0-100.
4. "Skills around 50": set the weapon's skill, `melee attack`, `melee defence`, `toughness`, `strength`, `dexterity`, `dodge` and `athletics`
   to 50 unless the user says otherwise. Stat names are in the guide's "Stats" table.
5. A vanilla squad as the game rolls it is `{ "template": "<squad template>" }`.
6. Save it as `<name>.squad.json` and run `check <file>`; fix every error it lists before fighting.

## Running

- Rating one squad: `rate <file>` (12 fights). Two squads: `compare <A> <B>`, then both are also on the shared scale. Vanilla baseline:
  `bench` (long; `--filter` narrows it). `report` refits everything run in the folder.
- A fighting command starts and quits Meitou on the arena save, so tell the user first and don't run two at once. It is windowed unless they ask
  for `--headless`.
- When the user changes a squad file, its old fights stay filed under the old version: the new version is rated from scratch.

## Reading results

- Give the rating with its 90% range, and say how many fights it rests on. A wide range means more fights (`rate` again adds to it).
- For why a squad won or lost, read `squad-rating-runs/results/<id>.json`: its `log` has every hit (attacker, weapon, body part, damage after
  armour), every downing, recovery, death and lost limb.
- Ask the user before adding tests (repo rule), saying what they would catch.

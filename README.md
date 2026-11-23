# SquadArena

Rates Kenshi squads in Dust-bandit equivalents for [MeitouClient](https://github.com/Meitou-Modding-Ecosystem/MeitouClient) mod authors:
the SquadRating command-line tool writes fights, the test-only SquadArena code mod (`squad.arena`) fights them in a running game, and the
tool fits each squad's strength. How to set it up and use it: [docs/squad-rating.md](docs/squad-rating.md). Agents use the
[squad-rating skill](.claude/skills/squad-rating/SKILL.md). GPL-3.0-or-later.

SquadArena spawns fighters next to the player: it is a tool, not a mod to play with.

- Build and run locally with Visual Studio, VS Code or the terminal (this repo's engine or the installed launcher, deployed to the game): [docs/play.md](docs/play.md).
- Setting up the repos on your machine: [docs/local-development.md](docs/local-development.md).
- Instructing agents on a change that spans repos: [docs/agents-across-repos.md](docs/agents-across-repos.md).
- Agent rules for the engine pin, versions, CI and releases: [CLAUDE.md](CLAUDE.md) (same as `AGENTS.md`); step-by-step procedures:
  `.claude/skills/meitou-engine-release`.

## Numbers

Task types and dialogue codes are shared by every loaded mod; Squad Arena claims none yet; its block is reserved
([mod number blocks](https://github.com/TheIvoryDisaster/GutenStructure/blob/beta/docs/numbers.md)).

Block 4700-4799

| Number | Pool | Use |
| --- | --- | --- |

## Layout

- `engine/`: MeitouClient as a git submodule, pinned to the engine commit the mod builds against. `git pull` does not move it: after
  every pull run `powershell -ExecutionPolicy Bypass -File tools\Update-Local.ps1 -Clean -Build`
  ([keep clones current](docs/local-development.md#keep-clones-current)).
- `SquadArena/`: the arena code mod and `meitou.json`. `SquadRating/`: the command-line tool; its `write-mod` command writes
  `SquadArena.mod` (Play runs it).
- `Play/`: one-click build, deploy and run.
- `Directory.Build.props` imports the engine's, so mods build with the game's settings (warnings are errors).
- `Directory.Build.rsp` stamps the engine references as the oldest Meitou release the mod supports, equal to its `gameVersion` floor: the
  game refuses a mod referencing a newer engine stamp, and the submodule builds `1.0.0.0`
  ([versioning](https://github.com/TheIvoryDisaster/GutenStructure/blob/beta/docs/versioning.md)).

## Branches

PRs into `beta`; CI (build and tests) must pass. `beta` is promoted to `main` once tested in play, and production goes through a PR to
`release`. Each merge into `release` tags it `<slug>-v<version>` and publishes the mod's zip (`.github/workflows/release.yml`) unless that
tag exists; `tools\Tag-Release.ps1` is the manual fallback.

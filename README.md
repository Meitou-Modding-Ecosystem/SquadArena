# SquadArena

Rates Kenshi squads in Dust-bandit equivalents for [MeitouClient](https://github.com/Meitou-Modding-Ecosystem/MeitouClient) mod authors:
the SquadRating command-line tool writes fights, the test-only SquadArena code mod (`squad.arena`) fights them in a running game, and the
tool fits each squad's strength. How to set it up and use it: [docs/squad-rating.md](docs/squad-rating.md). Agents use the
[squad-rating skill](.claude/skills/squad-rating/SKILL.md). GPL-3.0-or-later.

SquadArena spawns fighters next to the player: it is a tool, not a mod to play with.

- Build and run locally with Visual Studio, VS Code or the terminal (this repo's engine or the installed launcher, deployed to the game): [docs/play.md](docs/play.md).
- Setting up the repos on your machine: [docs/local-development.md](docs/local-development.md).
- Instructing agents on a change that spans repos: [docs/agents-across-repos.md](docs/agents-across-repos.md).

## Layout

- `engine/`: MeitouClient as a git submodule, pinned to the engine commit the mod builds against. After a pull that moves it:
  `git submodule update --init`.
- `SquadArena/`: the arena code mod and `meitou.json`. `SquadRating/`: the command-line tool; its `write-mod` command writes
  `SquadArena.mod` (Play runs it).
- `Play/`: one-click build, deploy and run.
- `Directory.Build.props` imports the engine's, so mods build with the game's settings (warnings are errors).

## Branches

PRs into `beta`; CI (build and tests) must pass. `beta` is promoted to `main` once tested in play, and production goes through a PR to
`release`.

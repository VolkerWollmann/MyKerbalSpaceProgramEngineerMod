# EngineerFuelSaver

*[Deutsche Fassung](README.de.md)*

A Kerbal Space Program 1.12.5 plugin: an engineer aboard reduces the fuel consumption of
every engine on the vessel by **up to 5 %**, depending on the experience level.

| Engineer level | 0 | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|---|
| Fuel saved | 0 % | 2 % | 3 % | 4 % | 5 % | 5 % |

**Nota bene:** In the VAB the engineer can look like a *net loss* on some vessel designs - the
extra 94 kg of crew costs delta-v, and on light vessels that outweighs the saving at every
level. A level 0 engineer offsets nothing at all. Compare like for like: the same seat filled
by a non-engineer, not an empty seat. K.E.R. (Kerbal Engineer Redux) shows the effect, or make
a test flight to low orbit with and without an engineer aboard and compare the remaining
delta-v.

* The engineer has to be **at the controls**: in a command pod (capsule, cockpit, cupola)
  or in an external command seat. In a passenger cabin or the lab he cannot do anything
  and saves nothing (switch it off with `requireCommandPod`).
* Only the **most experienced** engineer aboard counts - engineers do not stack.
* Implemented through **specific impulse**, so delta-v rises accordingly and every
  in-game readout stays consistent.
* Solid boosters are excluded (configurable).
* No ModuleManager required.

## Installation

Copy the folder `GameData/EngineerFuelSaver` into your KSP installation's `GameData`
directory, so that you end up with:

```
Kerbal Space Program/GameData/EngineerFuelSaver/
    Plugins/EngineerFuelSaver.dll
    EngineerFuelSaver.cfg
    EngineerTrait.cfg
    EngineerFuelSaver.version
```

To uninstall, delete that folder. The mod writes nothing into save games, so removing it
leaves no traces behind.

## How the bonus is calculated

For a saving of `p`, two values on the engine module are changed:

```
atmosphereCurve *= 1 / (1 - p)    // Isp rises at every altitude
maxFuelFlow     *= (1 - p)        // flow drops by exactly p
```

Since `thrust = flow x Isp x g0`, **thrust stays unchanged** while exactly `p` less fuel
is consumed. Raising Isp alone would instead give more thrust at the same consumption -
both yield the same delta-v, but only the variant above literally "saves fuel".

Level 5 on an LV-T30 "Reliant": the Isp curve goes from 310.0 / 265.0 s (vacuum / sea
level) to 326.3 / 278.9 s, and `maxFuelFlow` from 0.078947 to 0.075. Thrust check:
`0.075 x 326.3 x 9.81 = 240.1 kN`, unchanged against the part's `maxThrust = 240`.

### Why not multIsp / multFlow

The first implementation used the multiplier fields `multIsp` and `multFlow`. They are
set without complaint, but **no delta-v calculation reads them** - not the stock stage
display, not Kerbal Engineer Redux, not MechJeb. The bonus was invisible in every
readout. `atmosphereCurve` and `maxFuelFlow` are read by all of them.

None of the fields used are written to save games (verified against `persistent.sfs`:
neither `maxFuelFlow` nor `atmosphereCurve` appear there). After each change
`VesselDeltaV.SetCalcsDirty(true)` is called so the stock display does not keep showing a
cached value.

## The skill readout

`FuelSavingSkill.cs` is a custom kerbal skill, so the saving shows up in the kerbal's
info panel next to "Provides repair skills" and the other engineer abilities. It is
purely informational - the actual work happens in `EngineTweaker`.

KSP discovers the class by reflection over the loaded assemblies
(`[ExperienceSystem]: Found N effect types`). It is attached to the trait through the
bundled `EngineerTrait.cfg`: KSP merges multiple `EXPERIENCE_TRAIT` nodes that share the
same `name` - the same mechanism the Serenity expansion uses to add
`DeployedSciencePowerSkill` to the engineer. No ModuleManager needed.

The text lives in the config (`effectDescription`), default `FuelSaving:<saving>`. Every
kerbal owns its own instance of the skill, so `<saving>` shows that kerbal's actual value:
level 3 yields `FuelSaving:4%`. The level comes from `Parent.CrewMemberExperienceLevel()`.
Further placeholders: `<level>`, `<perLevel>`, `<maxSaving>`, `<maxLevel>`.

All values derive from `fuelSavingPerLevel` and `levelOffset`, so the readout and the rule
cannot drift apart. For the same reason the effect deliberately carries no `modifiers` of
its own.
Use angle brackets, not curly ones: `{` and `}` are node delimiters in the config format
and would truncate the value along with every line that follows it.

The description is only built when it is shown, so it loads the config on demand if needed
(`Settings.EnsureLoaded`) instead of relying on the settings addon having run. In practice
the addon (MainMenu) starts before the experience system initialises - the fallback rarely
fires, but it keeps `GetDescription()` independent of the scene order.

## Project layout

```
src/EngineerFuelSaver/
  EngineerFuelSaver.csproj   build (net472, references the KSP installation)
  Settings.cs                reads the config at startup
  EngineerBonus.cs           rules: best engineer, saving, exclusions
  EngineTweaker.cs           applies and reverts the engine values, keeps the originals
  FlightBonusController.cs   sweeps all loaded vessels in flight
  EditorBonusController.cs   sweeps the vessel in the editor (VAB/SPH)
  FuelSavingSkill.cs         kerbal skill for the info panel (display only)
  Log.cs                     logging to KSP.log
GameData/EngineerFuelSaver/
  EngineerFuelSaver.cfg      configuration
  EngineerTrait.cfg          attaches the skill to the engineer trait
  EngineerFuelSaver.version  KSP-AVC version file
```

In the **editor**, the mod reads the crew from `ShipConstruction.ShipManifest` - the very
assignment made in the crew dialog - and applies the same bonus, so the delta-v readout is
already correct in the VAB and not only on the launch pad. Both controllers share the same
logic in `EngineTweaker`.

The crew is counted per part rather than per vessel: `EngineerBonus.IsCommandPart` only
admits parts that offer a place at the controls, which means parts carrying a
`ModuleCommand` plus the external command seat. That seat has no `ModuleCommand` - its
control comes from the kerbal himself - so two more modules are accepted for it, depending
on where the game currently keeps the kerbal: on the seat in the editor (`KerbalSeat`), and
in flight on the occupant's own part (`KerbalEVA`), which hangs off the seat and carries
the `crew = ...` line in the save file. Nothing is counted twice: there is no EVA part in
the editor, and in flight the seat itself is empty. A kerbal floating free is a vessel of
his own without engines, so he cannot grant a bonus to any other vessel.

Each controller recalculates everything every `refreshInterval` seconds (0.5 s by default)
rather than listening to individual GameEvents. Docking, crew transfer, staging, EVA and
mid-flight level-ups are covered without special handling. When a vessel unloads or the
flight scene ends, the original values are written back.

## Building

Requires KSP 1.12.5. The reference assemblies in `KSP_x64_Data/Managed` are referenced
only, never redistributed.

```
dotnet build src/EngineerFuelSaver/EngineerFuelSaver.csproj -c Release
```

`KSPRoot` defaults to the usual Steam location. If KSP lives somewhere else, create a
`Directory.Build.props` in the repository root. It is not version-controlled, so the path
stays on your machine and never reaches the repo:

```xml
<Project>
  <PropertyGroup>
    <KSPRoot>D:\Games\KSP</KSPRoot>
  </PropertyGroup>
</Project>
```

For a one-off build the command line overrides both:

```
dotnet build src/EngineerFuelSaver/EngineerFuelSaver.csproj -c Release -p:KSPRoot="D:\Games\KSP"
```

The build then deploys the DLL and the config files to
`<KSPRoot>/GameData/EngineerFuelSaver/`. To build without deploying:

```
dotnet build src/EngineerFuelSaver/EngineerFuelSaver.csproj -c Release -p:SkipDeploy=true
```

Existing config files in the game are never overwritten, so your own settings survive
every rebuild.

## Configuration

All values live in `GameData/EngineerFuelSaver/EngineerFuelSaver.cfg`:

| Key | Default | Meaning |
|---|---|---|
| `fuelSavingPerLevel` | `0.01` | saving per experience level |
| `maxLevel` | `5` | highest level taken into account |
| `levelOffset` | `1` | levels credited on top before the cap; `0` restores 1 % per level |
| `maxFuelSaving` | `0.9` | hard upper bound |
| `refreshInterval` | `0.5` | seconds between recalculations |
| `excludedPropellants` | `SolidFuel` | engines that get no bonus |
| `effectDescription` | `FuelSaving:<saving>` | skill text in the kerbal info panel |
| `requireCommandPod` | `True` | only engineers in a command pod or external seat count |
| `fullBonusWhenExperienceDisabled` | `True` | behaviour without the experience system (sandbox) |
| `debugLog` | `False` | per-vessel and per-engine output to KSP.log |
| `debugLevelOverride` | `-1` | testing only: the engineer aboard counts as this level |

`debugLevelOverride` only replaces the level of an engineer who is **actually present**.
Without an engineer aboard or in the crew manifest it stays at 0 %, which makes the crew
detection testable without grinding experience first.

## Verified in game

Built against KSP 1.12.5 (build 03190, Steam), 0 errors and 0 warnings.

* The config is read completely; the mod loads without exceptions alongside
  ModuleManager, MechJeb2, Kerbal Engineer Redux, SCANsat and Final Frontier.
* Crew evaluation: the engineer is identified by `trait`, the level is correct, and the
  editor crew manifest is read.
* Engine values are set exactly - cross-checked against the stock part config, see the
  Reliant numbers above.
* The `FuelSavingSkill` appears in the kerbal info panel. Confirmed in the log by
  `[ExperienceSystem]: Found 21 effect types` (20 without the mod).
* Control case: a **pilot** at level 1 aboard yields 0 % - the profession matters, not
  just the level.
* Level-up in normal operation: `Orbit,Kerbin` + `Recover` in the flight log, afterwards
  level 1 and 2 % saving, with no debug switches.
* The editor delta-v readout reflects the bonus.

One note on log reading: `EngineerFuelSaver` sorts alphabetically before `Squad`, so this
mod's node creates the engineer trait and the stock effects are added to it. The log
therefore shows all Squad effects as `Added Effect ... to Trait 'Engineer'` but not
`FuelSavingSkill` - KSP only logs additions, never the effects of the node that created
the trait. A missing `FuelSavingSkill` in the log is not a sign of failure.

With `debugLog = True` the log carries the Isp curve set for each engine, and during a
burn the values the game itself computed (`realIsp`, thrust, flow). `realIsp` is the
decisive measurement: it comes out of KSP's own calculation, not this mod's.

## Known limitations

* **Loaded vessels only.** Unloaded vessels outside physics range consume no fuel in
  stock anyway.
* **RCS** (`ModuleRCS`) gets no bonus, main engines only. What counts is the module, not
  the propellant: the Vernor thruster burns LF/Ox and still gets nothing because it is an
  RCS part, while the O-10 "Puff" is a proper engine and gets the full bonus on
  monopropellant.
* The `requireCommandPod` rule has **not been measured in game yet** - it compiles against
  KSP 1.12.5 and the module names are verified, but a test flight with an engineer in a
  passenger cabin or an external seat is still outstanding.
* With kerbal experience disabled (typical in sandbox), stock treats every kerbal as
  fully trained; `fullBonusWhenExperienceDisabled = True` follows that and grants the
  full bonus there.
* Mods touching the same fields (RealFuels, engine upgrades) can conflict. The controller
  detects overwritten values and reapplies them, but only reports this in the log.

## License

MIT, see [LICENSE](LICENSE). Copyright (c) 2026 Volker Wollmann.

Use, modify and redistribute freely - the copyright notice and the license text must be
retained.

The KSP assemblies in `KSP_x64_Data/Managed` are referenced for compilation only and are
not part of this repository. They must not be redistributed.

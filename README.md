# EngineerFuelSaver

*[Deutsche Fassung](README.de.md)*

A Kerbal Space Program 1.12.5 plugin: an engineer aboard reduces the fuel consumption of
every engine and RCS thruster on the vessel by **up to 5 %**, depending on the experience
level.

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
* **RCS thrusters** save just the same (switch it off with `includeRcs`). Thrust and
  control authority stay unchanged, the monopropellant simply lasts longer.
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

`g0` here is standard gravity, 9.80665 m/s^2 - not Kerbin's surface gravity and not the
rounded 9.81. KSP hard-codes the value in both the engine and the RCS thruster; it serves
only to turn an Isp given in seconds into an exhaust velocity, and it is the same on every
celestial body. For the bonus it cancels out anyway: scaling Isp and flow in opposite
directions leaves the product unchanged, whatever the value happens to be.

Level 5 on an LV-T30 "Reliant": the Isp curve goes from 310.0 / 265.0 s (vacuum / sea
level) to 326.3 / 278.9 s, and `maxFuelFlow` from 0.078947 to 0.075. Thrust check:
`0.075 x 326.3 x 9.80665 = 240.0 kN`, unchanged against the part's `maxThrust = 240`.

### RCS does the arithmetic differently and saves the same

`ModuleRCS` (and therefore `ModuleRCSFX`) carries the same two fields but arrives at
thrust by a different route:

```
maxFuelFlow = thrusterPower / (Isp(vacuum) x g0)   once, when the part is loaded
exhaustVel  = Isp(altitude) x g0                   recomputed every FixedUpdate
consumption = maxFuelFlow x throttle,  thrust = consumption x exhaustVel
```

There is no minimum flow. Because `exhaustVel` is read fresh from `atmosphereCurve` on
every physics step, the scaled curve takes effect immediately; the raised value shows up
as `realISP` in the thruster's right-click menu.

The arithmetic works out exactly as it does on an engine. At 5 %:

| | RV-105 | Vernor |
|---|---|---|
| `thrusterPower` | 1 kN | 12 kN |
| Isp vacuum | 240 -> 252.6 s | 260 -> 273.7 s |
| Isp sea level | 100 -> 105.3 s | 140 -> 147.4 s |
| `maxFuelFlow` | 0.000425 -> 0.000404 | 0.004706 -> 0.004471 |
| Vacuum thrust | 1.000 kN | 12.000 kN |

The Vernor runs on LF/Ox, the RV-105 on monopropellant - what counts is the propellant,
not the part type: `excludedPropellants` applies here exactly as it does to engines.

### Why not multIsp / multFlow

The first implementation used the multiplier fields `multIsp` and `multFlow` (`ispMult`
and `flowMult` on an RCS thruster). They are set without complaint, but **no delta-v
calculation reads them** - not the stock stage display, not Kerbal Engineer Redux, not
MechJeb. The bonus was invisible in every readout. `atmosphereCurve` and `maxFuelFlow` are
read by all of them.

None of the fields used are written to save games (verified against `persistent.sfs`:
neither `maxFuelFlow` nor `atmosphereCurve` appear there). After each change
`VesselDeltaV.SetCalcsDirty(true)` is called so the stock display does not keep showing a
cached value.

## The skill readout

`FuelSavingSkill.cs` is a custom kerbal skill, so the saving shows up in the kerbal's
info panel next to "Provides repair skills" and the other engineer abilities. It is
purely informational - the actual work happens in `EngineTweaker` and `RcsTweaker`.

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
  ThrusterTweaker.cs         applies and reverts the values, keeps the originals (base)
  EngineTweaker.cs           the engine's fields (ModuleEngines/FX)
  RcsTweaker.cs              the RCS thruster's fields (ModuleRCS/FX)
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
logic in `ThrusterTweaker`.

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
| `includeRcs` | `True` | RCS thrusters get the same bonus |
| `excludedPropellants` | `SolidFuel` | propellants that get no bonus |
| `effectDescription` | `FuelSaving:<saving>` | skill text in the kerbal info panel |
| `requireCommandPod` | `True` | only engineers in a command pod or external seat count |
| `fullBonusWhenExperienceDisabled` | `True` | behaviour without the experience system (sandbox) |
| `debugLog` | `False` | per-vessel, per-engine and per-thruster output to KSP.log |
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

### Two flights compared: pilot against engineer

The same vessel on the same ascent, only the crew swapped - once Jebediah (pilot level 1),
once Bill (engineer level 1, so 2 %):

```
MunTourist1: bester Ingenieur Level 0 -> 0.0 % ... Jebediah Kerman (Pilot Level 1)
MunTourist1: bester Ingenieur Level 1 -> 2.0 % ... Bill Kerman (Engineer Level 1)
```

Values applied on the engineer's flight, factor 1 / 0.98 = 1.0204:

| Module | `maxFuelFlow` | Isp vacuum | Isp sea level |
|---|---|---|---|
| Bobcat LV-TX87 | 0.13158 -> 0.12894 | 310.0 -> 316.3 s | 290.0 -> 295.9 s |
| Skipper RE-I5 | 0.20713 -> 0.20299 | 320.0 -> 326.5 s | 280.0 -> 285.7 s |
| RCS Place-Anywhere 7 | 0.00085 -> 0.00083 | 240.0 -> 244.9 s | 100.0 -> 102.0 s |
| Kickback SRB | 0.31055 unchanged | 220.0 s | 195.0 s |

And what KSP itself computed during the burn, at identical throttle:

| Throttle 100 % | Pilot | Engineer | Ratio |
|---|---|---|---|
| Bobcat `realIsp` | 310.0 s | 316.3 s | 1.0203 |
| Bobcat thrust | 400.0 kN | 400.0 kN | **unchanged** |
| Bobcat flow | 26.3153 | 25.7890 | **0.98000** |
| Skipper `realIsp` | 320.0 s | 326.5 s | 1.0203 |
| Skipper thrust | 650.0 kN | 650.0 kN | **unchanged** |
| Skipper flow | 41.4260 | 40.5975 | **0.98000** |

Exactly the intent: same thrust, exactly 2 % less flow. The Kickback shows the same peak
flow of 41.4067 on both flights - the `SolidFuel` exclusion holds in the middle of the
engineer's flight while every other module sits at level 1.

For **RCS**, KSP's own `realISP` confirms the raised curve across more than 1,260 firing
lines, cleanly separated between the two flights:

| | Pilot | Engineer |
|---|---|---|
| `realISP` in vacuum | 240.0 s | 244.9 s |
| `realISP` low in the atmosphere | 101.0 s | 103.1 s |

240.0 x 1.0204 = 244.90. There is **no thrust pair for RCS** the way there is for the
engines: a thruster's thrust follows the momentary control deflection, which is never the
same across two flights. The observed peaks (1.58 against 1.61 kN on a 2.0 kN rating) are
both partial deflections and prove nothing either way. That thrust stays unchanged follows
here from the two fields measured individually - `maxFuelFlow` times 0.98 and `realISP`
times 1.0204 - not from a direct thrust measurement.

No exceptions in the log; the assembly loads as `EngineerFuelSaver, Version=1.3.0.0`.

## Known limitations

* **Loaded vessels only.** Unloaded vessels outside physics range consume no fuel in
  stock anyway.
* For **RCS** the raised Isp curve is measured in flight (`realISP` 240.0 -> 244.9 s in
  vacuum), but the **unchanged thrust is not measured directly** - that would need the same
  control deflection on both flights, which cannot be arranged. It follows from the two
  fields verified individually.
* The `requireCommandPod` rule has **not been measured in game yet** - it compiles against
  KSP 1.12.5 and the module names are verified, but a test flight with an engineer in a
  passenger cabin or an external seat is still outstanding.
* With kerbal experience disabled (typical in sandbox), stock treats every kerbal as
  fully trained; `fullBonusWhenExperienceDisabled = True` follows that and grants the
  full bonus there.
* Mods touching the same fields (RealFuels, engine upgrades) can conflict. The controller
  detects overwritten values and reapplies them, but only reports this in the log.
* RCS does not enter the **stock delta-v readout**, so the bonus for the thrusters does
  not show up there even though it applies. The thruster's `realISP` in the right-click
  menu does show it.

## Versions

The number lives in `GameData/EngineerFuelSaver/EngineerFuelSaver.version` (KSP-AVC) and
in the csproj; every release also carries a git tag `vX.Y.Z`.

* **1.3.0** - RCS thrusters (`ModuleRCS`, and therefore `ModuleRCSFX`) get the same bonus
  as the main engines, switchable with `includeRcs`. This drops the former limitation that
  only main engines saved anything - the Vernor thruster now saves too. Cross-checked in
  game with two flights, pilot against engineer.
* **1.2.0** - The bonus now only applies to engineers at the controls: command pod or
  external command seat, switch it off with `requireCommandPod`. Not measured in game
  yet. Plus the clarification that the module type decides, not the propellant - the
  O-10 "Puff" gets the bonus, the Vernor thruster does not.
* **1.1.0** - Bonus curve with one level added before the cap (`levelOffset`): a level 1
  engineer saves as much as a level 2 engineer did before.
* **1.0.0** - First release.

## License

MIT, see [LICENSE](LICENSE). Copyright (c) 2026 Volker Wollmann.

Use, modify and redistribute freely - the copyright notice and the license text must be
retained.

The KSP assemblies in `KSP_x64_Data/Managed` are referenced for compilation only and are
not part of this repository. They must not be redistributed.

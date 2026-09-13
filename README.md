# EngineerFuelSaver

KSP-1.12.5-Plugin: Ein Ingenieur an Bord senkt den Treibstoffverbrauch aller Triebwerke
des Schiffs um **1 % pro Sterne-Level**.

| Level des Ingenieurs | 0 | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|---|
| Ersparnis | 0 % | 1 % | 2 % | 3 % | 4 % | 5 % |

* Es zaehlt nur der **beste** Ingenieur an Bord, mehrere Ingenieure stapeln sich nicht.
* Umgesetzt ueber den **spezifischen Impuls** - das Delta-v steigt entsprechend und alle
  Bordanzeigen bleiben konsistent.
* Feststoffbooster sind ausgenommen (konfigurierbar).

## Wie der Bonus rechnet

Bei einer Ersparnis `p` werden am Triebwerksmodul zwei Werte veraendert:

```
atmosphereCurve *= 1 / (1 - p)    // Isp steigt auf jeder Hoehe
maxFuelFlow     *= (1 - p)        // Durchfluss sinkt um genau p
```

Da `Schub = Durchfluss x Isp x g0` gilt, bleibt der **Schub unveraendert** und es wird
exakt `p` weniger Treibstoff verbraucht. Wuerde man nur den Isp anheben, bekaeme man
stattdessen mehr Schub bei gleichem Verbrauch - beides ergibt dasselbe Delta-v, aber nur
die Variante oben entspricht woertlich "spart Sprit".

Bei Level 5 und einem Terrier mit 345 s Vakuum-Isp: Isp-Kurve x 1,0526 auf 363 s,
`maxFuelFlow` x 0,95.

### Warum nicht multIsp/multFlow

Der erste Ansatz benutzte die Multiplikatorfelder `multIsp` und `multFlow`. Die werden
zwar gesetzt, aber von keiner Delta-v-Rechnung gelesen - weder von der
Stock-Stufenanzeige noch von KER oder MechJeb. Der Bonus war dadurch in jeder Anzeige
unsichtbar. `atmosphereCurve` und `maxFuelFlow` liest dagegen jede dieser Rechnungen.

Keines dieser Felder wird in Spielstaende geschrieben (nachgeprueft an `persistent.sfs`:
weder `maxFuelFlow` noch `atmosphereCurve` tauchen dort auf) - der Mod hinterlaesst nichts
Dauerhaftes. Nach jeder Aenderung wird zusaetzlich `VesselDeltaV.SetCalcsDirty(true)`
aufgerufen, damit die Stock-Anzeige nicht auf einem alten Wert stehen bleibt.

## Aufbau

```
src/EngineerFuelSaver/
  EngineerFuelSaver.csproj   Build (net472, Referenzen auf die KSP-Installation)
  Settings.cs                liest die cfg beim Spielstart
  EngineerBonus.cs           Regelwerk: bester Ingenieur, Ersparnis, Ausnahmen
  EngineTweaker.cs           setzt/entfernt die Werte am Triebwerk, haelt die Originale
  FlightBonusController.cs   Durchlauf ueber alle geladenen Schiffe im Flug
  EditorBonusController.cs   Durchlauf ueber das Schiff im Bauhof (VAB/SPH)
  FuelSavingSkill.cs         Kerbal-Faehigkeit fuer den Infoblock (nur Anzeige)
  Log.cs                     Logging ins KSP.log
GameData/EngineerFuelSaver/
  EngineerFuelSaver.cfg      Konfiguration
  EngineerTrait.cfg          haengt die Faehigkeit an den Ingenieur
```

`FuelSavingSkill.cs` ist eine eigene Kerbal-Faehigkeit, damit die Ersparnis im Infoblock
des Kerbals steht - neben "Provides repair skills" und den uebrigen Ingenieurs-
Faehigkeiten. Rein informativ, gerechnet wird in `EngineTweaker`.

KSP findet die Klasse per Reflection ueber die geladenen Assemblies
(`[ExperienceSystem]: Found N effect types`). Angehaengt wird sie ueber die mitgelieferte
`EngineerTrait.cfg`: Mehrere `EXPERIENCE_TRAIT`-Nodes mit demselben `name` fuehrt KSP
zusammen - denselben Weg benutzt die Serenity-Erweiterung, um dem Ingenieur den
`DeployedSciencePowerSkill` zu ergaenzen. **Kein ModuleManager noetig.**

Der Text steht in der cfg (`effectDescription`), Vorgabe `FuelSaving:<saving>`. Weil jeder
Kerbal eine eigene Instanz der Faehigkeit besitzt, zeigt `<saving>` dessen konkreten Wert:
Level 3 ergibt `FuelSaving:3%`. Das Level kommt aus `Parent.CrewMemberExperienceLevel()`.
Weitere Platzhalter: `<level>`, `<perLevel>`, `<maxSaving>`, `<maxLevel>`.

Alle Werte stammen aus `fuelSavingPerLevel` - Anzeige und Regel koennen nicht
auseinanderlaufen. Der Effekt fuehrt aus demselben Grund bewusst keine eigenen
`modifiers`. Spitze Klammern, nicht geschweifte: `{` und `}` sind im cfg-Format
Node-Klammern und schneiden den Wert samt aller folgenden Zeilen ab.

Steht `debugLevelOverride` auf einem Wert, zeigt der Text dieses Level statt des echten -
sonst widersprechen sich Anzeige und Wirkung waehrend eines Tests.

Weil die Faehigkeit schon waehrend des Datenbankladens gebaut wird - lange bevor das
Settings-Addon startet - laedt sie die cfg bei Bedarf selbst nach (`Settings.EnsureLoaded`).
Die eigene Config-Node liegt zu diesem Zeitpunkt bereits in der GameDatabase; im Log
gemessen sind es 42 ms Vorsprung. Sprache frei waehlbar; Voreinstellung ist Englisch,
passend zu den Stock-Faehigkeiten daneben.

Im **Bauhof** liest der Mod die Besatzung aus `ShipConstruction.ShipManifest` - also genau
die Zuweisung aus dem Crew-Dialog - und wendet denselben Bonus an, damit die
Delta-v-Anzeige schon dort stimmt und nicht erst auf der Startrampe. Beide Controller
teilen sich dieselbe Logik in `EngineTweaker`.

Der Controller rechnet alle `refreshInterval` Sekunden (Standard 0,5 s) komplett neu,
statt auf einzelne GameEvents zu hoeren. Docking, Crew-Transfer, Staging, EVA und
Level-Ups mitten im Flug sind damit ohne Sonderbehandlung abgedeckt. Beim Entladen eines
Schiffs und beim Verlassen der Flugszene werden die Originalwerte zurueckgeschrieben.

## Bauen

Vorausgesetzt wird KSP 1.12.5. Der csproj zeigt standardmaessig auf die vorhandene
Steam-Installation auf Laufwerk E:. Die Referenz-DLLs aus dem Ordner
`KSP_x64_Data/Managed` werden nur referenziert, nie mitausgeliefert.

```
dotnet build src/EngineerFuelSaver/EngineerFuelSaver.csproj -c Release
```

Der Build kopiert DLL und cfg anschliessend automatisch nach
`<KSPRoot>/GameData/EngineerFuelSaver/`. Ohne dieses Deployment:

```
dotnet build src/EngineerFuelSaver/EngineerFuelSaver.csproj -c Release -p:SkipDeploy=true
```

Steht KSP woanders:

```
dotnet build src/EngineerFuelSaver/EngineerFuelSaver.csproj -c Release -p:KSPRoot="D:\Spiele\KSP"
```

## Konfiguration

Alle Werte in `GameData/EngineerFuelSaver/EngineerFuelSaver.cfg`:

| Schluessel | Standard | Bedeutung |
|---|---|---|
| `fuelSavingPerLevel` | `0.01` | Ersparnis je Level |
| `maxLevel` | `5` | hoechstes gewertetes Level |
| `maxFuelSaving` | `0.9` | harte Obergrenze |
| `refreshInterval` | `0.5` | Sekunden zwischen zwei Neuberechnungen |
| `excludedPropellants` | `SolidFuel` | Triebwerke ohne Bonus |
| `effectDescription` | englischer Satz | Text der Faehigkeit im Kerbal-Infoblock |
| `fullBonusWhenExperienceDisabled` | `True` | Verhalten ohne Erfahrungssystem (Sandbox) |
| `debugLog` | `True` | Ausgabe je Schiff und Triebwerk ins KSP.log |
| `debugLevelOverride` | `-1` | nur zum Testen: der Ingenieur an Bord zaehlt als dieses Level |

`debugLevelOverride` ersetzt nur das Level eines **tatsaechlich vorhandenen** Ingenieurs.
Ohne Ingenieur an Bord bzw. im Crew-Manifest bleibt es bei 0 % - so laesst sich die
Crew-Erkennung testen, ohne erst XP zu sammeln.

Der Build kopiert die cfg nur, wenn sie im Spiel noch fehlt. Eigene Einstellungen
ueberleben also jeden Rebuild.

## Status

Gebaut gegen KSP 1.12.5 (Build 03190, Steam), 0 Fehler und 0 Warnungen. Das Plugin ist
nach `GameData/EngineerFuelSaver/` deployed.

Deinstallieren: den Ordner `GameData/EngineerFuelSaver` loeschen. Der Mod schreibt nichts
in die Spielstaende.

Im Spiel nachgewiesen (KSP.log und Bildschirm):

* Mod laedt, cfg wird vollstaendig gelesen.
* Crew-Auswertung: Ingenieur wird am `trait` erkannt, Level stimmt, Bauhof-Manifest wird
  gelesen.
* Triebwerkswerte werden exakt gesetzt - beim LV-T30 gegen die Stock-Konfiguration
  gegengerechnet: Isp-Kurve 310,0 / 265,0 s mal 1,0526 auf 326,3 / 278,9 s,
  `maxFuelFlow` 0,078947 mal 0,95 auf 0,075. Schubprobe: 0,075 x 326,3 x 9,81 = 240,1 kN,
  also unveraendert gegenueber `maxThrust = 240`.
* Die Faehigkeit `FuelSavingSkill` steht im Infoblock des Kerbals. Im Log bestaetigt durch
  `[ExperienceSystem]: Found 21 effect types` (vorher 20).

Zur Reihenfolge der Trait-Nodes: `EngineerFuelSaver` steht alphabetisch vor `Squad`, also
legt unsere Node den Trait an und die Stock-Effekte werden ihr hinzugefuegt. Im Log
erscheinen deshalb alle Squad-Effekte als `Added Effect ... to Trait 'Engineer'`, unser
eigener dagegen nicht - KSP protokolliert nur Ergaenzungen, nicht die Effekte der
erzeugenden Node. Ein fehlender `FuelSavingSkill` im Log ist also kein Hinweis auf einen
Fehler.

* Gegenprobe: Ein Pilot mit Level 1 an Bord bekommt 0 % - es zaehlt der Beruf, nicht nur
  das Level.
* Levelaufstieg im Normalbetrieb: Bill Kerman, `Orbit,Kerbin` + `Recover` im Flugbuch,
  danach Level 1 und 1 % Ersparnis. Ohne Testschalter.
* Die Delta-v-Anzeige im Bauhof zeigt den Bonus.

Damit ist die Kette vom Kerbal bis zur Anzeige vollstaendig belegt.

### Noch offen

Nur der zahlenmaessige Delta-v-Vergleich (erwartet: Faktor 1,0526 bei Level 5, TWR
identisch). Vorgehen: `debugLevelOverride = 5` setzen, Wert notieren, auf `-1` zurueck,
KSP neu starten, erneut ablesen. Bei den 1 % eines Level-1-Ingenieurs sind es rund
5 m/s auf 514 - zum Ablesen zu wenig.

Im KSP.log steht je Triebwerk die gesetzte Isp-Kurve, und waehrend eines Brennvorgangs
die vom Spiel selbst gerechneten Werte (`realIsp`, Schub, Durchfluss). Der `realIsp` ist
der entscheidende Messwert: er stammt aus KSPs eigener Rechnung, nicht aus unserer.

## Bekannte Einschraenkungen

* **Nur geladene Schiffe.** Ungeladene Schiffe ausserhalb des Ladebereichs verbrauchen
  in Stock ohnehin keinen Treibstoff.
* **RCS** (`ModuleRCS`) bekommt keinen Bonus, nur Haupttriebwerke.
* Im Sandbox-Save ist die Kerbal-Erfahrung deaktiviert; mit der Standardeinstellung
  `fullBonusWhenExperienceDisabled = True` geben Ingenieure dort den vollen Bonus.
* Mods, die dieselben Felder anfassen (RealFuels, Triebwerks-Upgrades), koennen
  kollidieren. Der Controller erkennt ueberschriebene Werte und setzt sie neu, meldet das
  aber nur im Log.
* Wenn ein anderer Mod `multIsp`/`multFlow` am selben Triebwerk veraendert, nachdem
  dieses Plugin es erstmals erfasst hat, kann es zu Konflikten kommen.

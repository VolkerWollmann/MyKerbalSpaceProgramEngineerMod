# EngineerFuelSaver

*[English version](README.md)*

KSP-1.12.5-Plugin: Ein Ingenieur an Bord senkt den Treibstoffverbrauch aller Triebwerke
und RCS-Duesen des Schiffs um **bis zu 5 %**, abhaengig vom Sterne-Level.

| Level des Ingenieurs | 0 | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|---|
| Ersparnis | 0 % | 2 % | 3 % | 4 % | 5 % | 5 % |

**Nota bene:** Im VAB kann der Ingenieur bei manchen Entwuerfen wie ein *Verlust* aussehen -
die zusaetzlichen 94 kg Besatzung kosten Delta-v und uebersteigen auf leichten Schiffen die
Ersparnis auf jedem Level. Ein Level-0-Ingenieur setzt dem ohnehin nichts entgegen.
Vergleiche Gleiches mit Gleichem: derselbe Sitz mit einem Nicht-Ingenieur besetzt, nicht ein
leerer Sitz. Der K.E.R. (Kerbal Engineer Redux) zeigt den Effekt, oder mach einen Testflug in
einen niedrigen Orbit mit und ohne Ingenieur und vergleiche das verbleibende Delta-v.

* Der Ingenieur muss **an den Hebeln** sitzen: in einer Kommandokapsel (Kapsel, Cockpit,
  Cupola) oder im externen Kommandositz. In der Mitfahrer-Kabine oder im Labor kann er
  nichts ausrichten und spart nichts (abschaltbar mit `requireCommandPod`).
* Es zaehlt nur der **beste** Ingenieur an Bord, mehrere Ingenieure stapeln sich nicht.
* Umgesetzt ueber den **spezifischen Impuls** - das Delta-v steigt entsprechend und alle
  Bordanzeigen bleiben konsistent.
* **RCS-Duesen** sparen genauso (abschaltbar mit `includeRcs`). Schub und Steuerkraft
  bleiben gleich, nur der Monotreibstoff haelt laenger.
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

`g0` ist dabei die Normfallbeschleunigung 9,80665 m/s^2 - nicht Kerbins
Oberflaechenschwerkraft und auch nicht das gerundete 9,81. KSP hat den Wert sowohl im
Triebwerk als auch in der RCS-Duese fest verdrahtet; er dient nur dazu, den in Sekunden
angegebenen Isp in eine Austrittsgeschwindigkeit umzurechnen, und ist deshalb auf jedem
Himmelskoerper derselbe. Fuer den Bonus kuerzt er sich ohnehin heraus: skaliert man Isp
und Durchfluss gegenlaeufig, bleibt das Produkt gleich, egal welcher Wert dort steht.

Bei Level 5 und einem Terrier mit 345 s Vakuum-Isp: Isp-Kurve x 1,0526 auf 363 s,
`maxFuelFlow` x 0,95.

### RCS rechnet anders, spart aber dasselbe

`ModuleRCS` (und damit auch `ModuleRCSFX`) fuehrt dieselben zwei Felder, kommt aber auf
anderem Weg zum Schub:

```
maxFuelFlow = thrusterPower / (Isp(Vakuum) x g0)   einmal beim Laden des Teils
exhaustVel  = Isp(Hoehe) x g0                      in jedem FixedUpdate neu
Verbrauch   = maxFuelFlow x Drossel,  Schub = Verbrauch x exhaustVel
```

Ein Mindestdurchfluss existiert dort nicht. Weil `exhaustVel` jeden Physikschritt frisch
aus `atmosphereCurve` kommt, wirkt die skalierte Kurve sofort; im Rechtsklick-Menue der
Duese steht der angehobene Wert unter `realISP`.

Die Rechnung geht genauso auf wie am Triebwerk. Bei 5 %:

| | RV-105 | Vernor |
|---|---|---|
| `thrusterPower` | 1 kN | 12 kN |
| Isp Vakuum | 240 -> 252,6 s | 260 -> 273,7 s |
| Isp Meereshoehe | 100 -> 105,3 s | 140 -> 147,4 s |
| `maxFuelFlow` | 0,000425 -> 0,000404 | 0,004706 -> 0,004471 |
| Schub im Vakuum | 1,000 kN | 12,000 kN |

Die Vernor-Duese laeuft auf LF/Ox, die RV-105 auf Monotreibstoff - massgeblich ist der
Treibstoff, nicht der Teiletyp: `excludedPropellants` gilt hier genau wie am Triebwerk.

### Warum nicht multIsp/multFlow

Der erste Ansatz benutzte die Multiplikatorfelder `multIsp` und `multFlow` (an der
RCS-Duese heissen sie `ispMult` und `flowMult`). Die werden zwar gesetzt, aber von keiner
Delta-v-Rechnung gelesen - weder von der Stock-Stufenanzeige noch von KER oder MechJeb.
Der Bonus war dadurch in jeder Anzeige unsichtbar. `atmosphereCurve` und `maxFuelFlow`
liest dagegen jede dieser Rechnungen.

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
  ThrusterTweaker.cs         setzt/entfernt die Werte, haelt die Originale (Basis)
  EngineTweaker.cs           die Felder des Triebwerks (ModuleEngines/FX)
  RcsTweaker.cs              die Felder der RCS-Duese (ModuleRCS/FX)
  FlightBonusController.cs   Durchlauf ueber alle geladenen Schiffe im Flug
  EditorBonusController.cs   Durchlauf ueber das Schiff im Bauhof (VAB/SPH)
  FuelSavingSkill.cs         Kerbal-Faehigkeit fuer den Infoblock (nur Anzeige)
  Log.cs                     Logging ins KSP.log
GameData/EngineerFuelSaver/
  EngineerFuelSaver.cfg      Konfiguration
  EngineerTrait.cfg          haengt die Faehigkeit an den Ingenieur
  EngineerFuelSaver.version  Versionsdatei fuer KSP-AVC
```

`FuelSavingSkill.cs` ist eine eigene Kerbal-Faehigkeit, damit die Ersparnis im Infoblock
des Kerbals steht - neben "Provides repair skills" und den uebrigen
Ingenieurs-Faehigkeiten. Rein informativ, gerechnet wird in `EngineTweaker` und
`RcsTweaker`.

KSP findet die Klasse per Reflection ueber die geladenen Assemblies
(`[ExperienceSystem]: Found N effect types`). Angehaengt wird sie ueber die mitgelieferte
`EngineerTrait.cfg`: Mehrere `EXPERIENCE_TRAIT`-Nodes mit demselben `name` fuehrt KSP
zusammen - denselben Weg benutzt die Serenity-Erweiterung, um dem Ingenieur den
`DeployedSciencePowerSkill` zu ergaenzen. **Kein ModuleManager noetig.**

Der Text steht in der cfg (`effectDescription`), Vorgabe `FuelSaving:<saving>`. Weil jeder
Kerbal eine eigene Instanz der Faehigkeit besitzt, zeigt `<saving>` dessen konkreten Wert:
Level 3 ergibt `FuelSaving:4%`. Das Level kommt aus `Parent.CrewMemberExperienceLevel()`.
Weitere Platzhalter: `<level>`, `<perLevel>`, `<maxSaving>`, `<maxLevel>`.

Alle Werte stammen aus `fuelSavingPerLevel` und `levelOffset` - Anzeige und Regel koennen
nicht auseinanderlaufen. Der Effekt fuehrt aus demselben Grund bewusst keine eigenen
`modifiers`. Spitze Klammern, nicht geschweifte: `{` und `}` sind im cfg-Format
Node-Klammern und schneiden den Wert samt aller folgenden Zeilen ab.

Steht `debugLevelOverride` auf einem Wert, zeigt der Text dieses Level statt des echten -
sonst widersprechen sich Anzeige und Wirkung waehrend eines Tests.

Die Beschreibung wird erst beim Anzeigen gebaut, deshalb laedt sie die cfg notfalls selbst
nach (`Settings.EnsureLoaded`) statt sich darauf zu verlassen, dass das Settings-Addon schon
lief. In der Praxis startet das Addon (MainMenu) vor der Initialisierung des
Experience-Systems - die Absicherung greift also selten, haelt `GetDescription()` aber
unabhaengig von der Szenen-Reihenfolge. Sprache frei waehlbar; Voreinstellung ist Englisch,
passend zu den Stock-Faehigkeiten daneben.

Im **Bauhof** liest der Mod die Besatzung aus `ShipConstruction.ShipManifest` - also genau
die Zuweisung aus dem Crew-Dialog - und wendet denselben Bonus an, damit die
Delta-v-Anzeige schon dort stimmt und nicht erst auf der Startrampe. Beide Controller
teilen sich dieselbe Logik in `ThrusterTweaker`.

Gezaehlt wird die Besatzung teilweise, nicht schiffsweit: `EngineerBonus.IsCommandPart`
laesst nur Teile durch, die einen Platz an den Hebeln bieten. Das sind Teile mit
`ModuleCommand` sowie der externe Kommandositz. Der Freisitz traegt kein `ModuleCommand` -
seine Steuerung kommt vom Kerbal selbst -, deshalb sind dort zwei weitere Module erlaubt,
je nachdem, wo das Spiel den Kerbal gerade fuehrt: im Bauhof am Sitz (`KerbalSeat`), im
Flug am Teil des Sitzenden (`KerbalEVA`), das am Sitz haengt und im Spielstand die Zeile
`crew = ...` traegt. Doppelt gezaehlt wird nichts: im Bauhof gibt es kein EVA-Teil, im Flug
ist der Sitz selbst leer. Ein frei schwebender Kerbal ist ein eigenes Schiff ohne
Triebwerke und kann deshalb keinem anderen Schiff einen Bonus verschaffen.

Der Controller rechnet alle `refreshInterval` Sekunden (Standard 0,5 s) komplett neu,
statt auf einzelne GameEvents zu hoeren. Docking, Crew-Transfer, Staging, EVA und
Level-Ups mitten im Flug sind damit ohne Sonderbehandlung abgedeckt. Beim Entladen eines
Schiffs und beim Verlassen der Flugszene werden die Originalwerte zurueckgeschrieben.

## Bauen

Vorausgesetzt wird KSP 1.12.5. Die Referenz-DLLs aus dem Ordner
`KSP_x64_Data/Managed` werden nur referenziert, nie mitausgeliefert.

```
dotnet build src/EngineerFuelSaver/EngineerFuelSaver.csproj -c Release
```

Der Build kopiert DLL und cfg anschliessend automatisch nach
`<KSPRoot>/GameData/EngineerFuelSaver/`. Ohne dieses Deployment:

```
dotnet build src/EngineerFuelSaver/EngineerFuelSaver.csproj -c Release -p:SkipDeploy=true
```

`KSPRoot` zeigt als Vorgabe auf den ueblichen Steam-Pfad. Steht KSP woanders, im
Wurzelverzeichnis eine `Directory.Build.props` anlegen. Die ist nicht versioniert, der
Pfad bleibt also auf deiner Maschine und landet nie im Repo:

```xml
<Project>
  <PropertyGroup>
    <KSPRoot>D:\Spiele\KSP</KSPRoot>
  </PropertyGroup>
</Project>
```

Fuer einen einzelnen Build schlaegt die Kommandozeile beides:

```
dotnet build src/EngineerFuelSaver/EngineerFuelSaver.csproj -c Release -p:KSPRoot="D:\Spiele\KSP"
```

## Konfiguration

Alle Werte in `GameData/EngineerFuelSaver/EngineerFuelSaver.cfg`:

| Schluessel | Standard | Bedeutung |
|---|---|---|
| `fuelSavingPerLevel` | `0.01` | Ersparnis je Level |
| `maxLevel` | `5` | hoechstes gewertetes Level |
| `levelOffset` | `1` | Level, die vor der Deckelung zusaetzlich zaehlen; `0` = 1 % je Stern |
| `maxFuelSaving` | `0.9` | harte Obergrenze |
| `refreshInterval` | `0.5` | Sekunden zwischen zwei Neuberechnungen |
| `includeRcs` | `True` | RCS-Duesen bekommen denselben Bonus |
| `excludedPropellants` | `SolidFuel` | Treibstoffe, die keinen Bonus bekommen |
| `effectDescription` | englischer Satz | Text der Faehigkeit im Kerbal-Infoblock |
| `requireCommandPod` | `True` | nur Ingenieure in Kommandokapsel oder Freisitz zaehlen |
| `fullBonusWhenExperienceDisabled` | `True` | Verhalten ohne Erfahrungssystem (Sandbox) |
| `debugLog` | `False` | Ausgabe je Schiff, Triebwerk und RCS-Duese ins KSP.log |
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
  `maxFuelFlow` 0,078947 mal 0,95 auf 0,075. Schubprobe: 0,075 x 326,3 x 9,80665 =
  240,0 kN, also unveraendert gegenueber `maxThrust = 240`.
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
  danach Level 1 und 2 % Ersparnis. Ohne Testschalter.
* Die Delta-v-Anzeige im Bauhof zeigt den Bonus.

Damit ist die Kette vom Kerbal bis zur Anzeige vollstaendig belegt.

### Zahlenmaessig belegt

Gegenprobe im Bauhof mit `debugLevelOverride = 5`, Rakete mit zwei Mk-55 "Thud",
33.881 kg, Kerbin auf Meereshoehe:

| | ohne Bonus | mit 5 % |
|---|---|---|
| Delta-v der Thud-Stufe | 2.840 m/s | 2.990 m/s |
| Isp Vakuum | 305,0 s | 321,1 s |
| Isp Meereshoehe | 275,0 s | 289,5 s |
| `maxFuelFlow` | 0,04012 | 0,03811 |
| TWR | 1,34 | 1,34 |

Soll waere Isp mal 1/0,95 = 1,0526 und Durchfluss mal 0,95, Schub und TWR unveraendert.
Alle Werte treffen das. Die Schubprobe geht auf: 0,04012 x 275,0 = 0,03811 x 289,5.
Stock-Stufenanzeige und Kerbal Engineer Redux zeigen denselben Wert, die
Feststoffbooster bleiben wie vorgesehen unberuehrt.

Im Flug gegengeprueft, noch mit der alten 1-%-Kurve: Aufstieg auf knapp 100 km, Ingenieur
gegen Wissenschaftler auf demselben Sitz, 1.140 gegen 1.120 m/s Restreserve. Erwartet
waren rund 30 m/s - die Differenz liegt in der Streuung zweier Aufstiege.

Im KSP.log steht je Triebwerk die gesetzte Isp-Kurve, und waehrend eines Brennvorgangs
die vom Spiel selbst gerechneten Werte (`realIsp`, Schub, Durchfluss). Der `realIsp` ist
der entscheidende Messwert: er stammt aus KSPs eigener Rechnung, nicht aus unserer.

### Zwei Fluege im Vergleich: Pilot gegen Ingenieur

Dasselbe Schiff, dieselbe Aufstiegsbahn, nur die Besatzung getauscht - einmal Jebediah
(Pilot Level 1), einmal Bill (Engineer Level 1, also 2 %):

```
MunTourist1: bester Ingenieur Level 0 -> 0.0 % ... Jebediah Kerman (Pilot Level 1)
MunTourist1: bester Ingenieur Level 1 -> 2.0 % ... Bill Kerman (Engineer Level 1)
```

Gesetzte Werte im Ingenieurs-Flug, Faktor 1 / 0,98 = 1,0204:

| Modul | `maxFuelFlow` | Isp Vakuum | Isp Meereshoehe |
|---|---|---|---|
| Bobcat LV-TX87 | 0,13158 -> 0,12894 | 310,0 -> 316,3 s | 290,0 -> 295,9 s |
| Skipper RE-I5 | 0,20713 -> 0,20299 | 320,0 -> 326,5 s | 280,0 -> 285,7 s |
| RCS Place-Anywhere 7 | 0,00085 -> 0,00083 | 240,0 -> 244,9 s | 100,0 -> 102,0 s |
| Kickback SRB | 0,31055 unveraendert | 220,0 s | 195,0 s |

Und das, was KSP waehrend des Brennvorgangs selbst rechnet, bei identischer Drossel:

| Drossel 100 % | Pilot | Ingenieur | Verhaeltnis |
|---|---|---|---|
| Bobcat `realIsp` | 310,0 s | 316,3 s | 1,0203 |
| Bobcat Schub | 400,0 kN | 400,0 kN | **unveraendert** |
| Bobcat Durchfluss | 26,3153 | 25,7890 | **0,98000** |
| Skipper `realIsp` | 320,0 s | 326,5 s | 1,0203 |
| Skipper Schub | 650,0 kN | 650,0 kN | **unveraendert** |
| Skipper Durchfluss | 41,4260 | 40,5975 | **0,98000** |

Genau das war das Ziel: gleicher Schub, exakt 2 % weniger Durchfluss. Der Kickback zeigt
in beiden Fluegen denselben Spitzendurchfluss 41,4067 - die `SolidFuel`-Ausnahme greift
mitten im Ingenieurs-Flug, waehrend alle anderen Module auf Level 1 stehen.

Fuer **RCS** belegt KSPs eigener `realISP` die angehobene Kurve, ueber 1.260 Feuer-Zeilen
sauber getrennt zwischen den Fluegen:

| | Pilot | Ingenieur |
|---|---|---|
| `realISP` im Vakuum | 240,0 s | 244,9 s |
| `realISP` tief in der Atmosphaere | 101,0 s | 103,1 s |

240,0 x 1,0204 = 244,90. Ein **Schub-Paar wie bei den Triebwerken gibt es fuer RCS nicht**:
der Schub einer Duese haengt am momentanen Steuerausschlag, und der ist zwischen zwei
Fluegen nie derselbe. Die beobachteten Spitzen (1,58 gegen 1,61 kN bei 2,0 kN Nennschub)
sind beide Teilausschlaege und taugen nicht als Gegenprobe. Dass der Schub unveraendert
bleibt, folgt hier aus den beiden einzeln nachgemessenen Feldern - `maxFuelFlow` mal 0,98
und `realISP` mal 1,0204 -, nicht aus einer direkten Schubmessung.

Keine Exceptions im Log, Assembly geladen als `EngineerFuelSaver, Version=1.3.0.0`.

## Bekannte Einschraenkungen

* **Nur geladene Schiffe.** Ungeladene Schiffe ausserhalb des Ladebereichs verbrauchen
  in Stock ohnehin keinen Treibstoff.
* Beim **RCS** ist die angehobene Isp-Kurve im Flug nachgemessen (`realISP` 240,0 -> 244,9 s
  im Vakuum), der **unveraenderte Schub dagegen nicht direkt** - dafuer muesste in beiden
  Fluegen derselbe Steuerausschlag anliegen, was sich nicht herstellen laesst. Er folgt aus
  den beiden einzeln belegten Feldern.
* Die Regel `requireCommandPod` ist **im Spiel noch nicht nachgemessen** - Kompilat gegen
  KSP 1.12.5 und Modulnamen sind geprueft, ein Testflug mit Ingenieur in Mitfahrer-Kabine
  bzw. Freisitz steht aus.
* Im Sandbox-Save ist die Kerbal-Erfahrung deaktiviert; mit der Standardeinstellung
  `fullBonusWhenExperienceDisabled = True` geben Ingenieure dort den vollen Bonus.
* Mods, die dieselben Felder anfassen (RealFuels, Triebwerks-Upgrades), koennen
  kollidieren. Der Controller erkennt ueberschriebene Werte und setzt sie neu, meldet das
  aber nur im Log.
* Wenn ein anderer Mod `multIsp`/`multFlow` am selben Triebwerk veraendert, nachdem
  dieses Plugin es erstmals erfasst hat, kann es zu Konflikten kommen.
* RCS geht in die **Stock-Delta-v-Anzeige** nicht ein - dort taucht der Bonus fuer die
  Duesen also nicht auf, auch wenn er wirkt. Der `realISP` im Rechtsklick-Menue der Duese
  zeigt ihn.

## Versionen

Die Nummer steht in `GameData/EngineerFuelSaver/EngineerFuelSaver.version` (KSP-AVC) und
in der csproj, jede Veroeffentlichung traegt ausserdem ein Git-Tag `vX.Y.Z`.

* **1.3.0** - RCS-Duesen (`ModuleRCS`, damit auch `ModuleRCSFX`) bekommen denselben Bonus
  wie die Haupttriebwerke, abschaltbar mit `includeRcs`. Damit faellt die bisherige
  Einschraenkung weg, dass nur Haupttriebwerke sparen; die Vernor-Duese spart jetzt also
  auch. Im Spiel gegengeprueft mit zwei Fluegen, Pilot gegen Ingenieur.
* **1.2.0** - Der Bonus gilt nur noch fuer Ingenieure an den Hebeln: Kommandokapsel oder
  externer Kommandositz, abschaltbar mit `requireCommandPod`. Im Spiel noch nicht
  nachgemessen. Dazu die Klarstellung, dass ueber den Bonus der Modultyp entscheidet und
  nicht der Treibstoff - die O-10 "Puff" bekommt ihn, die Vernor-Duese nicht.
* **1.1.0** - Bonuskurve mit einem Level Zuschlag vor der Deckelung (`levelOffset`): ein
  Level-1-Ingenieur spart so viel wie zuvor ein Level-2-Ingenieur.
* **1.0.0** - Erste Fassung.

## Lizenz

MIT, siehe [LICENSE](LICENSE). Copyright (c) 2026 Volker Wollmann.

Benutzen, aendern und weitergeben ist erlaubt - Copyright-Hinweis und Lizenztext muessen
dabei erhalten bleiben.

Die KSP-Assemblies aus `KSP_x64_Data/Managed` werden nur zum Uebersetzen referenziert und
sind nicht Bestandteil dieses Repositorys. Sie duerfen nicht mitausgeliefert werden.

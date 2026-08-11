# #thinTiny.pixaki` — Qualitätsprüfung

Prüfung der handgepixelten Vollausbau-Variante von thinTiny (alle thinSmall-Glyphen), `thinTiny.pixaki`.

| | |
|---|---|
| **Geprüfter Stand** | Dateidatum 2026-08-11 19:15 (36 350 Bytes) — *Revision 12* |
| Vorstände | 19:12 = Rev. 11 · 19:01 = Rev. 10 · 18:56 = Rev. 9 · 18:32 = Rev. 8 · 18:26 = Rev. 7 · 18:20 = Rev. 6 · 18:09 = Rev. 5 · 17:37 = Rev. 4 · 17:21 = Rev. 3 · 15:37 = Rev. 2 · 2026-08-10 = Rev. 1 |
| **Fazit** | **fertig — kein offener Punkt.** Kein Defekt; die Grundsatzfrage aus § 8 ist entschieden: **Vollersatz** |

**Fazit:** Die Font ist **defektfrei**. Abdeckung vollständig (331/331 Codepoints), Metrik
durchgehend konsistent, keine Regression über zwölf Revisionen. Jeder in diesem Dokument
festgehaltene Befund ist entweder **behoben** oder als **bewusste, begründete Ausnahme**
dokumentiert.

Bilanz über alle Revisionen: von den 26 in Revision 1 gefundenen Befunden sind **20 behoben**
und **6 als formbedingte Ausnahme bestätigt** (§ 4.2). Achtzehn weitere Punkte kamen über die
Revisionen 2–12 hinzu und sind **alle behoben** — `Ģ`, `ё`, der `Р`-Nebeneffekt, `ġ`, `ć`, `ğ`,
`Ű`/`ű`, die `М`-Form, `и й И Й`, `ş`, der Fremdfarb-Pixel in `m`, die
Cedille/Komma-Unterscheidung (4 Zeichen), `ê`, `Ą` und `Ŕ`.

**Alle Prüfungen grün** (§ 10): keine Formkollision über alle 331 Codepoints, alle **neun
Akzenttypen oben** in sich einheitlich, die **drei Diakritika unten** klar voneinander getrennt
(§ 4.6), alle 44 Akzent/Basis-Paare konsistent, 336 von 337 Zellen ohne rechte Lücke (die 337.
ist CK-konform), ein einziger Weißton, keine Layer-Fremdfarbe, kein Pixel außerhalb seiner
Rect-Box, kein Pixel mit Alpha < 255.

**Die Grundsatzfrage aus § 8 ist entschieden: Vollersatz** — alle 331 Glyphen werden in
thinTiny injiziert. Damit werden CK's Schadens- und Punktzahlen 1 px flacher; dafür ist die
Font in sich stimmig, und die schmalen Buchstaben (`C E F L`) werden lesbarer als im Original.
Nächste Schritte sind die drei Anpassungen an `pixaki_to_glyphs.py` (§ 9).

---

## 1. Prüfstand auf einen Blick

| Klasse | Umfang (Rev. 1) | behoben | offen |
|---|---|---|---|
| **A** — Extraktionsdefekte (falsche Pixel nach Zuschnitt) | 12 Zellen | **12** + der `Ģ`-Folgepunkt | 0 |
| **B** — Höhenausreißer (1 px) | 8 Zellen | 2 | 0 — die übrigen 6 sind formbedingt nicht reduzierbar und als Ausnahme bestätigt (§ 4.2) |
| **C** — Formkollisionen | 6 Zellen | **6** (`š` Rev. 4 · `Ш ш`, `Ő ő` Rev. 5 · `m` Rev. 7) | **0** |
| **D** — Einzelbefunde (Abweichung von Grundform/Akzentgruppe) | 7 | **7** (`ё`, `ć`, `ğ`, `Ű ű`, `ê`, `Ą`, `Ŕ`) | 0 |
| **F** — Diakritika unten nicht differenziert | 4 Zeichen | **4** (Rev. 9–11) | 0 — siehe § 4.6 |
| **§ 7** — rechte Lücken | 6 Zeichen | **5** (Rev. 7) | 0 — der sechste (`×`) ist CK-konform |
| **E** — Fremdfarbe im Atlas | 1 | **1** (Rev. 8) | 0 |

Zusätzlich haben die Revisionen 2–7 sechs Dinge geändert, die **nicht** auf der Defektliste
standen (§ 5): die Neuausrichtung von Zeile 0, zwei Schritte an `Р`, die Punktposition von
`ġ`, die Löschung der vier Waisen-Rects (§ 6), die Verbreiterung der М/Ш/Щ-Familie und der
lateinischen `M`/`m` (§ 5.4, § 5.5).

---

## 2. Was in der Datei liegt

| Eigenschaft | Wert |
|---|---|
| Canvas | **257 × 144** — die Größe des *thinSmall*-Atlas (`rrsthin8`), **nicht** die von thinTiny (`rrs5`, 256 × 40) |
| Zellraster | 8 × 12 (thinSmall-`charDims`), 32 Spalten × 12 Zeilen = 384 Zellen |
| Bemalte Zellen | **337** — in *jeder* Zeile deckungsgleich mit der thinSmall-Referenz |
| Rect-Boxen | **337** — genau eine je bemalter Zelle (Rev. 1/2: 341, mit 4 Waisen-Slots, § 6) |
| Rect-Geometrie | **(y = 1, h = 10) bei allen 337**, x-Offset 0 bei allen — seit Rev. 2 vollständig einheitlich |
| Layer (Rev. 3) | `Background`, `Dims`, `Rects`, `Atlas`, `Layer 1` (156 px Hilfsstreifen, jetzt sichtbar) |
| Layer (Rev. 1) | zusätzlich ein unsichtbarer `thinSmall`-Referenzlayer und ein leerer `thinTiny`-Layer |

Die Glyphen sind im **thinSmall-Zellraster (8 × 12)** gezeichnet, tragen aber
**thinTiny-Metrik** (Box 8 × 10). Diese Kombination ist unproblematisch: die Zeilenteilung
des Quelldokuments spielt bei der Extraktion keine Rolle, nur die `Rects`-Box zählt.

> **Hinweis zur Referenz:** Seit Revision 2 enthält die Datei den `thinSmall`-Referenzlayer
> nicht mehr.
> Vergleiche in diesem Dokument laufen daher gegen das ausgelieferte CK-Atlas
> `rrsthin8_raw.png` bzw. gegen die aus Revision 1 extrahierte Kopie.

### Koordinatenkonvention in diesem Dokument

`cell(Spalte, Zeile)` — Spalte 0…31 von links nach rechts, Zeile 0…11 von **oben** nach
unten. Zelle → Pixelbereich: `x = Spalte × 8`, `y = Zeile × 12`. Pixelpositionen innerhalb
einer Zelle sind `(x, y)`, y von der Zellenoberkante nach unten. Die Rect-Box liegt auf
y = 1…10.

> **Der `Rects`-Layer trägt zwei Bedeutungen gleichzeitig.** `pixaki_to_glyphs.py` nutzt
> seine Bounding-Box sowohl als **Sprite-Ausschnitt** aus dem Atlas als auch als
> **Advance-Width** (`gd.rect.width`). Jeder Pixel außerhalb dieser Box ist deshalb nicht
> nur verrutscht — er wird bei der Extraktion abgeschnitten.

---

## 3. Was stimmt

| Prüfung | Ergebnis (Rev. 12) |
|---|---|
| Codepoint-Abdeckung | **331 / 331**, kein Zeichen fehlt (thinTiny hatte 114) |
| Atlas-Belegung vs. thinSmall | 337 = 337, zeilenweise übereinstimmend |
| Pixel außerhalb der Rect-Box | **0** (Rev. 1: 11) |
| Pixel mit Alpha < 255 | **0** (Rev. 1: 4) |
| Baseline | 258/337 mit 2 px Luft unten; jede Abweichung semantisch korrekt — Unterlängen `g j p q y Q` (40× Luft 1), Cedillen und Kommas `Ç ç Ş ş Ą Ų ų Ķ ķ Ļ ļ Ņ ņ Ț ț Ģ` (16× Luft 0), Hochzeichen `" ' ^ – “ ”` (Luft 3–6) |
| Advance-Konvention | Gap 0 bei 328/337, Gap 1 bei 9 — wie thinSmall (327/331) |
| Ziffern-Advance | `0`–`9` durchgehend **3**, identisch zur echten thinTiny → CK's Zahlen-Layout bleibt unverändert |
| Höhenklassen (lateinisch) | Versalien top = 4 (**25/25**), x-Höhe top = 5 (**13/13**), Oberlängen top = 4 (**7/7**) |
| Höhenklassen (kyrillisch) | Versalien top = 4 (29/31), x-Höhe top = 5 (25/29) — die 6 Abweichungen sind formbedingt gewollt und **keine** Defekte (§ 4.2) |

---

## 4. Befunde und ihr Status

**Alle fünf Klassen sind abgeschlossen.** Die behobenen Punkte bleiben mit ihrem
Vorher/Nachher dokumentiert — sie erklären, warum die Font heute so aussieht, und bewahren die
Begründungen für künftige Änderungen. Klasse B ist der einzige Abschnitt, der bewusst
*nicht*-behobene Fälle enthält: dort sind sechs Höhenabweichungen als formbedingt notwendig
bestätigt (§ 4.2).

### 4.1 Klasse A — vollständig erledigt

Behoben in Revision 2:

| Zelle | Zeichen | Was gemacht wurde |
|---|---|---|
| (12, 1) | `,` U+002C | beide Streupixel (2,11) + (3,11) gelöscht |
| (24, 4) | `Û` U+00DB | Streupixel (0,11) gelöscht |
| (8, 7) | `×` U+00D7 | die vier halbtransparenten Eckpixel gelöscht — **siehe Korrektur unten** |
| (4, 11) | `Ķ` U+0136 | Komma um 1 px nach oben *und* links verschoben → (1,9) + (0,10) |
| (5, 11) | `ķ` U+0137 | dito |
| (8, 11) | `Ļ` U+013B | dito |
| (9, 11) | `ļ` U+013C | dito |
| (12, 11) | `Ņ` U+0145 | dito |
| (13, 11) | `ņ` U+0146 | dito |
| (20, 11) | `Ț` U+021A | dito |
| (21, 11) | `ț` U+021B | dito |

Die acht verschobenen Kommas liegen jetzt **pixelgenau auf derselben Position wie die
Cedille von `Ç` (6,4) und `ç` (6,5)** — die Familie ist damit vereinheitlicht.

> **Korrektur zu `×` (8,7).** Revision 1 dieses Dokuments behauptete, die vier Eckpixel
> hätten „Alpha 3 statt 255" und das Zeichen rendere „als kleines Kreuz ohne Arme,
> verstümmelt". **Das war falsch.** Der Vergleich gegen das ausgelieferte CK-Atlas
> `rrsthin8_raw.png` zeigt dort dieselben Werte (`alpha = 3` an genau diesen vier
> Positionen). Das `×` sah in Vanilla immer so aus wie jetzt; die halbtransparenten Pixel
> waren ein Artefakt aus CK's eigenem Asset, das beim Nachzeichnen mitkopiert wurde. Das
> Löschen in Revision 2 ist reine Hygiene und optisch folgenlos. Ursache des Fehlschlusses:
> der Vergleichsdump zeichnete jedes `alpha > 0` als vollen Pixel und verdeckte damit das
> Artefakt im Original.

**Folgepunkt `Ģ` (23,10) — in Revision 3 behoben.** In Revision 2 war bei den acht
Geschwistern das Komma *verschoben*, bei `Ģ` aber nur der überstehende Pixel *gelöscht*
worden, sodass `Ģ` eine einpixlige Cedille an abweichender Position trug:

```
Rev. 2:  Ķ ķ Ļ ļ Ņ ņ Ț ț  und  Ç ç      y9: .#..   y10: #...    zweipixliges Komma
         Ģ                              y9: ....   y10: ..#.    ein einzelner Punkt, rechts

Rev. 3:  Ģ                              y9: .#..   y10: #...    identisch mit Ç, Ķ ✓
```

Damit trägt die ganze Familie dieselbe Cedille an derselben Position.

### 4.2 Klasse B — 2 behoben, 6 als formbedingte Ausnahme bestätigt

Ausgangsbefund von Revision 1: acht Zeichen behalten ihre ursprüngliche thinSmall-Höhe,
während ihre gesamte Höhenklasse um 1 px komprimiert wurde. Zwei davon (`ı`, `к`) waren
tatsächlich Versehen und sind korrigiert. Für die restlichen sechs hat der Autor entschieden,
die Abweichung zu **behalten**: eine weitere Kompression würde den Charakter des Zeichens
zerstören. Die Formanalyse stützt das — Revision 1 hatte die Abweichung zu Unrecht als
"objektiver Ausreißer, keine Stilentscheidung" eingeordnet. Messbar ist die Abweichung,
die Schlussfolgerung "also ein Versehen" war eine Annahme.

| Zelle | Zeichen | Höhe | Klassennorm | Status |
|---|---|---|---|---|
| (3, 6) | `ı` U+0131 | y5–8 | y5–8 | **✓ behoben** (war y4–8) |
| (11, 8) | `к` U+043A | y5–8 | y5–8 | **✓ behoben** (war y4–8) |
| (1, 8) | `а` U+0430 | y4–8 | y5–8 | **gewollt** — Abgrenzung zum lateinischen `a` |
| (23, 8) | `в` U+0432 | y4–8 | y5–8 | **gewollt** — zwei gestapelte Bäuche |
| (26, 8) | `з` U+0437 | y4–8 | y5–8 | **gewollt** — Doppelkurve mit Mitteleinzug |
| (28, 8) | `э` U+044D | y4–8 | y5–8 | **gewollt** — dito, gespiegelt |
| (26, 9) | `З` U+0417 | y3–8 | y4–8 | **gewollt** — Rundung + diagonaler Einzug |
| (28, 9) | `Э` U+042D | y3–8 | y4–8 | **gewollt** — dito, gespiegelt |

**Warum die sechs nicht weiter gehen — je Form.** Der begrenzende Faktor ist durchweg die
**vertikale** Gliederung, nicht die Breite: jede dieser Formen zerfällt in Zonen, die je zwei
Zeilen brauchen, plus mindestens eine Trennzeile dazwischen. Platz in der Breite ist in allen
sechs Fällen vorhanden.

- **`в`** trägt zwei gestapelte Bäuche, die sich bei y6 eine gemeinsame Mittellinie teilen:
  2 + 1 + 2 = 5 Zeilen. Bei 4 Zeilen verschmelzen sie oder einer schrumpft auf 1 px.
- **`з` / `э`** sind Doppelkurven mit Einschnürung in der Mitte: obere Kurve 2 + Mitte 1 +
  untere Kurve 2 = 5 Zeilen. Der Mitteleinzug ist das identifizierende Merkmal.
- **`а`** hält die Unterscheidung zum lateinischen `a` aufrecht, die CK selbst macht: `a`/`а`
  gehört **nicht** zu den 23 Zeichengruppen, die thinSmall identisch führt (§ 10). Dort sind
  beide gleich hoch (y4–8) und trennen sich über die Form — das kyrillische `а` trägt einen
  Bogen oben rechts über einem Querbalken, das lateinische einen geschlossenen Ring. In der
  kompakten Fassung ist `a` auf vier Zeilen gebracht; `а` braucht die fünfte, sonst
  verschwindet die Unterscheidung. Das gilt auch für gewöhnliche Schriften und ist damit eine
  bewusste Entscheidung, die dem Original folgt — nicht eine Nachlässigkeit bei der
  Kompression.
- **`З` / `Э`** gliedern sich in drei Zonen: obere Rundung (y3–4), Einschnürung (y5–6),
  untere Rundung (y7–8) — sechs Zeilen. Jede Rundung braucht zwei Zeilen, und der Einzug
  läuft bei `З` selbst über zwei (y5 auf x2, y6 auf x3). Auf fünf Zeilen müsste eine der drei
  Zonen auf eine einzige Zeile fallen.

**Gegenprobe:** Die 25 kyrillischen Kleinbuchstaben, die auf 4 px x-Höhe passen, sind
durchweg einfacher gebaut — `о` ist ein reiner Ring, `е с г п н х` haben eine einzelne Kurve
oder gerade Stämme. Die sechs Ausnahmen sind also nicht nachlässig komprimiert, sondern
stoßen an die Grenze der Rasterauflösung.

**Korrekt und nie betroffen:** `б` (2,8) mit y4–8 und `ф` (6,8) mit y4–9 — die tragen echte
Ober- bzw. Unterlängen und *sind* sauber von y3 herunterkomprimiert.

**Optischer Preis:** In einem kyrillischen Wort stehen `а в з э` 1 px höher als `о е с`.
Das ist wahrnehmbar, aber geringer als der Preis eines Formverlusts.

> **Für künftige Prüfungen:** Diese sechs Zellen sind dokumentierte Ausnahmen und dürfen
> nicht erneut als Defekt gemeldet werden. Eine Höhenklassen-Prüfung sollte sie als
> Whitelist führen.

### 4.3 Klasse C — vollständig erledigt

Hier ist ein Zeichen **pixelidentisch** mit einem anderen, das thinSmall unterscheidet. Die
rechte Spalte begründet, warum jeweils *diese* Zelle des Paares die falsche ist. Die
Advance-Werte sind die der neuen Font.

| Zelle | Zeichen | Advance | Defekt | Warum diese Zelle die falsche ist |
|---|---|---|---|---|
| ~~(27, 9)~~ | ~~`Ш` U+0428~~ | 3→**5** | **in Rev. 5 behoben** — war pixelgleich mit `W` (23,2) | `Ж` (22,9) und `Ф` (6,9) hatten ihre 5 px behalten, `Щ` 4, `Ю`/`Ы` 5 — nur `Ш`/`ш` waren auf 3 gefallen |
| ~~(27, 8)~~ | ~~`ш` U+0448~~ | 3→**5** | **in Rev. 5 behoben** — war pixelgleich mit `м` (13,8) | dito |
| ~~(13, 3)~~ | ~~`m` U+006D~~ | 3→**5** | **in Rev. 7 behoben** — war pixelgleich mit `н` (14,8) | die alte Form (zwei Stämme + mittiger Querbalken) **war** ein korrektes `н`; ein `m` braucht drei Beine. Rev. 7 hat `m` **und** `M` auf 5 px gebracht |
| ~~(14, 11)~~ | ~~`Ő` U+0150~~ | 4→**5** | **in Rev. 5 behoben** — Akzent war eine versetzte Tilde und damit gleich `Õ` (19,4) | der ungarische Doppelakut braucht zwei parallele Striche; das Zeichen wurde dafür auf 5 px verbreitert |
| ~~(15, 11)~~ | ~~`ő` U+0151~~ | 4→**5** | **in Rev. 5 behoben** — dito, war gleich `õ` (19,5) | dito |
| ~~(19, 6)~~ | ~~`š` U+0161~~ | 3 | **in Rev. 4 behoben** — trug die Versalform von `S` und war damit gleich `Š` (22,6) | am **Körper** gemessen (nicht an der obersten Pixelzeile — dort sitzt der Hatschek): `s` (19,3) hat y5–8, `S` (19,2) hat y4–8, und `š` hatte **wie** `Š` y4–8. `Š` war also korrekt, `š` der Ausreißer |

> **Korrektur zu `š`/`Š` (Revision 3).** Die Revisionen 2/2b dieses Dokuments führten hier
> `Š` (22,6) als den fehlerhaften Partner und begründeten das mit „steht auf x-Höhe, genau wie
> `š`". **Das war umgekehrt.** Ursache des Fehlschlusses: als Kennzahl diente `top`, die
> oberste Pixelzeile — bei einem Akzentzeichen ist das aber der **Akzent**, nicht der Körper.
> Beide haben `top = 1` (Hatschek), woraus „beide auf x-Höhe" geschlossen wurde. Am Körper
> gemessen liegen beide auf **Versalhöhe** (y4–8). Damit ist `Š` richtig und `š` der Ausreißer.

**`š` (19,6) ist in Revision 4 behoben:** Es trägt jetzt die `s`-Kleinform auf y5–8,
pixelidentisch zur Basis `s`, mit dreipixligem Hatschek auf y2–3 — genau wie `č ž ě ň ř`.
`Š` blieb unangetastet und ist damit sauber abgesetzt. Das bestätigt die Richtung der
korrigierten Diagnose: `Š` war nie das Problem.

**Revision 5 hat vier davon behoben** — und zwar so, wie CK es selbst macht: `Ш` und `ш`
tragen jetzt bei Advance 5 die **pixelidentische CK-Form** (`#.#.#`×4 über `#####`), `Ő`/`ő`
den echten Doppelakut (`.#..#`/`#..#.`) bei Advance 5.

**Revision 7 hat den letzten Fall gelöst:** `m` (13,3) und `M` (13,2) sind auf Advance 5 mit
drei Stämmen umgestellt (`####` über `#.#.#`), `н`/`Н` bleiben bei 3 px in H-Form. Damit
**enthält die Font keine Formkollision mehr** — der Test über alle 331 Codepoints meldet
`OK — keine Kollision`. Im Satz sind `MMM`/`mmm` und `HHH`/`nnn` klar getrennt.

### 4.4 Einzelbefunde: Abweichungen von der eigenen Grundform

Diese Klasse entsteht aus einem eigenen Prüfkriterium (§ 10): ein akzentuiertes Zeichen muss
seine unakzentuierte Grundform unverändert tragen. Weicht es ab, während seine Geschwister
konsistent sind, ist das die Signatur eines Versehens — und anders als beim Vergleich gegen
das Original lässt sich das von einer Formentscheidung trennen.

**`ё` (8,10) — in Revision 3 behoben.** Es fehlte der linke untere Eckpixel (0,8):

| Zeichen | Zelle | y8 (Rev. 2) | y8 (Rev. 3) |
|---|---|---|---|
| `e` / `е` | (5,3) / (5,8) | `###` | `###` — Grundform, beide pixelidentisch |
| `ë` | (10,5) | `###` | `###` |
| `ё` | (8,10) | `.##` ← (0,8) fehlte | `###` ✓ |

Die Familie ist geschlossen (`e` ≡ `е`, `E` ≡ `Е`, `Ë` ≡ `Ё`; auch CK führt `ë`/`ё`
identisch), und der Unterschied bestand aus genau diesem einen Pixel.

**`ć` (16,6) — in Revision 4 behoben.** Es war als Ganzes 1 px zu hoch, doppelt belegt an zwei
unabhängigen Vergleichsgruppen:

```
Rev. 3:  c   č   ċ   ç         Körper ab y5      ć   Körper ab y4
         ń ś ź ý á é ó ú       Akut auf y2–3     ć   Akut auf y1–2

Rev. 4:  ć   Körper y5–8 (pixelidentisch zu c)   Akut auf y2–3  ✓
```

Der `c`-Körper war um eine Zeile gestreckt (drei statt zwei Stammzeilen), weshalb der Akut
mitwandern musste. Revision 4 hat das Zeichen komplett neu gesetzt.

**`ğ` (2,6) — in Revision 4 behoben.** Es hatte einen Pixel zu viel in der Unterlänge:
`g` (7,3) und `ġ` (22,10) haben y9 = `.##`, `ğ` hatte `###`. Der Pixel (0,9) ist gelöscht.
Dieselbe Signatur wie beim `ё`-Fall — eine Ein-Pixel-Abweichung gegen zwei konsistente
Geschwister.

**`Ű` (11,10) und `ű` (12,10) — in Revision 6 behoben.** Sie trugen als letzte
Doppelakut-Zeichen noch die **Tilde-Form**, obwohl `Ő`/`ő` in Revision 5 gerade umgestellt
worden waren. Gefunden über einen **Akzent-Typ-Vergleich** (§ 10): normalisiert man die
Akzentform jedes Zeichens und gruppiert sie je Typ, muss pro Typ genau eine Form
herauskommen. Ergebnis vor der Behebung:

```
Doppelakut   .#..#/#..#.   ő Ő      <- korrekt
             .#.#/#.#.     ű Ű      <- das ist die Tilde-Form
```

Alle anderen acht Akzenttypen (Akut, Gravis, Zirkumflex, Trema, Tilde, Hatschek, Punkt oben,
Breve) waren bereits in sich einheitlich. Revision 6 hat `Ű`/`ű` auf den Doppelakut und
Advance 5 gebracht; die Familie ist damit geschlossen.

> **Warum kein früherer Test das fand:** Der Kollisionstest braucht ein Partnerzeichen, das
> gleich aussieht. `Ő` fiel auf, weil `Õ` existiert und kollidierte. `Ű` fiel nicht auf, weil
> `Ũ` (U mit Tilde) im CK-Zeichensatz überhaupt nicht vorkommt — der falsche Akzent hatte
> niemanden, mit dem er kollidieren konnte. Dieselbe Lücke wie bei `š` und `ć`: ein Zeichen,
> dem die Behandlung fehlt, die sein Geschwister schon erhalten hat.

**Drei Kollateralfehler aus den Cedille-Runden — je ein Pixel, alle behoben.** Sie entstanden
beim Bearbeiten benachbarter Zellen und folgen genau dem Muster der vorigen Befunde: ein
Zeichen fällt aus seiner Akzentgruppe:

| Zeichen | Zelle | Fehler | gefunden durch | behoben |
|---|---|---|---|---|
| `ê` | (9,5) | Zirkumflex `.#.`/`###` statt `.#.`/`#.#` — Pixel (1,3) zu viel; die neun anderen Zirkumflex-Zeichen sind einheitlich | Akzent-Typ-Check | Rev. 10 |
| `Ą` | (7,6) | Ogonek als Diagonale statt waagerecht — wich damit von `ą` ab, obwohl CK `Ą ą Ę ę` alle gleich setzt | Diakritikum-Form je Groß/Klein-Paar | Rev. 11 |
| `Ŕ` | (16,11) | Akut `.#.`/`#.#` statt `.#`/`#.` — Pixel (3,2) zu viel | **drei Prüfungen gleichzeitig** | Rev. 12 |

Der `Ŕ`-Fall ist der Beleg dafür, dass die Prüfliste inzwischen redundant greift: der eine
Pixel lag **außerhalb der Rect-Box** (Klasse A — der einzige solche Verstoß der ganzen
Historie), brach die **Akut-Gruppenform** und erzeugte den ersten **negativen rechten Gap**
(Glyph breiter als Advance). Wäre er eine Spalte weiter links gelandet, hätte nur der
Akzent-Typ-Check angeschlagen — und der existiert erst seit Revision 5.

**Klasse D ist damit vollständig erledigt.** Der Test über alle 44 Akzent/Basis-Paare meldet
jetzt: jede Basis steckt unverändert in ihrem Akzentzeichen. Auch die Akzentpositionen sind
über alle Typen geschlossen — Punkt oben auf y3 (`ġ ż ċ`), Akut auf y2 (alle neun), Trema auf
y3 (`ä ë ï ö ü`), Hatschek auf y2–3 bei Kleinbuchstaben und y1–2 bei Versalien (deren Körper
eine Zeile höher beginnt). `ť` und `ď` tragen korrekt den Apostroph rechts statt eines
Hatscheks oben.

### 4.5 Klasse E — Fremdfarbe im Atlas (in Rev. 8 behoben)

Bei `m` (13,3) lag an der zellinternen Position **(1,6)** — absolut (105,42) — ein Pixel in
`(229,59,223)`, exakt der Magenta-Farbe des `Rects`-Layers:

```
Rev. 7:  m (13,3)   y5  ####..
                    y6  #M#.#.     M = Magenta (229,59,223) statt Weiß
                    y7  #.#.#.
                    y8  #.#.#.

Rev. 8:             y6  #.#.#.     Pixel gelöscht ✓
```

Der Pixel war **voll deckend**, also kein Alpha-Artefakt, und wäre beim Zuschnitt als Teil des
Glyphen übernommen worden — im Spiel ein rosa Punkt in **jedem** `m`, im Testrendering deutlich
sichtbar. Entstanden bei der Verbreiterung in Revision 7, offenbar mit noch aktiver
Rects-Farbe gezeichnet. Revision 8 hat ihn gelöscht; `m` liest jetzt durchgehend `#.#.#` unter
dem `####`-Balken.

> **Warum kein früherer Test das gefunden hätte:** Klasse A prüft Alpha < 255 — dieser Pixel
> ist deckend. Kollisions-, Grundform- und Akzenttest arbeiten auf der **Silhouette** (Alpha),
> nicht auf der Farbe; für sie ist ein magentafarbener Pixel ein normaler Glyphenpixel. Erst
> der Abgleich der Atlas-Farbpalette gegen die bekannten Layer-Hilfsfarben macht ihn sichtbar.
> Dieser Test ist seit Revision 7 Teil der Prüfliste (§ 10).

### 4.6 Klasse F — Cedille, Komma und Ogonek differenziert (Rev. 9–11 behoben)

**Ausgangslage — und sie geht auf eine Empfehlung dieses Dokuments zurück.** Bis Revision 8
trugen **alle zehn** Zeichen mit Diakritikum unten dieselbe Form `y9 .#` / `y10 #.`: die
Cedillen `Ç ç Ş ş`, die Kommas `Ț ț` und die lettischen `Ģ Ķ ķ Ļ ļ Ņ ņ`. Ursache war die
Behebung des Überstands in Revision 2, wo dieses Dokument riet, die Komma-Zeichen „an die
`Ç`/`ç`-Position anzugleichen" — **ohne zu prüfen, ob CK die beiden Diakritika trennt.** Der
Überstand war ein echter Defekt und musste weg; dass die Angleichung eine typografische
Unterscheidung kostet, hätte benannt werden müssen.

**CK trennt sie, und zwar auf zwei Achsen:**

```
Cedille (Ç ç Ş ş)          y8 .###    Buchstabenboden
                           y9 ..#     hängt DIREKT an, keine Lücke
                           y10 .##    unten zwei Pixel

Komma (Ț ț Ģ Ķ ķ Ļ ļ Ņ ņ)  y8 ..#     Buchstabenboden
                           y9 ---     LÜCKE
                           y10 ..#    Diagonale, je ein Pixel
                           y11 .#
```

**Die Lücken-Achse ist strukturell nicht verfügbar:** Der Versalkörper endet bei y8, die
Rect-Box bei y10. Eine Lücke plus zweizeiliges Diakritikum bräuchte y11 — außerhalb der Box.

**Der erste Lösungsvorschlag scheiterte.** Cedille als `.#`/`##` (Haken) gegen Komma als
`.#`/`#.` (Diagonale) funktioniert bei `Ç`/`ç`, dessen Boden bei x1–x2 sitzt. Bei `Ş`/`ş` liegt
der Boden aber bei x0–x1, sodass die Cedille ihn **wörtlich verdoppelt** — bei `ş` wären die
letzten vier Zeilen `.#` / `##` / `.#` / `##` gewesen, also ein doppelter s-Bogen statt eines s
mit Cedille. Ursache: `s` ist hier auf **2 px** komprimiert (CK: 4 px), womit Buchstabenbogen
und Cedille denselben Formraum beanspruchen.

**Die Lösung kam vom Autor und nutzt einen Freiheitsgrad, den der erste Vorschlag übersah:
die Position des Buchstabens in seiner Box.** Rückt der Buchstabe eine Spalte nach rechts, wird
links unten Raum frei, in den die Cedille schwingen kann — aus einem 2×2-Problem mit zu wenigen
Formen wird ein 2×3-Problem:

```
ş   .##      s-Körper auf x1–x2 verschoben
    .#.
    ..#
    .##
    .#.      Cedille
    ##.      schwingt nach links
```

**Umsetzung in Revision 9, sparsam:** Bei `Ç`/`ç` beginnt die Bodenzeile schon bei x1, dort
genügte der zusätzliche Pixel (1,10) — **Advance bleibt 3**. Nur `Ş` (3→4) und `ş` (2→3) wurden
verschoben. Kostet zwei Pixel Textbreite statt vier.

**Der Advance-Zuwachs hat Präzedenz** — sieben Akzentzeichen dieser Font sind bereits breiter
als ihre Basis, fünf davon um zwei Pixel: `š` (+1), `õ` (+1), `ő` `ű` (+2), `ť` `ľ` (+2),
`ď` (+2). Ein +1 für die Cedille ist die mildeste Ausprägung eines etablierten Prinzips.

**Ergebnis — drei klar getrennte Diakritika:**

| Diakritikum | y9 | y10 | Zeichen | Charakter |
|---|---|---|---|---|
| **Komma** | `.#` | `#.` | `Ț ț Ģ Ķ ķ Ļ ļ Ņ ņ` | Diagonale nach **links**, 2 px |
| **Ogonek** | `.#` | `..#` | `Ų ų` | Diagonale nach **rechts**, 2 px |
| **Ogonek** | `..##` | — | `Ą ą Ę ę` | waagerecht, 2 px (wie CK) |
| **Ogonek** | `.#` | — | `Į į` | ein Pixel (2 px Basisbreite) |
| **Cedille** | `.#` | `##` | `Ç ç Ş ş` | Haken nach links, **3 px** |

Komma und Ogonek sind damit exakte Spiegelbilder — typografisch korrekt, denn das Ogonek (˛)
schwingt nach rechts, das Komma nach links. Die Cedille tritt aus der Zweiteilung heraus, indem
sie einen Pixel mehr erhält. **Mehr Trennschärfe ist auf diesem Raster nicht zu holen.**

**Zur Zuordnung:** Von den zehn Zeichen mit Diakritikum unten sind typografisch nur `Ç ç Ş ş`
echte Cedillen. `Ģ ģ Ķ ķ Ļ ļ Ņ ņ` heißen in Unicode „LETTER … WITH CEDILLA", werden in der
lettischen Typografie aber als **Komma** gesetzt — CK folgt dieser Praxis, nicht dem
Unicode-Namen, und diese Font tut es ebenso. Wer nach Unicode-Namen gruppiert, kommt hier zum
falschen Ergebnis.

**Die Ogonek-Familie nutzt drei Formen**, weil der Platz je Buchstabe variiert — bei CK ist es
genauso. Innerhalb jedes Groß-/Klein-Paares ist die Form identisch; `Ą` war zunächst die
Ausnahme und wurde in Revision 11 an `ą` angeglichen (§ 4.4). `Ų ų` tragen eine zweipixlige
Diagonale statt CK's dreipixligem Haken — Kompression, untereinander konsistent.

**Beobachtung ohne Handlungsbedarf:** Zwei weitere Paare, die CK identisch führt, sind in
dieser Font unterschiedlich gezeichnet — `x` (24,3) / `х` (8,8) mit der Taille auf y6 statt
y7, und `y` (25,3) / `у` (30,8) in Gabelung und Unterlänge. Anders als bei `ё` weicht hier
keines der beiden von einer gemeinsamen Grundform ab (eine kyrillische Form ohne
lateinisches Gegenstück existiert nicht), sodass beides dieselbe bewusste
Latein/Kyrillisch-Abgrenzung sein kann wie bei `а`. Bei vier Zeilen x-Höhe gibt es zudem
keine echte Mittelzeile, sodass die Taille von `x` auf y6 **oder** y7 fallen muss.

---

## 5. Änderungen außerhalb der Defektliste

### 5.1 Zeile 0 neu ausgerichtet (Rev. 2) — Verbesserung

Alle 12 Glyphen der Zeile 0 (`♥ ♡`, die sechs Controller-Symbole, `„ “ – ”`) **und** ihre
Rect-Boxen wurden um 1 px nach unten verschoben. Damit liegen alle Boxen einheitlich auf
`(y = 1, h = 10)`; in Revision 1 saßen die 12 aus Zeile 0 auf `y = 0`. Die Glyphen stehen
jetzt auf derselben Position wie im CK-Original.

Revision 1 dieses Dokuments hatte den Versatz als harmlos eingeordnet (Box und Glyph waren
gemeinsam verschoben, relativ also korrekt) — das bleibt richtig, vereinheitlicht ist aber
klar besser. Die unveränderten Luft-unten-Werte belegen, dass nichts verrutscht ist.

### 5.2 `Р` (18,9) von `P` unterschieden — in zwei Schritten (Rev. 2 und 3)

Dieser Punkt stand ursprünglich unter „kein Defekt", weil `P` und `Р` derselbe Buchstabe sind
und thinSmall sie nur durch die Bauchhöhe trennt (bei `Р` schließt der Bauch eine Zeile
tiefer), wofür die kompakte Fassung keinen Platz hat.

**Revision 2** ergänzte zwei Pixel, (2,4) und (2,6). `Р` war damit nicht mehr pixelidentisch
mit `P` (16,2), begann oben aber 3 px breit (`###`), während `В` (23,9), `Б` (2,9) und `Ь`
(24,9) mit 2 px anfangen — ein Detail, das vorher konsistent war.

**Revision 3** nahm (2,4) zurück. Endstand:

| | y4 | y5 | y6 | y7 | y8 |
|---|---|---|---|---|---|
| `P` (16,2) | `##` | `#.#` | `##` | `#` | `#` |
| `Р` (18,9) | `##` | `#.#` | `###` | `#` | `#` |
| `В` (23,9) | `##` | `#.#` | `##` | `#.#` | `##` |

`Р` beginnt wieder mit `##` wie seine kyrillischen Nachbarn, und die Unterscheidung zu `P`
liegt jetzt in einer einzigen Zeile (y6). Der Nebeneffekt ist damit ausgeräumt; der Bauch von
`Р` ist unten 1 px breiter als oben, was der minimal mögliche Preis für die Unterscheidung ist.

### 5.3 `ġ` (22,10): Punkt auf die Punkt-Zeile gesetzt (Rev. 3)

Der Punkt über dem `g` wanderte von y2 auf y3. Damit folgt `ġ` der Systematik, die die Font
selbst aufstellt: **Punkt**-Akzente sitzen bei Kleinbuchstaben auf **y3** (`ż` 12,6 · `ċ` 18,10
· `i` 9,3), **Akut**-Akzente auf **y2–3** (`á é í ó ú ń ś ź ý`). Ein Punkt braucht nur eine
Zeile und darf deshalb näher an den Buchstaben; eine Diagonale braucht zwei. Vorher lag `ġ`
auf der Akut-Position, ohne eine Diagonale zu sein.

### 5.4 Die М/Ш/Щ-Familie verbreitert (Rev. 5), `М`/`м` nachgezogen (Rev. 6)

Um die Kollisionen `W`≡`Ш` und `м`≡`ш` zu lösen, hätte es genügt, `Ш` und `ш` zu verbreitern.
Revision 5 hat stattdessen die **ganze Familie** angehoben — `Ш ш` auf 5 px, `Щ щ` auf 6 px,
`М м` auf 5 px — obwohl `Щ`/`щ` und `М`/`м` nicht kollidierten. `Ш ш Щ щ` sind seither
**pixelidentisch mit CK's Original**.

`М`/`м` bekamen zunächst eine eigene Form (Balken oben, Mittelstrich über drei Zeilen), die
lesbar und kollisionsfrei war, aber von CK abwich. **Revision 6 hat sie nachgezogen** und
stattdessen CK's Form um eine Zeile komprimiert:

| | y4 | y5 | y6 | y7 | y8 |
|---|---|---|---|---|---|
| `М` CK (6 Zeilen, ab y3) | `##.##` | `#####` | `#.#.#` | `#...#` | `#...#` |
| `М` Rev. 5 | `#####` | `#.#.#` | `#.#.#` | `#.#.#` | `#...#` |
| `М` Rev. 6 | `#...#` | `##.##` | `#.#.#` | `#...#` | `#...#` |

Revision 6 lässt CK's `#####`-Zeile weg und behält die charakteristische `##.##`-Spitze — das
ist die saubere Kompression von 6 auf 5 Zeilen, passend zur Versalhöhe dieser Font.

### 5.5 `M` und `m` auf 5 px (Rev. 7)

Revision 6 hatte die lateinischen `W M V X A` noch bei 3 px gelassen, während ihre
kyrillischen Gegenstücke schon 5 px hatten — `W` und `M` unterschieden sich damals nur durch
die Position des Doppelbalkens. Revision 7 hat `M` (13,2) und `m` (13,3) auf **Advance 5** mit
drei Stämmen gebracht:

| | y4 | y5 | y6 | y7 | y8 |
|---|---|---|---|---|---|
| `M` | `####` | `#.#.#` | `#.#.#` | `#.#.#` | `#.#.#` |
| `m` | — | `####` | `#.#.#` | `#.#.#` | `#.#.#` |

Das löst zugleich die letzte Formkollision (`m` ≡ `н`, § 4.3). `W V X A` bleiben bei 3 px; sie
kollidieren mit nichts, unterscheiden sich untereinander und sind im Satz lesbar — ob der
Kontrast zum breiteren `M` und zum kyrillischen Satz stört, ist eine Geschmacksfrage, keine
Korrektheitsfrage.

---

## 6. Die vier Rects ohne Glyph in Zeile 11 — kein Defekt, aber eine Falle

In den Zellen (26,11)–(29,11) lag bis Revision 2 je eine Rect-Box ohne gezeichneten Glyph;
**Revision 3 hat sie gelöscht** (Rect-Boxen 341 → 337, genau −140 px). Sie stammten aus dem
Template-Grid. Es liegt nahe, sie für die verschiedenen **Leerzeichen** zu halten — das waren
sie aber nicht, und die Begründung bleibt für künftige Template-Läufe relevant:

- **Keine einzige CK-Font mappt ein Leerzeichen.** Weder thinSmall (331 Codepoints) noch
  thinTiny (114), noch `boldHuge` (341) oder auch nur die chinesische Font (3891). U+0020,
  U+00A0 und die typografischen Leerzeichen U+2000–U+200A kommen in **keiner**
  Glyphentabelle vor. CK behandelt Leerzeichen außerhalb der Glyphenauflösung; ihre Breite
  ist über diese Datei weder definierbar noch änderbar.
- **Es sind Waisen-Slots.** `glyph#378–381` deklarieren `adv = 5`, haben aber `chars = []`
  — also keinen Codepoint. `PugFont.GetGlyphData` startet mit
  `codePoints.TryGetValue(c, …)` und erreicht einen Slot ohne Codepoint deshalb nie.
- **Herkunft:** `build_glyph_grids.py` zeichnet ein Rect für **jeden** Slot mit
  `rw > 0 && rh > 0`, ohne zu prüfen, ob ein Codepoint daranhängt. Nur die Slots mit
  Breite 0 werden übersprungen — im Docstring des Skripts als "space" bezeichnet, was die
  Fehldeutung zusätzlich nahelegt. Die vier mit Breite 5 blieben deshalb im Template stehen.

**Konsequenz:** Die vier Rects waren folgenlos — das Extraktionsskript iteriert über
Codepoints und hätte sie ohnehin nie gesehen. Ihre Löschung in Revision 3 ist Aufräumen, kein
Bugfix; seither entspricht die Zahl der Rect-Boxen genau der Zahl bemalter Zellen (337), was
als Invariante prüfbar ist. Eine Möglichkeit, Leerzeichenbreiten zu beeinflussen, gab es hier
nie.

### Dieselbe Waisen-Situation in Zeile 0

`glyph#2–7` in den Zellen (2,0)–(7,0) sind ebenfalls codepointlos (`adv = 7`), tragen aber
**Pixel**: die farbigen Controller-Symbole (A/B/X/Y sowie + und −). CK spricht sie intern
über den Glyph-Index an, nicht über `codePoints`. Für die Injektion heißt das: diese sechs
Zellen werden von einer codepoint-basierten Ersetzung **nicht erfasst** — die dafür
gezeichneten Pixel landen zwar im Sheet, aber nie im Spiel. Die beiden Herzen `♥` (0,0) und
`♡` (1,0) haben dagegen echte Codepoints (U+2665 / U+2661) und werden ersetzt.

---

## 7. Farbtöne und rechte Lücken

### 7.1 Weißtöne — in Revision 5 erledigt

Bis Revision 4 enthielt der Atlas **zwei** Weißtöne, `(255,255,255)` und `(255,254,255)`, in
Revision 1 auf 2118 bzw. 1096 px verteilt und in **259 Zellen gemischt**. Beim Rendern
unsichtbar; der Off-White markierte zu 76 % neu gezeichnete Pixel (gegen den Referenzlayer
korreliert) und war damit ein Nebenprodukt des Zeichenprozesses.

**Revision 5 hat ihn vollständig entfernt:** ein einziger Weißton `(255,255,255)`, **0 Zellen**
mit gemischten Tönen. Damit ist die Textur ohne Verlust palettierbar, und der Atlas zählt nur
noch 13 Farben (die farbigen Sondersymbole der Zeile 0 sowie das Herz).

### 7.2 Rechte Lücken — in Revision 7 erledigt

Eine „rechte Lücke" ist `Advance − Glyph-Rechtskante > 0`: das Zeichen belegt weniger Breite,
als seine Rect-Box beansprucht, und bekommt beim Setzen entsprechend mehr Luft nach rechts.
In Revision 6 hatten **331 von 337** Zellen Gap 0, jetzt sind es **336 von 337** — die einzige
verbleibende Lücke ist die CK-konforme bei `×`.

| Zeichen | Zelle | Advance | Glyph | Lücke | Status |
|---|---|---|---|---|---|
| `×` U+00D7 | (8,7) | 5 | x1–3 (3 px) | 1 | **CK-konform, kein Defekt** |
| `и` U+0438 | (9,8) | 5→**4** | x0–3 (4 px) | **0** | in Rev. 7 behoben |
| `й` U+0439 | (10,8) | 5→**4** | x0–3 (4 px) | **0** | in Rev. 7 behoben |
| `И` U+0418 | (9,9) | 5→**4** | x0–3 (4 px) | **0** | in Rev. 7 behoben |
| `Й` U+0419 | (10,9) | 5→**4** | x0–3 (4 px) | **0** | in Rev. 7 behoben |
| `ş` U+015F | (6,6) | 3→**2** | x0–1 (2 px) | **0** | in Rev. 7 behoben |

Behoben wurde jeweils über die **Rect-Box**, nicht über den Glyphen — die Zeichenformen sind
unverändert, nur die Advance ist auf die tatsächliche Glyphbreite gezogen. Die Analyse der
Ursachen bleibt unten stehen, weil sie erklärt, wie die Abweichungen entstanden sind.

**`×` ist korrekt so.** CK hat dort ebenfalls Advance 5 bei einem nur 3 px breiten Kreuz — die
beiden äußeren Spalten waren in CK mit `alpha = 3` belegt (§ 4.1). Nach dem Löschen dieser
unsichtbaren Pixel bleibt ein in seiner 5-px-Box **zentriertes** Kreuz mit je 1 px Luft links
und rechts. Genau so rendert es auch in Vanilla.

**`и й И Й` — die Advance wurde nicht mitkomprimiert.** In CK sind diese vier 5 px breit bei
Advance 5. Diese Font hat den Glyphen auf 4 px verschmälert, die Advance aber bei 5 gelassen.
Dass das ein Versehen ist und keine Absicht, zeigt der Rest des kyrillischen Blocks, wo die
Advance konsequent mitwanderte:

```
н  CK 5 px → neu 3 px, Advance 3   (Gap 0)
п  CK 5 px → neu 3 px, Advance 3   (Gap 0)
ц  CK 5 px → neu 4 px, Advance 4   (Gap 0)
щ  CK 6 px → neu 6 px, Advance 6   (Gap 0)
и  CK 5 px → neu 4 px, Advance 5   (Gap 1)  ← Ausreißer
```

**Behebung:** Rect-Box auf 4 px verschmälern. Alternativ den Glyphen auf 5 px verbreitern —
`И` ist in CK `#...#` mit Diagonale, was bei 4 px eng ist; das wäre die aufwendigere, aber
CK-nähere Variante.

**`ş` — Advance vom Nachbarn übernommen.** Innerhalb der s-Familie:

```
s   Glyph 2 px, Advance 2   (Gap 0)
ś   Glyph 2 px, Advance 2   (Gap 0)
š   Glyph 3 px, Advance 3   (Gap 0)   ← der Hatschek ist breiter als der Buchstabe
ş   Glyph 2 px, Advance 3   (Gap 1)   ← Ausreißer
```

`ş` ist ein `s` mit Cedille und damit wie `s` nur 2 px breit — es trägt aber die Advance 3 von
`š`, wo sie durch den dreipixligen Hatschek gerechtfertigt ist. Bei `Ş` (5,6) stimmt es
(Versal-`S` ist 3 px breit, Advance 3, Gap 0). **Behebung:** Rect-Box auf 2 px verschmälern.

> **Statuswechsel gegenüber Revision 1.** Diese Zeile stand ursprünglich unter „ohne
> Defektcharakter — kein Handlungsbedarf", begründet damit, dass thinSmall dieselbe Streuung
> hat (−2 bis +2). Das Argument war falsch gewählt: maßgeblich ist nicht CK's Streuung, sondern
> die **Systematik dieser Font**, und die ist mit 331 von 337 Zellen bei Gap 0 eindeutig. Fünf
> der sechs Fälle sind rein mechanisch behebbar (je 1 px an der Rect-Box) und in § 1 als offene
> Punkte geführt.

---

## 8. Verhältnis zur echten thinTiny — Entscheidung: Vollersatz

Zellweiser Vergleich gegen CK's ausgeliefertes `rrs5`-Atlas über die 114 gemeinsamen
Zeichen: **30 formgleich, 84 abweichend.** Der systematische Unterschied:

| | neue Font | echte thinTiny |
|---|---|---|
| Versalhöhe | **5 px** | 6 px |
| Ziffernhöhe | **5 px** | 6 px |
| x-Höhe | 4 px | 4 px |
| Breite `C E F L` | 3 px | 2 px |

Die neue Font ist also bei Versalien und Ziffern einen Pixel flacher und bei den schmalen
Buchstaben etwas breiter (lesbarer). Alles Übrige stimmt eng überein; nebeneinander lesen
sich beide als dieselbe Schrift.

Daraus folgte die Grundsatzfrage, die am 2026-08-11 **entschieden** wurde:

> ### Entscheidung: Vollersatz — alle 331 Glyphen werden injiziert.

Die Alternativen im Vergleich:

- **Vollersatz (gewählt)** — in sich stimmig und optisch eine leichte Verbesserung gegenüber
  CK's Original. Preis: CK's Schadens- und Punktzahlen werden 1 px flacher, da Vanilla thinTiny
  ausschließlich dafür verwendet.
- **Nur die 217 fehlenden Glyphen (verworfen)** — hätte Vanilla unberührt gelassen, aber ein
  6 px hohes `A` neben ein 5 px hohes `Ä` im selben Wort gestellt. Der Stilbruch wiegt schwerer
  als der Pixel.

Die Ziffern-Advances sind in beiden Fällen identisch (3), das Textlayout verschiebt sich also
nicht — nur die Höhe der Ziffern um einen Pixel.

**Konsequenz für die Umsetzung:** Der `tt_cp`-Guard in `pixaki_to_glyphs.py` muss entfernt
werden (§ 9, Punkt 2), damit auch die 114 Glyphen extrahiert werden, die thinTiny bereits hat.

---

## 9. Nötige Änderungen an der Pipeline

`item-checklist/sources/glyph-templates/pixaki_to_glyphs.py` wurde für das Iter-25-Dokument
geschrieben (Canvas in thinTiny-Größe, Zellen 8 × 10). Für diese Datei braucht es:

1. `NEWCDX, NEWCDY = 8, 12` — das Zellraster ist das von thinSmall, nicht das von thinTiny.
2. Den Guard `if str(code) in tt_cp: continue` entfernen — **zwingend**, da § 8 auf Vollersatz
   entschieden ist: der Guard überspringt genau die 114 Glyphen, die thinTiny bereits hat.
3. `PIXAKI` auf diese Datei zeigen lassen.

Der Magenta-Filter in `mbbox()` passt unverändert zur `Rects`-Farbe `(229, 59, 223)`. Das
erzeugte Sheet wird 257 × 144 und braucht weiterhin `textureType: 8` und `spriteMode: 1`
in seiner `.meta` (die ModBuilder-Sprite-Falle).

Die Runtime-Seite (`ThinTinyGlyphPatch.cs`) bleibt mechanisch gleich, wächst aber von 85 auf
331 Einträge und muss die Konvention von `PugFont.InitCodePoints` weiterhin exakt
nachbilden: `rect2 = (x, y+1, w, h-1)`, danach `x -= 1; width += 2`, sofern es passt, plus
**zentrierter** Pivot.

---

## 10. Wie geprüft wird

Alle Zahlen stammen aus direkter Inspektion der entpackten `.pixaki` (ein reines ZIP: die
Layer-Cels sind beschnittene PNGs unter `images/drawings/<uuid>.png`, positioniert über
`cel.frame` aus `document.json`). Die Layer werden auf volle Canvas-Größe komponiert und
verglichen gegen:

- `glyph_metrics.json` — Runtime-Dump von CK's eigener Font-Metrik (Zell-/Codepoint-Zuordnung)
- `rrsthin8_raw.png` — das ausgelieferte thinSmall-Atlas
- `rrs5_raw.png` — das ausgelieferte thinTiny-Atlas

alle drei in `item-checklist/sources/glyph-templates/`.

Die Zuordnung Zelle → Zeichen folgt der thinSmall-Anordnung:
`col = rect.x // 8`, `row = 11 - (rect.y // 12)` (thinSmall-Rects liegen in
Unity-Koordinaten mit Ursprung unten links).

**Geprüfte Kriterien**, in dieser Reihenfolge: Zellbelegung und Codepoint-Abdeckung; Zahl der
Rect-Boxen (muss der Zahl bemalter Zellen entsprechen, § 6); Rect-Geometrie (y, Höhe,
x-Offset); Pixel außerhalb der Rect-Box; Pixel mit Alpha < 255; Baseline über „Luft unten" =
Boxunterkante − Glyphunterkante; rechter Gap = Advance − Glyph-Rechtskante; Höhenklassen je
Alphabet und Groß-/Kleinschreibung; Formkollisionen über Pixelsignatur-Vergleich (gleich in
dieser Font, verschieden im Original); **Grundform-Konsistenz** und **Akzent-Typ-Konsistenz**
(beide unten); Akzentpositionen je Akzenttyp; Farbtöne (§ 7.1); Pixel-Diff gegen die
Vorrevision; **Fremdfarben-Abgleich** (unten).

**Grundform-Konsistenz — das produktivste Kriterium.** Ein akzentuiertes Zeichen muss seine
unakzentuierte Grundform unverändert tragen; ein kyrillisches Zeichen, das CK identisch zu
seinem lateinischen Partner führt, muss es auch hier sein. Der Test findet Versehen, die kein
anderes Kriterium sieht, weil er **innerhalb** der Font vergleicht statt gegen das Original —
und weil eine Ein-Pixel-Abweichung gegen zwei oder mehr konsistente Geschwister praktisch
nie eine Formentscheidung ist. Er hat `ё`, `ć`, `ğ` und die `š`/`Š`-Fehldiagnose gefunden.

**Den Test mengentheoretisch formulieren, nicht geometrisch:** *Ist die Pixelmenge der Basis
eine Teilmenge des Akzentzeichens?* Eine erste Fassung bestimmte den „Körper" geometrisch als
alles unterhalb der obersten Lücke — die lieferte False Positives bei `å` und `ů`, weil deren
Ring **ohne Lücke** direkt über dem Buchstaben sitzt und damit als Körperteil gezählt wurde.
Der Teilmengentest ist unabhängig davon, wo der Akzent sitzt und ob er den Buchstaben berührt.

**Akzent-Typ-Konsistenz — der Test für Fehler ohne Kollisionspartner.** Normalisiere die
Akzentform jedes Zeichens (Pixel oberhalb der obersten Lücke, auf (0,0) verschoben) und
gruppiere sie nach Akzenttyp; pro Typ muss genau eine Form herauskommen. Dieser Test findet,
was Kollisions- und Grundform-Prüfung **nicht** sehen können: einen falschen Akzent, für den
kein verwechselbares Partnerzeichen existiert. `Ű`/`ű` trugen die Tilde statt des
Doppelakuts — unentdeckbar über Kollisionen, weil `Ũ` im CK-Zeichensatz nicht vorkommt
(§ 4.4). Geprüfte Typen: Akut, Gravis, Zirkumflex, Trema, Tilde, Doppelakut, Hatschek, Punkt
oben, Breve.

**Rechte Lücke gegen die eigene Systematik prüfen, nicht gegen CK.** `Advance −
Glyph-Rechtskante` sollte 0 sein. CK selbst streut hier (−2 bis +2), was in Revision 1 zu dem
Fehlschluss „kein Handlungsbedarf" führte. Maßgeblich ist die Verteilung **innerhalb** der
geprüften Font: liegen 331 von 337 Zellen bei 0, sind die übrigen sechs Ausreißer und
verlangen eine Erklärung (§ 7.2).

**Farbpalette des Atlas gegen die Layer-Hilfsfarben abgleichen.** Der Atlas darf nur Weiß und
die farbigen Sondersymbole der Zeile 0 enthalten. Taucht dort eine der Hilfsfarben auf — das
Magenta des `Rects`-Layers `(229,59,223)`, das Grün von `Dims` `(60,244,44)`/`(36,146,26)` oder
das Cyan des `Background` `(44,232,244)` —, wurde mit der falschen Farbe auf dem falschen Layer
gezeichnet. **Dieser Test ist unverzichtbar, weil alle anderen Prüfungen farbblind sind:**
Kollisions-, Grundform- und Akzenttest arbeiten auf der Silhouette (Alpha), und Klasse A prüft
nur Alpha < 255. Ein voll deckender Fremdfarb-Pixel ist für sie ein normaler Glyphenpixel — er
fällt erst im Spiel auf. So gefunden in Revision 7 (§ 4.5).

**Diakritikum-Form je Groß-/Klein-Paar vergleichen.** Der Akzent-Typ-Check gruppiert über den
ganzen Typ und akzeptiert daher mehrere Formen, wenn der Platz sie erzwingt — die Ogonek-Familie
braucht legitim drei (§ 4.6). Innerhalb eines Groß-/Klein-Paares muss die Form aber identisch
sein. Genau durch diese Lücke fiel `Ą`, das die Diagonale trug, während sein `ą` waagerecht war
(§ 4.4). Der Test ist also die feinere Ergänzung zum Typ-Check, nicht dessen Ersatz.

**Diakritika unten getrennt prüfen.** Sie liegen unterhalb des Buchstaben*körpers*, weshalb der
Akzent-Typ-Check sie nicht erfasst (er sammelt die Pixel *oberhalb* der obersten Lücke). Cedille,
Komma und Ogonek müssen als eigene Gruppen geprüft werden — bei einer angehängten Cedille gibt es
zudem gar keine Lücke, an der ein „Diakritikum" abgegrenzt werden könnte. Der belastbare Test ist
der direkte Vergleich der beiden untersten Zeilen je Zeichengruppe (§ 4.6).

**Bei Akzentzeichen den Körper messen, nicht `top`.** Die oberste Pixelzeile ist dort der
Akzent. Wer die Höhenklasse an `top` festmacht, vergleicht Akzente statt Buchstaben — genau
das führte zur falschen `š`/`Š`-Diagnose (§ 4.3). Der Körper ist die Pixelgruppe unterhalb der
Akzentlücke.

**Wichtig bei Alpha-Vergleichen:** halbtransparente Pixel müssen als eigene Stufe
dargestellt werden. Ein Dump, der jedes `alpha > 0` als vollen Pixel zeichnet, verdeckt
genau die Artefakte, um die es geht — das war die Ursache des `×`-Fehlschlusses in
Revision 1 (§ 4.1).

**Bekannte Ausnahmen, die keine Defekte sind.** Eine Prüfung muss sie als Whitelist führen,
sonst meldet sie sie in jeder Runde neu:

- **Höhenklassen:** `а` (1,8), `в` (23,8), `з` (26,8), `э` (28,8), `З` (26,9), `Э` (28,9)
  stehen formbedingt 1 px höher (§ 4.2); `б` (2,8) und `ф` (6,8) tragen echte Ober- bzw.
  Unterlängen.
- **Rect-Box ohne Glyph:** seit Revision 3 keine mehr; die vier Waisen-Slots (26,11)–(29,11)
  sind gelöscht (§ 6). Ein erneutes Auftauchen wäre ein Template-Artefakt, kein Defekt.
- **Zulässig gleiche Formen:** thinSmall führt selbst **23** Zeichengruppen identisch —
  `'`/`´`, `A`/`А`, `B`/`В`, `C`/`С`, `E`/`Е`, `H`/`Н`, `I`/`І`, `K`/`К`, `O`/`О`, `T`/`Т`,
  `X`/`Х`, `c`/`с`, `e`/`е`, `i`/`і`, `o`/`о`, `p`/`р`, `r`/`г`, `x`/`х`, `y`/`у`, `Ë`/`Ё`,
  `Ï`/`Ї`, `ë`/`ё`, `ï`/`ї`. Der Kollisionstest filtert sie korrekt heraus, weil er auf
  „gleich hier, verschieden im Original" prüft und nicht auf Gleichheit allein. In dieser
  Font sind 20 davon ebenfalls gleich; die drei Abweichungen stehen in § 4.4.
  **`a`/`а` ist ausdrücklich nicht dabei** — CK trennt die beiden, und die Font tut es auch
  (§ 4.2).

**Eine methodische Grenze:** Eine Abweichung von der Systematik ist messbar, ihre *Ursache*
nicht. Ob ein Zeichen, das aus seiner Höhenklasse fällt, ein Versehen oder eine bewusste
Formentscheidung ist, entscheidet der Autor — nicht die Messung. Revision 1 hatte hier zu
schnell auf „Versehen" geschlossen (§ 4.2).

---

## 11. Revisionshistorie dieses Dokuments

| Rev. | Datum | Änderung |
|---|---|---|
| 1 | 2026-08-11 | Erstprüfung gegen Dateistand 2026-08-10. 26 Defekte in drei Klassen. |
| 1a | 2026-08-11 | § 6 ergänzt: die vier Rects ohne Glyph sind **keine** Leerzeichen, sondern codepointlose Waisen-Slots aus dem Template-Grid; keine CK-Font mappt Whitespace. Dazu die gleiche Situation bei den Controller-Symbolen in Zeile 0. |
| 2 | 2026-08-11 | Prüfung gegen Dateistand 15:37. Klasse A erledigt, Klasse B 2/8, Klasse C unverändert. **Korrektur:** die `×`-Analyse aus Rev. 1 war falsch — die halbtransparenten Pixel stehen so in CK's eigenem Atlas (§ 4.1). Neu: `Ģ`-Abweichung (§ 4.1), Neuausrichtung Zeile 0 und `Р`-Änderung (§ 5). |
| 2a | 2026-08-11 | **Korrektur:** die sechs restlichen Höhenabweichungen der Klasse B sind **keine** Defekte, sondern formbedingt — eine weitere Kompression würde den Zeichencharakter zerstören (Entscheidung des Autors, durch Formanalyse gestützt, § 4.2). Damit 7 statt 13 offene Punkte. Der begrenzende Faktor ist durchweg die **vertikale** Gliederung, nicht die Breite. § 10 um eine Whitelist bekannter Ausnahmen und die methodische Grenze „Messung zeigt die Abweichung, nicht ihre Ursache" ergänzt. |
| 2b | 2026-08-11 | `а`-Begründung präzisiert: es geht um die **Abgrenzung zum lateinischen `a`**, die CK selbst vornimmt — `a`/`а` gehört nicht zu den identisch geführten Paaren (§ 4.2, § 10). Aus diesem Abgleich **neu gefunden:** `ё` (8,10) fehlt der Pixel (0,8), womit es als einziges Mitglied der `e`-Familie von seiner Grundform abweicht (§ 4.4, neue Klasse D) — damit 8 offene Punkte. Zahl der identisch geführten Zeichengruppen von 19 auf **23** korrigiert (gemessen gegen `rrsthin8_raw.png` statt gegen den Referenzlayer); `x`/`х` und `y`/`у` als Beobachtung ohne Handlungsbedarf ergänzt. |
| 3 | 2026-08-11 | Prüfung gegen Dateistand 17:21. **Behoben:** `ё` (0,8) ergänzt, `Ģ`-Cedille auf die Familienposition gebracht, die vier Waisen-Rects gelöscht (§ 6), der `Р`-Nebeneffekt zurückgenommen (§ 5.2), `ġ`-Punkt auf die Punkt-Zeile gesetzt (§ 5.3). Klasse C unverändert. **Korrektur:** bei `š`/`Š` war die Diagnose vertauscht — beide stehen auf Versalhöhe, also ist `Š` korrekt und `š` der Ausreißer; Ursache war die Messung an `top` statt am Körper (§ 4.3, § 10). **Neu gefunden:** `ć` (16,6) ist als Ganzes 1 px zu hoch und `ğ` (2,6) hat einen Pixel zu viel in der Unterlänge (§ 4.4) — beides über den auf alle Akzentzeichen ausgeweiteten Grundform-Konsistenz-Test. 8 offene Punkte. |
| 4 | 2026-08-11 | Prüfung gegen Dateistand 17:37. **Behoben:** `š` trägt jetzt die `s`-Kleinform (Kollision mit `Š` aufgelöst, § 4.3), `ć` komplett um 1 px nach unten gesetzt, `ğ` der Pixel (0,9) gelöscht (§ 4.4) — Klasse D vollständig erledigt. Keine Regression; der Diff umfasst genau diese drei Zellen. **Damit ist die Font mechanisch fehlerfrei:** die 5 verbleibenden Punkte sind sämtlich Formkollisionen der Klasse C und brauchen Zeichenentscheidungen. § 10 um die mengentheoretische Fassung des Grundform-Tests ergänzt (die geometrische Variante lieferte False Positives bei `å` und `ů`). |
| 5 | 2026-08-11 | Prüfung gegen Dateistand 18:09. **Behoben:** vier der fünf Kollisionen — `Ш`/`ш` auf Advance 5 in **CK-identischer Form**, `Ő`/`ő` mit echtem Doppelakut auf Advance 5; dazu `Щ`/`щ` und `М`/`м` mitverbreitert, obwohl sie nicht kollidierten (§ 5.4). **§ 7.1 erledigt:** nur noch ein Weißton, 0 Zellen gemischt — die Textur ist palettierbar. **Neu gefunden:** `Ű`/`ű` trugen als letzte Doppelakut-Zeichen noch die Tilde-Form; gefunden über einen neuen **Akzent-Typ-Vergleich** (§ 4.4, § 10). Beobachtung: `М`/`м` in eigener Form statt CK's. |
| 6 | 2026-08-11 | Prüfung gegen Dateistand 18:20. **Behoben:** `Ű`/`ű` auf Doppelakut + Advance 5 (Doppelakut-Familie geschlossen), `М`/`м` auf CK's Form nachgezogen als saubere 5-Zeilen-Kompression (§ 5.4). **Statuswechsel:** die rechten Lücken aus § 7 sind **keine** Nicht-Defekte — 5 der 6 sind mechanisch behebbar (`и й И Й` Advance 5 bei 4 px Glyph, `ş` Advance 3 bei 2 px Glyph), nur `×` ist CK-konform. Das Revision-1-Argument („thinSmall streut auch") war falsch gewählt: maßgeblich ist die Systematik dieser Font mit 331/337 bei Gap 0 (§ 7.2, § 10). **6 offene Punkte:** `m` ≡ `н` plus die 5 Advance-Lücken. |
| 7 | 2026-08-11 | Prüfung gegen Dateistand 18:26. **Behoben:** `m` **und** `M` auf Advance 5 mit drei Stämmen — damit ist die **letzte Formkollision** weg und Klasse C vollständig erledigt (§ 4.3, § 5.5); außerdem alle fünf rechten Lücken über die Rect-Box korrigiert (`и й И Й` auf Advance 4, `ş` auf 2), sodass 336 von 337 Zellen Gap 0 haben (§ 7.2). **Neu gefunden — Klasse E:** ein voll deckender Pixel in der Rects-Magenta-Farbe `(229,59,223)` im **Atlas**-Layer bei `m` (13,3)/(1,6), im Spiel als rosa Punkt in jedem `m` sichtbar (§ 4.5). Gefunden über einen neuen **Fremdfarben-Abgleich** — nötig, weil alle anderen Tests auf der Silhouette arbeiten und damit farbblind sind (§ 10). **1 offener Punkt.** |
| 8 | 2026-08-11 | Prüfung gegen Dateistand 18:32. **Behoben:** der Magenta-Pixel bei `m` (13,3)/(1,6) ist gelöscht — Diff genau diese eine Zelle (§ 4.5). **Damit ist die Font defektfrei.** Alle Prüfungen grün: keine Formkollision, alle neun Akzenttypen einheitlich, alle 38 Akzent/Basis-Paare konsistent, 336/337 Zellen ohne rechte Lücke, ein Weißton, keine Layer-Fremdfarbe, kein Überstand, kein Alpha < 255, 331/331 Codepoints, 337 bemalte Zellen = 337 Rect-Boxen. Offen bleibt allein die Grundsatzentscheidung aus § 8 (Vollersatz vs. nur die fehlenden 217 Glyphen). |
| 9 | 2026-08-11 | Prüfung gegen Dateistand 18:56. **Cedille von Komma differenziert** (`Ç ç Ş ş`), nachdem die Nachfrage des Autors offenlegte, dass bis dahin **alle zehn** Zeichen mit Diakritikum unten dieselbe Form trugen — Folge einer Empfehlung dieses Dokuments aus Revision 2 (§ 4.6). Der erste Lösungsvorschlag (`.#`/`##`) scheiterte an `Ş`/`ş`, weil er deren Bodenzeile verdoppelt; die tragfähige Lösung kam vom Autor: den Buchstaben eine Spalte nach rechts rücken und die Cedille nach links schwingen lassen. Umgesetzt sparsam — `Ç`/`ç` ohne Verschiebung, nur `Ş` (3→4) und `ş` (2→3) breiter. **Kollateral:** ein Pixel zu viel im Zirkumflex von `ê`. |
| 10 | 2026-08-11 | `ê` (9,5) gefixt — Zirkumflex wieder identisch zu `â î ô û`. Auf Nachfrage zusätzlich die **Ogonek-Familie** geprüft: kein Konflikt mit der neuen Cedille, Komma und Ogonek sind exakte Spiegelbilder. Dabei gefunden: `Ą` trug eine andere Ogonek-Form als `ą` (§ 4.4). |
| 11 | 2026-08-11 | `Ą` (7,6) an `ą` angeglichen — die Ogonek-Gruppierung entspricht jetzt CK (`Ą ą Ę ę` waagerecht, `Į į` ein Pixel, `Ų ų` Diagonale). **Kollateral:** ein Pixel (3,2) im Akut von `Ŕ`, der gleich drei Prüfungen auslöste — Überstand über die Rect-Box, gebrochene Akut-Gruppenform und erstmals ein negativer rechter Gap. |
| 12 | 2026-08-11 | `Ŕ` (16,11) gefixt. **Die Font ist wieder defektfrei** — und gegenüber Revision 8 zusätzlich um die Differenzierung der drei Diakritika unten verbessert. Alle Prüfungen grün: keine Formkollision, neun Akzenttypen oben einheitlich, drei Diakritika unten getrennt, 44/44 Akzent/Basis-Paare, 336/337 ohne rechte Lücke, ein Weißton, keine Fremdfarbe, kein Überstand, kein Alpha < 255. **§ 8 entschieden: Vollersatz.** |

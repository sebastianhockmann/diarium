# Handoff – DiariumBookStudio

Stand: 2026-11-04 · Version v25

**Erledigt in v25 (Schritte 3 und 4 zum Teil):** Druckerei-Profil und Buchumschlag.
- Gewählte Druckerei: **Lulu**, Profil `lulu-a5-pb-premium` (A5-Taschenbuch, Premium-Farbe, 80# gestrichen,
  matt; `pod_package_id` `0583X0827FCPREPB080CW444MXX`). Begründung: einzige der betrachteten Druckereien mit
  öffentlicher Druck-API (Schritt 6), A5 verfügbar, Vorgaben veröffentlicht. Quelle der Maße: Lulu Book Creation
  Guide (Beschnitt 0,125 in, Sicherheitsabstand 0,5 in, Taschenbuch 32–800 Seiten, Rücken = Seiten/444 + 0,06 in,
  Bundsteg-Tabelle). Profile liegen in `Services/PrintProfiles.cs`; weitere Druckereien = weiterer Eintrag.
- Umschlag: `Services/CoverExporter.cs` + `Templates/diarycover.sty`; Rückenbreite aus der echten Seitenzahl.
- Innenteil-Maße kommen aus dem Profil (`bookgeometry.tex`), Bundsteg nach Seitenzahl, ggf. automatischer Neusatz.
- Fotos > 600 ppi werden auf 450 ppi verkleinert (`Services/ImageExport.cs`, mit Cache).
- **Getestet** am Projekt Diarium_2026-10-01 (Jan–Okt, 233 Einträge) mit pdfinfo/pdffonts/pdfimages:
  300 Seiten; Seite mit Beschnitt 437,53 × 613,28 pt (= 154,35 × 216,35 mm), TrimBox 9 pt innen;
  alle Schriften eingebettet; 869 Bilder, 378–591 ppi (vorher 536 Bilder über 600 ppi); Umschlag
  910,02 × 613,28 pt (= 321,04 × 216,35 mm), Rücken 18,69 mm. Sichtprüfung Umschlag und Doppelseiten ok.
  Dabei gefunden und behoben: TrimBox/BleedBox wurden nicht ins PDF geschrieben (`\pdfvariable pageattr`
  expandiert nicht). Export mit Bild-Cache: 22 s beim ersten, 1 s bei weiteren Läufen; LuaLaTeX ca. 80 s.
- **Nicht verifiziert** (erst mit API-Zugang möglich, Schritt 6): ob Lulu die Dateien im Validator ohne
  Beanstandung annimmt, ob der `pod_package_id` für A5 genau so bestellbar ist, Preise und Versand nach DE.
  Die Lulu-API bietet `/cover-dimensions/` und Dateivalidierung – damit die Formel gegenprüfen.
- Hinweis: Die Fotos im Diarium-Export sind höchstens 1600 px breit. Für den Umschlag (ca. 157 mm breit)
  reicht das für ca. 270 ppi – gut, aber unter Lulus Empfehlung von 300 ppi.
- Hardcover (Case Wrap) bewusst noch nicht: Lulu nennt dafür nur eine Rückentabelle, die Umschlagmaße
  (Einschlag) sollten über die API ermittelt werden.

**Erledigt in v23 (Schritt 1 der Reihenfolge unten):** Autospeichern (5 s, beim Beenden und vor
Projektwechsel, Rückfrage nur bei Fehler), atomares Speichern, Strg+S, Rechtschreibprüfung de-DE
(Text, Bildunterschriften), Strg+B/Strg+I und Knöpfe für fett/kursiv/Liste. Risiko 1 ist damit behoben.

**Erledigt in v24 (Schritt 2):** Vorabprüfung (`Services/Preflight.cs`, `PreflightWindow`). Der Export
sammelt fehlende/defekte Fotos und die effektive Auflösung in der echten Druckbreite (`GalleryLayout.Render`
meldet die Breite jedes Fotos per Callback; Satzspiegel 114 × 172 mm als Konstanten in `PreflightReport` –
bei Layoutänderungen in `diarybook.sty` mitpflegen) sowie ungeprüfte Einträge. Knopf „Prüfen“ und
automatische Prüfung vor „PDF erzeugen“ (Dialog nur bei Fehlern/Warnungen). Getestet am Projekt
Diarium_2026-10-01: 233 Hinweise (ungeprüft), keine Auflösungsprobleme. Noch offen aus Risiko 3:
Seitenzahl-Grenzen der Druckerei – gehört zu den Druckerei-Profilen (Schritt 4).

## Ziel

Aus einem Diarium-Export pro Jahr möglichst automatisch ein gedrucktes Buch erzeugen –
im Idealfall per Knopfdruck direkt an einen Druckdienstleister übergeben.

## Projektstand (Bewertung)

**Reifegrad: funktionsfähiger Prototyp für den Eigengebrauch.** Der Weg
Import → Prüfen → PDF funktioniert. Für „ein Klick bis zum gedruckten Buch“ fehlen
nur noch die Bestellung per API (Druckdateien für Lulu entstehen seit v25 automatisch).

Build: `dotnet build` ohne Warnungen und Fehler (.NET 8, WPF). LuaLaTeX (MiKTeX) ist auf dem
Entwicklungsrechner installiert.

### Was gut funktioniert

| Bereich | Stand |
|---|---|
| Import | ZIP und entpackter Ordner, mehrere HTML-Dateien, erneuter Import übernimmt Bearbeitungen über Eintragsschlüssel |
| Jahresbuch | Jahresauswahl filtert Baum und Buch; Titelvorschlag „Tagebuch <Jahr>“ |
| Bearbeitung | Text (`**fett**`, `*kursiv*`, Listen), Prüfstatus, Bildauswahl/-reihenfolge, Titelbild, Bildunterschriften, Ortsnamen |
| Layout | Sauber getrennt: `book.tex` = Daten, `diarybook.sty` = Design; projektspezifische `.sty` hat Vorrang |
| Satz | EB Garamond, Initialen, Monatsseiten mit Statistik, Blocksatz-Fotoreihen, Karten (OSM, gecacht), Emoji-Fallback |
| Druck | A5 mit 3 mm Beschnitt, TrimBox/BleedBox, Seitenzahl auf Vielfaches von 4, Graustufen-Option für S/W-Textseiten |
| Fehler | Verständliche LuaLaTeX-Fehlermeldungen mit betroffenem Eintrag |

### Schwächen und Risiken

1. ~~**Kein Autospeichern und keine Rückfrage beim Schließen**~~ – behoben in v23.
2. ~~**Kein Umschlag**~~ – behoben in v25 (Lulu-Profil).
3. ~~**Keine Druck-Vorabprüfung**~~ – behoben in v24 (Auflösung, fehlende Bilder, ungeprüfte Einträge);
   offen ist nur die Seitenzahl-Prüfung gegen die Grenzen der Druckerei.
4. **Kein PDF/X, Fotos in RGB** – die meisten Digitaldruckereien akzeptieren RGB-PDFs, aber Farbabweichungen
   sind möglich. PDF/X-3/-4 wäre für einige Anbieter Voraussetzung.
5. **Keine Tests** – Importer (HTML-Parsing, Datumsformate), `LatexText.Escape` und `GalleryLayout` sind gut testbar,
   aber ungetestet. Regressionen fallen erst beim LuaLaTeX-Lauf auf.
6. **Code-Struktur** – Logik liegt im Code-Behind von `MainWindow` (kein MVVM). Für die jetzige Größe (~2 000 Zeilen)
   noch in Ordnung, wird aber mit Druck-Workflow, Vorlagen und Bestelldialog unübersichtlich.
7. **Abhängigkeit von installiertem MiKTeX** – fehlende Pakete lädt MiKTeX beim ersten Lauf nach (langsam, ggf. Dialog).
   Für eine Weitergabe der App wäre ein mitgeliefertes/portables TeX oder eine Prüfung beim Start sinnvoll.
8. **Externe Dienste** – OSM-Kacheln und Nominatim: Nutzungsbedingungen beachten (Rate-Limit, Attribution
   „© OpenStreetMap-Mitwirkende“ im Buch – prüfen, ob im Impressum/Schluss vorhanden).

## Empfehlungen zu den offenen Fragen

### Ein Buch pro Jahr, möglichst automatisch

Die Jahresauswahl ist vorhanden. Ziel-Workflow mit **einem Button „Jahresbuch drucken…“**:

1. Jahr wählen (Vorauswahl: zuletzt abgeschlossenes Jahr).
2. Vorabprüfung mit Liste zum Anklicken: ungeprüfte Einträge, Bilder mit zu geringer Auflösung,
   fehlende Bilder, Seitenzahl.
3. Innenteil-PDF und Umschlag-PDF erzeugen (Rückenbreite aus Seitenzahl berechnet).
4. Kosten anzeigen, Bestellung bestätigen, an Druckerei übertragen, Auftragsstatus speichern.

Sinnvolle Automatik-Vorgaben, damit wenig Handarbeit nötig ist: Bilder standardmäßig nach Regel
auswählen (z. B. max. 6 pro Eintrag, Duplikate/Serienbilder weg), Titelbild des Buches = meistgenutztes
oder manuell markiertes Foto, Titel „Tagebuch <Jahr>“.

### Button für den Druckdienstleister

Eine echte API-Übergabe bieten nur wenige Anbieter. Stand der Recherche (vor Umsetzung verifizieren):

- **Lulu Print API** – öffentliche REST-API mit Sandbox: Druckauftrag mit Innenteil- und Umschlag-PDF
  (per URL), Kostenberechnung, Endpunkt für Umschlagmaße/Rückenbreite. Bester Kandidat für einen echten Button.
  Nachteil: PDFs müssen per URL erreichbar sein (z. B. kurzlebiger Upload auf eigenen Speicher), Versand nach DE prüfen.
- **Peecho / Gelato** – Print-APIs, eher für Shops; Buchformate und Konditionen prüfen.
- **epubli, BoD, Blurb, Saal Digital, CEWE** – keine öffentliche Bestell-API. Hier kann der Button nur
  „druckfertiges Paket erzeugen“ (Innenteil + Umschlag in den Maßen des Anbieters) und die Upload-Seite öffnen.

Empfehlung: Druckerei-Anbindung als austauschbares Profil umsetzen (`IPrintProvider`: Formate, Papier,
Rückenbreiten-Formel, Seitenzahlgrenzen, optional API). Zuerst ein Profil „Paket erzeugen“ (für jede
Druckerei nutzbar), danach Lulu als API-Profil.

### Formatierung und Rechtschreibprüfung

- **Rechtschreibprüfung: ja** – sehr geringer Aufwand. WPF bringt sie mit:
  `SpellCheck.IsEnabled="True"` und `Language="de-DE"` am Textfeld (nutzt die Windows-Rechtschreibprüfung).
  Optional später Grammatik über LanguageTool (lokal oder API).
- **Formatierungsoptionen: schlank** – Fett/Kursiv/Liste existieren bereits als Markdown-Syntax.
  Sinnvoll sind Knöpfe bzw. Strg+B/Strg+I, die die Markierung einschließen, plus „Absatz“ und
  ggf. „Zitat“. Kein Rich-Text-Editor: das Buchlayout soll einheitlich bleiben.
- **Wichtiger als Formatierung: Vorschau** – eine Seitenvorschau des aktuellen Eintrags (nur dieser
  Eintrag, schnell kompiliert) zeigt Umbrüche und Bildreihen, bevor das ganze Buch gesetzt wird.

### Layoutvorlagen

**Ja, aber wenige und gut gemachte** (2–3 statt vieler). Technisch ist das fast vorbereitet, weil das
Design komplett in `diarybook.sty` steckt:

- Vorlagen als `Templates/<name>/diarybook.sty` (z. B. „Klassisch“ = heutiges Layout, „Modern“
  serifenlos mit großen Fotos, „Fotobuch“ mit ganzseitigen Bildern und wenig Text).
- Auswahl im Projekt (`BookProject.Template`), eigene `.sty` im Projektordner bleibt als Experten-Option.
- Zu jeder Vorlage gehört eine passende **Umschlagvorlage** (`cover.tex`) und eine Farbauswahl für den Akzent.

## Vorgeschlagene Reihenfolge

1. ~~Autospeichern + Rückfrage beim Schließen; Rechtschreibprüfung; Strg+B/Strg+I.~~ *(erledigt in v23)*
2. ~~Vorabprüfung (Auflösung, fehlende Bilder, ungeprüfte Einträge) als Dialog vor der PDF-Erzeugung.~~ *(erledigt in v24)*
3. ~~Umschlag-PDF mit berechneter Rückenbreite.~~ *(erledigt in v25)*
4. Druckerei-Profile *(Grundlage und Lulu-Profil erledigt in v25)*; offen: weitere Profile (z. B. epubli,
   Lulu Hardcover) und Button „Druckfertiges Paket“ (Innenteil + Umschlag + Infodatei in einem Ordner/ZIP). *(klein)*
5. Vorlagenauswahl (2–3 Vorlagen). *(mittel)*
6. Lulu-API-Anbindung mit Kostenabfrage und Bestellung (Sandbox zuerst). *(groß)*
7. Unit-Tests für Importer, Escaping und Galerie-Layout; schrittweise Logik aus `MainWindow` herauslösen.

## Wichtige Dateien

- `DiariumBookStudio.App/MainWindow.xaml(.cs)` – Oberfläche und Ablauf (Import, Export, PDF)
- `Services/DiariumImporter.cs` – HTML-Export einlesen, Bearbeitungen übernehmen
- `Services/LatexExporter.cs` – `book.tex` und `bookgeometry.tex` erzeugen
- `Services/ImageExport.cs` – Fotos lesen, drehen, auf Druckgröße verkleinern (Cache)
- `Services/PrintProfiles.cs` – Druckerei-Profile (Lulu), Satzspiegel, Rückenbreite, Bundsteg
- `Services/CoverExporter.cs` + `Templates/diarycover.sty` – Buchumschlag
- `Services/Preflight.cs` + `PreflightWindow` – Vorabprüfung
- `Services/GalleryLayout.cs` – Fotoreihen im Blocksatz
- `Services/MapService.cs` – OSM-Karten, Kachel-Cache, Reverse-Geocoding
- `Templates/diarybook.sty` – komplettes Buchdesign
- Projektdaten: `Dokumente/DiariumBookStudio/<Export>/diarium-book-project.json`

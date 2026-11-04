# Handoff – DiariumBookStudio

Stand: 2026-11-04 · Version v22 (Commit `21ac0a6` + diese Datei)

## Ziel

Aus einem Diarium-Export pro Jahr möglichst automatisch ein gedrucktes Buch erzeugen –
im Idealfall per Knopfdruck direkt an einen Druckdienstleister übergeben.

## Projektstand (Bewertung)

**Reifegrad: funktionsfähiger Prototyp für den Eigengebrauch.** Der Weg
Import → Prüfen → PDF funktioniert. Für „ein Klick bis zum gedruckten Buch“ fehlen
Umschlag, Druck-Vorabprüfung und die Anbindung an eine Druckerei.

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

1. **Kein Autospeichern und keine Rückfrage beim Schließen** – ungespeicherte Bearbeitungen gehen
   beim Beenden verloren (es gibt keinen `Closing`-Handler). Höchste Priorität, kleiner Aufwand.
2. **Kein Umschlag** – Druckereien verlangen ein separates Umschlag-PDF (Rückseite + Rücken + Vorderseite),
   dessen Rückenbreite von Seitenzahl und Papier abhängt. Ohne Umschlag ist keine automatische Bestellung möglich.
3. **Keine Druck-Vorabprüfung** – keine Warnung bei zu niedriger Bildauflösung (< ~200 dpi in Druckgröße),
   fehlenden Bildern (nur in `map_debug.txt`), ungeprüften Einträgen oder Seitenzahl außerhalb der Grenzen der Druckerei.
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

1. Autospeichern + Rückfrage beim Schließen; Rechtschreibprüfung; Strg+B/Strg+I. *(klein)*
2. Vorabprüfung (Auflösung, fehlende Bilder, ungeprüfte Einträge) als Dialog vor der PDF-Erzeugung. *(mittel)*
3. Umschlag-PDF mit berechneter Rückenbreite. *(mittel)*
4. Druckerei-Profile, Button „Druckfertiges Paket“. *(mittel)*
5. Vorlagenauswahl (2–3 Vorlagen). *(mittel)*
6. Lulu-API-Anbindung mit Kostenabfrage und Bestellung (Sandbox zuerst). *(groß)*
7. Unit-Tests für Importer, Escaping und Galerie-Layout; schrittweise Logik aus `MainWindow` herauslösen.

## Wichtige Dateien

- `DiariumBookStudio.App/MainWindow.xaml(.cs)` – Oberfläche und Ablauf (Import, Export, PDF)
- `Services/DiariumImporter.cs` – HTML-Export einlesen, Bearbeitungen übernehmen
- `Services/LatexExporter.cs` – `book.tex` erzeugen, Bilder vorbereiten
- `Services/GalleryLayout.cs` – Fotoreihen im Blocksatz
- `Services/MapService.cs` – OSM-Karten, Kachel-Cache, Reverse-Geocoding
- `Templates/diarybook.sty` – komplettes Buchdesign
- Projektdaten: `Dokumente/DiariumBookStudio/<Export>/diarium-book-project.json`

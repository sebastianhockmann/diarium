# Diarium Book Studio

Kleiner WPF-Prototyp für Visual Studio, um einen Diarium-Export in eine Buchfassung zu überführen.

## Aktueller Funktionsumfang

- Diarium-ZIP (HTML-Export) importieren; erneuter Import übernimmt bisherige Bearbeitungen
- Einträge nach Monaten als Baum anzeigen
- bereits geprüfte Einträge grün markieren
- Tagebuchtext bearbeiten, ohne Diarium zu verändern (`**fett**`, `*kursiv*`, Listen mit `- `)
- Bilder auswählen, sortieren, ein Titelbild pro Eintrag festlegen
- Bildunterschriften pflegen, Ortsnamen pflegen (werden sonst automatisch ermittelt)
- Buchtitel und Untertitel festlegen, Druckversion mit Beschnittzugabe
- Projekt als JSON speichern (Pfade relativ, Projektordner ist verschiebbar)
- LaTeX-Datei in frei wählbaren Zielpfad exportieren, PDF mit LuaLaTeX erzeugen

## Layout anpassen

Das gesamte Design steckt in `DiariumBookStudio.App/Templates/diarybook.sty`
(Schrift, Farben, Eintragskopf, Monatsseiten, Fotos, Seitenformat). `book.tex`
enthält nur Daten. Für ein projektspezifisches Layout die Datei als
`diarybook.sty` in den Projektordner (neben `diarium-book-project.json`) kopieren
und dort anpassen – sie hat beim Export Vorrang.

## Start

1. `DiariumBookStudio.sln` in Visual Studio öffnen.
2. Projekt `DiariumBookStudio.App` starten.
3. `Datei -> Diarium-ZIP importieren...` wählen.
4. Einträge prüfen und Bilder auswählen.
5. `Datei -> LaTeX erzeugen unter...` wählen.

## LuaLaTeX

Die erzeugte Datei `book.tex` wird mit LuaLaTeX kompiliert, zum Beispiel:

```powershell
cd "C:\Pfad\zum\Exportordner"
lualatex book.tex
lualatex book.tex
```

LuaLaTeX ist Teil von MiKTeX und TeX Live.


## Version 6

Änderungen gegenüber v5:

- Karten werden aus Diarium-Koordinaten zuverlässiger erkannt, auch wenn der Ort als `📍 50.x, 11.x` exportiert wurde.
- Der LaTeX-Export lädt für Einträge mit Koordinaten einen statischen OpenStreetMap-Ausschnitt und setzt ihn rechts oben neben die Metadaten.
- Die Bildgalerie verwendet jetzt eine kompakte 3-Spalten-Kacheloptik wie in der Diarium-HTML-Ansicht.
- Die Abstände zwischen Bildern wurden reduziert.

Hinweis zur Karte: Beim Erzeugen der LaTeX-Datei wird Internetzugang benötigt, damit die OpenStreetMap-Karte als PNG in den `images`-Ordner geladen werden kann. Falls Firmen-Proxy oder Firewall den Abruf blockieren, erscheint keine Karte.

## Version 7

Änderungen gegenüber v6:

- Die Karte wird nicht mehr über einen Static-Map-Dienst geladen.
- Stattdessen lädt die App direkt OpenStreetMap-Kacheln wie Leaflet und setzt daraus ein kleines PNG zusammen.
- Dadurch erscheint der Kartenausschnitt zuverlässiger rechts oben im PDF.
- Die Galerie bleibt als kompakte 3-Spalten-Kacheloptik.


## Version 8

Änderungen gegenüber v7:

- Der Kartenexport schreibt jetzt eine Diagnose-Datei `map_debug.txt` in den Exportordner.
- Wenn OpenStreetMap-Kacheln nicht geladen werden können, wird trotzdem ein sichtbarer Platzhalter mit Koordinaten erzeugt.
- Der Export probiert mehrere OSM-Tile-URLs.
- Die Karte sollte dadurch entweder sichtbar sein oder der Grund ist in `map_debug.txt` nachvollziehbar.


## Version 9

Änderungen gegenüber v8:

- Neue Symbolleiste mit Import, Öffnen, Speichern, LaTeX, PDF-Erzeugung und Ausgabeordner.
- Neuer Button „PDF erzeugen & öffnen“: erzeugt `book.tex`, startet `lualatex` zweimal und öffnet danach `book.pdf`.
- Bei LuaLaTeX-Fehlern wird `lualatex_error.txt` im Ausgabeordner geschrieben.
- `map_debug.txt` wird jetzt bei jedem Export sofort angelegt, auch wenn keine Koordinaten erkannt werden.


## Version 10 (2026-07-01)

- Sichtbare Symbolleiste: jetzt 32 px hoch, hellgrauer Hintergrund.
- Version wird rechts in der Statusleiste angezeigt („DiariumBookStudio v10“).
- Symbolleiste steht ganz oben und sollte in jeder Windows-11- oder Windows-10-Skalierung sichtbar sein.


## Version 11

- Projektdatei korrigiert: Standard-SDK mit `<UseWPF>true</UseWPF>`, aber App.xaml und MainWindow.xaml explizit als WPF-XAML eingebunden.
- Dadurch werden `InitializeComponent`, `EntryTree`, `StatusText` usw. zuverlässig generiert.
- Fehlende WPF-Using-Direktiven in `LatexExporter.cs` ergänzt (`System.Windows`, `System.Windows.Media`, `System.Windows.Media.Imaging`).
- Versionsanzeige auf `DiariumBookStudio v11` gesetzt.


## Version 12

- Behebt die letzten beiden Compilefehler zu `DrawingContext` und `DrawingVisual`.
- Die beiden Typen werden in `LatexExporter.cs` jetzt vollständig qualifiziert (`System.Windows.Media.DrawingContext` / `System.Windows.Media.DrawingVisual`).


## Version 13 (2026-07-01)

- Metadaten im PDF überarbeitet: GPS-Koordinaten werden nicht mehr angezeigt, Wetter wird nicht mehr doppelt ausgegeben.
- Rechts oben ist mehr Abstand zwischen Kartenblock und Textblock.
- Seitenzahlen jetzt mittig als „Seite x von y“ im Fußbereich.
- Bildlayout verbessert: einzelne Bilder werden kleiner gesetzt, Galerien kompakter.
- In der App gibt es jetzt zusätzlich ein explizites Flag „Hauptbild“.


## Version 14 (2026-07-01)

- Behebt den Compilefehler „Ungültiges Token }“ / „} erwartet“ in `MainWindow.xaml.cs`.
- Die Hauptbild- und Metadaten-Methoden liegen jetzt wieder innerhalb der `MainWindow`-Klasse.


## Version 15 (2026-07-01)

- Behebt den LuaLaTeX-Fehler `I can't write on file book.pdf`.
- Jede PDF-Erzeugung verwendet nun einen eindeutigen Dateinamen, z. B. `book_20260701_153012.pdf`.
- Dadurch kann eine alte PDF im Viewer geöffnet bleiben, ohne die neue PDF-Erzeugung zu blockieren.


## Version 16 (2026-07-01)

- Während der PDF-Erzeugung zeigt die Statusleiste jetzt eine Fortschrittsanzeige an.
- Die PDF-Erzeugung blockiert die Oberfläche während der LuaLaTeX-Läufe nicht mehr vollständig.
- Der Status zeigt die Schritte „LaTeX/Karten“, „LuaLaTeX Lauf 1“ und „LuaLaTeX Lauf 2“.
- Behebt den Layoutfehler, bei dem Text rechts neben der Karte weiterlief. Nach dem Kartenblock wird nun ein harter Absatz gesetzt.


## Version 17 (2026-07-01)

- Behebt die Compilefehler aus v16:
  - `Dispatcher.Yield()` wird nun statisch über `System.Windows.Threading.Dispatcher.Yield()` aufgerufen.
  - `System.Windows.Input` ist eingebunden, damit `Mouse`/`Cursors` erkannt werden.
- Fortschrittsanzeige und Karten-/Text-Layout-Korrektur aus v16 bleiben enthalten.


## Version 18 (2026-07-01)

- Behebt den restlichen Compilefehler `Der Name "Mouse" ist im aktuellen Kontext nicht vorhanden`.
- `Mouse.OverrideCursor` wird nun vollständig qualifiziert als `System.Windows.Input.Mouse.OverrideCursor`.


## Version 19 (2026-07-01)

- Behebt den fehlerhaften Umbruch neben der Karte: Nach dem Metadaten-/Kartenblock wird jetzt ein harter neuer Absatz (`\par\medskip\noindent`) erzwungen.
- Der eigentliche Tagebuchtext startet nun immer explizit in einer neuen Zeile unterhalb des Kartenblocks.
- Wetterangaben werden robuster bereinigt, sodass `Wetter: Wetter: ...` nicht mehr vorkommt.


## Version 20 (2026-07-01)

- Eintragskopf stärker wie im Diarium-HTML-Export:
  - oben Datum mit Kalender-Symbol
  - darunter Überschrift
  - rechts daneben Karte
  - Wetter/Temperatur links unter dem Titel
  - GPS-Koordinaten werden nicht mehr im PDF angezeigt
- Bildlayout vereinfacht:
  - kein Hauptbild mehr nötig
  - alle ausgewählten Bilder werden gleich groß in einer 3-Spalten-Galerie gesetzt
- Die Hauptbild-Checkbox wurde aus der App entfernt.


## Version 21 (2026-10-02)

Fehlerbehebungen:

- Erneuter Import einer ZIP überschreibt keine Bearbeitungen mehr (Text, Prüfstatus,
  Bildauswahl, Bildunterschriften, Ortsnamen werden über einen Eintragsschlüssel übernommen).
- Mehrere Einträge am selben Tag überschreiben sich nicht mehr gegenseitig Bilder und Karten.
- Emoji im PDF: Metadaten-Symbole sind jetzt Font-Awesome-Icons, Emoji im Text werden über
  „Segoe UI Emoji“ gesetzt (vorher fehlten sie stillschweigend).
- LaTeX-Escaping in einem Durchlauf (Backslash wurde vorher zerstört); `"` wird zu „…“.
- Formatierungen (fett, kursiv, Listen) und Zeilenumbrüche aus Diarium bleiben erhalten.
- Datum wird auch bei englischer Diarium-Sprache bzw. aus dem Medienordner erkannt.
- Bildpfade werden relativ gespeichert; Buchtitel wird aus dem Zeitraum vorgeschlagen.
- Export läuft im Hintergrund, die Oberfläche friert bei Kartendownloads nicht mehr ein.

Neues Layout (`Templates/diarybook.sty`):

- EB Garamond (Fallback Libertinus), Erstzeileneinzug, Initiale am Eintragsanfang,
  Monat als Kolumnentitel, Seitenzahl außen.
- Eintragskopf: Datum in Kapitälchen, Titel, Zeile mit Ort, Wetter, Bewertung und Tags;
  Karte mit abgerundeten Ecken rechts daneben.
- Monatsseiten mit Statistik (Einträge, Fotos, Orte) und eigene Titelseite.
- Fotos ohne Beschnitt in Blocksatz-Reihen (Zeilen füllen exakt die Satzbreite),
  drei Bilder als großes Bild plus zwei kleine, optional Titelbild über dem Text.
- EXIF-Drehung wird berücksichtigt; nicht LaTeX-taugliche Formate werden nach JPEG umgewandelt.

Karten und Orte:

- Karten in doppelter Auflösung, Kachel-Cache unter `cache/tiles` im Projektordner,
  OSM-konformer User-Agent statt gefälschtem Referer.
- Ortsnamen (z. B. „Gera“) werden per Nominatim ermittelt, im Projekt gespeichert und
  sind in der App editierbar.

Druck:

- Option „Druckversion“: 3 mm Beschnittzugabe, TrimBox/BleedBox im PDF,
  Seitenzahl wird auf ein Vielfaches von 4 aufgefüllt.


## Version 22 (2026-10-02)

Jahresbuch:

- Jahresauswahl in der Kopfleiste: Baum und Buch enthalten nur die Einträge des gewählten Jahres.
  Bei einem Export vom 1.1. bis 31.12. wird „Tagebuch <Jahr>“ als Titel vorgeschlagen.
- Import in das geöffnete Projekt: Ein neuer Export (z. B. später im Jahr mit längerem Zeitraum)
  aktualisiert das bestehende Projekt, Bearbeitungen bleiben erhalten – unabhängig vom Dateinamen.
- Exporte mit mehreren HTML-Dateien werden vollständig eingelesen.

Import:

- „Entpackten Diarium-Ordner importieren“ (Menü und Symbolleiste) zusätzlich zur ZIP.

Druckkosten:

- Option „Akzente in Graustufen“: Datum, Symbole, Linien und Karten in Grau. Seiten ohne Fotos
  enthalten dann keine Farbe und werden z. B. bei epubli als Schwarz-Weiß-Seiten berechnet.
  Text und Linien sind jetzt immer als echte Graustufen (DeviceGray) definiert.

Fehlerbehebungen:

- LuaLaTeX brach mit „Incomplete \ifdim“ ab, wenn auf einen Eintrag ohne Text und ohne Fotos
  ein Eintrag folgte, der auf eine neue Seite umbrechen musste.
- Verständliche Fehlermeldung bei LuaLaTeX-Fehlern: eigentliche Fehlerzeile, betroffener
  Eintrag (Datum und Titel) und Hinweise für bekannte Fälle (PDF im Viewer geöffnet, Datei fehlt,
  LuaLaTeX nicht installiert).
- Einträge nur mit Titel (ohne Text und Fotos) erscheinen als einzeilige Kurznotiz;
  Einträge ganz ohne Inhalt werden ausgelassen (Hinweis in `map_debug.txt`).
- Keine Initiale bei sehr kurzen Texten (sie ragte sonst in die Fotos).
- Vorschaubilder in der App werden verkleinert geladen und die Bilderliste ist virtualisiert –
  wichtig bei Einträgen mit über 100 Fotos.


## Version 23 (2026-11-04)

- Autospeichern: Änderungen werden nach spätestens 5 Sekunden gespeichert, außerdem beim Beenden
  und vor dem Öffnen/Importieren eines anderen Projekts. Schlägt das Speichern fehl, fragt die App nach.
- Projektdatei wird über eine temporäre Datei geschrieben und kann bei einem Abbruch nicht beschädigt werden.
- Strg+S speichert das Projekt.
- Rechtschreibprüfung (Deutsch) im Tagebuchtext und in den Bildunterschriften; Korrekturvorschläge per Rechtsklick.
- Formatierung: Strg+B / Strg+I bzw. Knöpfe „F“ und „K“ setzen oder entfernen **fett** und *kursiv*,
  „• Liste“ macht markierte Zeilen zur Liste.


## Version 24 (2026-11-04)

Vorabprüfung vor dem Druck:

- Neuer Knopf „Prüfen“: erzeugt `book.tex` (ohne LuaLaTeX) und listet alle Auffälligkeiten auf.
- „PDF erzeugen“ prüft automatisch und fragt bei Fehlern oder Warnungen nach, bevor LuaLaTeX startet.
  Reine Hinweise erscheinen nur in der Statuszeile.
- Geprüft wird:
  - Fehler: ausgewähltes Foto fehlt oder kann nicht gelesen werden (würde im Buch fehlen).
  - Auflösung in der tatsächlichen Druckgröße (aus dem Galerie-Layout berechnet):
    unter 120 dpi Warnung („deutlich unscharf“), unter 200 dpi Hinweis.
  - Hinweis: Eintrag noch nicht als geprüft markiert.
- Doppelklick auf eine Zeile (oder „Zum Eintrag“) öffnet den betroffenen Eintrag in der App.


## Version 25 (2026-11-04)

Druckerei-Profil und Buchumschlag:

- Neues Druckprofil **Lulu · A5 Taschenbuch · Premium-Farbe** (Voreinstellung, Produktcode
  `0583X0827FCPREPB080CW444MXX`). Vorgaben aus dem Lulu Book Creation Guide: Beschnitt 3,175 mm,
  Sicherheitsabstand 12,7 mm, 32–800 Seiten, Rückenbreite = Seiten / 444 + 0,06 in, Bundsteg nach Seitenzahl.
- „Druckversion mit Umschlag“ erzeugt zusätzlich zum Innenteil `cover_….pdf`: Rückseite, Rücken und
  Vorderseite als ein Blatt mit Beschnitt, Rückenbreite aus der tatsächlichen Seitenzahl.
  Layout in `Templates/diarycover.sty` (eigene Version im Projektordner hat Vorrang).
- Umschlagfoto per Knopf „Als Umschlagfoto“ am Bild; sonst automatisch das erste Titelbild bzw. erste Foto.
  Rückentext erst ab 81 Seiten (Lulu-Empfehlung).
- Innenteil der Druckversion: Kopfzeile und Seitenzahl innerhalb des Sicherheitsabstands, Innenrand
  wächst mit der Seitenzahl (z. B. 25,4 mm bei 151–400 Seiten). Passt der Innenrand nach dem ersten Satz
  nicht zur Seitenzahl, setzt die App das Buch automatisch mit angepasstem Rand neu.
  Die Maße stehen in `bookgeometry.tex` (wird von `book.tex` eingebunden).
- Fotos über 600 ppi in Druckgröße werden auf 450 ppi verkleinert (Lulu: höchstens 600 ppi);
  die verkleinerten Bilder werden zwischengespeichert. Bildmaße werden nur noch aus dem Dateikopf gelesen.
- Nach der Erzeugung zeigt die App Seitenzahl, Umschlagmaße und Rückenbreite sowie Probleme
  (z. B. Seitenzahl außerhalb der Grenzen der Druckerei).


## Version 26 (2026-11-04)

- Neuer Knopf „Druckpaket“: schaltet bei Bedarf die Druckversion ein, erzeugt Innenteil und Umschlag
  und legt beides mit sprechenden Namen in `output/Druckpaket_<Titel>_<Datum>/`.
  Dazu kommt `Druckinfo.txt` mit Produkt, Produktcode, Seitenzahl, Maßen, Schritten zum Hochladen
  und Bestellen bei der Druckerei sowie offenen Hinweisen (z. B. ungeprüfte Einträge).
- Die PDFs werden in den Paketordner verschoben, nicht kopiert (sie sind oft über 100 MB groß).


## Version 27 (2026-11-04)

Weniger Handarbeit pro Jahr:

- Assistent **„Jahresbuch“** (Symbolleiste bzw. Menü „Buch“): Jahr, Titel, Untertitel und Druckerei wählen,
  optional Fotos automatisch auswählen und alle Einträge als geprüft markieren – danach entsteht das Druckpaket.
  Vorschlag: das letzte abgeschlossene Jahr, Titel „Tagebuch <Jahr>“.
- **Fotos automatisch auswählen** (Menü „Buch“) für alle ungeprüften Einträge, höchstens 6 je Eintrag:
  - Diarium exportiert die Fotos nicht in Aufnahmereihenfolge; sie wird über die Kamera-Bildnummer
    (`IMG_1234`) wiederhergestellt, die Fotos erscheinen so auch im Buch chronologisch.
  - Fast gleiche Fotos (Serienbilder, Duplikate) werden über einen Bild-Fingerabdruck erkannt und
    zusammengefasst; es bleibt das detailreichste. Fingerabdrücke werden im Projekt gespeichert.
  - Der Rest wird gleichmäßig über den Tag verteilt; Titelbilder bleiben, geprüfte Einträge unverändert.
- **Alle Einträge als geprüft markieren** (Menü „Buch“).
- Testprojekt `DiariumBookStudio.Tests` (xUnit, `dotnet test`): Textumwandlung, Galerie-Layout,
  Druckprofil (Rückenbreite, Bundsteg, Umschlag, Seitenzahlgrenzen), Fotoauswahl, Vorabprüfung.


## Version 28 (2026-11-04)

- **A4-Format**: neues Profil „Lulu · A4 Taschenbuch · Premium-Farbe“ (Voreinstellung für neue Projekte),
  12 pt Schrift und breitere Ränder; A5 bleibt wählbar. Umschlagschriften wachsen mit dem Format.
  Lese- und Druckversion verwenden jetzt denselben Satzspiegel – Vorschau und Druck brechen gleich um.
- **Vorschau** (👁 bzw. Buch → Vorschau): setzt den ausgewählten Eintrag oder seinen ganzen Monat und
  zeigt die Seiten als Doppelseiten mit Zoom. Angezeigt wird, welche Einträge auf welcher Seite beginnen,
  wie viele Einträge im Schnitt auf eine Seite passen; Doppelklick auf eine Seite öffnet den Eintrag.
  Seitenmarken (`\DiaryEntryKey`) schreibt `diarybook.sty` ins LuaLaTeX-Protokoll; gerendert wird mit
  `pdftoppm` aus MiKTeX.
- **Einstellungen** (Datei → Einstellungen, gespeichert in `%APPDATA%\DiariumBookStudio\settings.json`):
  Projektordner für neue Projekte, Ordner für Druckpakete, Pfad zu lualatex.exe (mit „Prüfen“),
  Format und Fotos je Eintrag für neue Projekte, zuletzt geöffnetes Projekt beim Start öffnen.
- **Hilfe** (F1 bzw. Hilfe → Anleitung): Ablauf, Bearbeitung, Tastenkürzel, Fotos, Vorschau, Formate,
  Einstellungen, Ordner, Layout anpassen, Fehlerbehebung.

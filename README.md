# Diarium Book Studio

Kleiner WPF-Prototyp für Visual Studio, um einen Diarium-Export in eine Buchfassung zu überführen.

## Aktueller Funktionsumfang

- Diarium-ZIP importieren
- Einträge nach Monaten als Baum anzeigen
- bereits geprüfte Einträge grün markieren
- Tagebuchtext bearbeiten, ohne Diarium zu verändern
- Bilder auswählen, sortieren und Rollen vergeben: Hauptbild, Galerie, Klein
- Bildunterschriften pflegen
- Projekt als JSON speichern
- LaTeX-Datei in frei wählbaren Zielpfad exportieren

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

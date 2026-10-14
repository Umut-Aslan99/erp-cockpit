# 0003 · Import: tolerant lesen, streng prüfen, nur Gültiges übernehmen, alles zurücknehmbar

**Status:** angenommen

## Kontext
Echte ERP-Exporte sind uneinheitlich: Semikolon oder Komma, UTF-8 oder Windows-1252, „1.234,56“ oder „1,234.56“,
Spaltennamen wie „ArtNr“, „Material“ oder „Artikelnumer“ (Tippfehler).

## Entscheidung
1. **Lesen** (`Erp.Core/TabularFile.cs`): Trennzeichen aus der Kopfzeile, UTF-8 nur wenn die Bytes gültig sind, sonst
   Windows-1252; Excel-Zellen werden typgerecht in kanonische Texte umgewandelt.
2. **Zahlenformat je Spalte**, nicht je Wert: „1.234“ allein ist mehrdeutig, eindeutige Werte wie „12,5“ entscheiden.
3. **Spalten zuordnen** (`ColumnMatcher`): Synonyme aus realen Exporten, dann Teilstring, dann Editierdistanz;
   jede Spalte höchstens einmal; der Benutzer bestätigt oder korrigiert.
4. **Prüfen** (`Validator`): Typen, Pflichtfelder, Verweise (Artikel/Lieferant bekannt), Duplikate, Datumslogik;
   Fehler mit Excel-Zeilennummer und verständlicher Meldung.
5. **Übernehmen**: nur gültige Zeilen; Stammdaten und Auftragspositionen per Upsert über den fachlichen Schlüssel,
   Bewegungen anhängend. Jeder Import schreibt ein Undo-Log (eingefügte Schlüssel + vorherige Werte).
6. **Zurücknehmen**: nur der jeweils letzte Import (LIFO), damit keine Verweise ins Leere zeigen.

## Konsequenzen
+ Ein fehlerhafter Datensatz blockiert nicht den ganzen Import.
+ Rücknahme stellt auch geänderte Preise exakt wieder her (getestet).
− LIFO ist eine bewusste Einschränkung; ältere Importe lassen sich erst nach den jüngeren zurücknehmen.

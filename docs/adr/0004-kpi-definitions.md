# 0004 · Kennzahlen wie im Controlling definiert

**Status:** angenommen

| Kennzahl | Definition |
|---|---|
| Lagerwert | Bestand (Summe aller Bewegungen) × Einstandspreis |
| Lagerumschlag | Abgänge der letzten 12 Monate zu Einstandspreisen ÷ aktueller Lagerwert |
| Reichweite | Bestand ÷ durchschnittlicher Tagesverbrauch der letzten 90 Tage |
| Ladenhüter | Bestand > 0, aber seit über 180 Tagen kein Abgang |
| Auftragsrückstand | offene Auftragspositionen (ohne Lieferdatum) × Verkaufspreis; überfällig = Liefertermin vor heute |
| Liefertreue | Anteil der Wareneingänge (12 Monate), die spätestens zum bestätigten Termin kamen |

Aggregationen über viele Zeilen (Bestand, Abgänge, Monatswerte) rechnet PostgreSQL (gefilterte Summen, Joins);
Zusammenführung und Kennzahlen pro Artikel/Lieferant laufen im Speicher – ein Arbeitsbereich umfasst rund
20.000 Zeilen. Bei größeren Datenmengen würden die Kennzahlen materialisiert.

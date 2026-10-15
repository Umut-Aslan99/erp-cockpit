// Guided tour of the ERP-Cockpit – texts here, mechanics in site-tour.js (shared by all demos of umut-aslan.dev).
import { startTours } from '/site-tour.js';

startTours([{
  id: 'erp',
  steps: [
    {
      title: 'Hallo! Kurz zum Problem',
      mood: 'worried',
      text: `Kunden liefern ihre Daten als **ERP-Export**: CSV aus dem Alt-System, Excel aus SAP, Listen vom Lieferantenportal.
Ausgewertet wird von Hand – kopieren, sortieren, SVERWEIS. **Jede Woche neu.**

Fehler in den Daten fallen erst auf, wenn im Meeting eine Zahl nicht stimmt.`,
      nextLabel: 'Zeig mir die Lösung',
    },
    {
      target: '[data-tour="kpis"]',
      title: 'Die wichtigsten Zahlen – sofort',
      text: `Aus den Rohdaten berechnet: **Lagerwert, Lagerumschlag, Ladenhüter, überfälliger Rückstand und Liefertreue**.

Jede Kachel ist ein Link – ein Klick führt zur Tabelle mit den Zeilen dahinter.`,
    },
    {
      target: '[data-tour="trend"]',
      title: 'Diagramme, die man prüfen kann',
      text: `Mit der Maus über das Diagramm fahren zeigt die Werte. **„Tabelle“** schaltet auf dieselben Zahlen als Liste um –
nachprüfbar und barrierefrei.`,
    },
    {
      target: '[data-tour="suppliers"]',
      title: 'Probleme springen ins Auge',
      mood: 'curious',
      text: `Ein Lieferant liegt deutlich unter dem Ziel von 90 % pünktlicher Lieferungen.

Ein Klick auf seinen Balken zeigt die überfälligen Bestellungen – vom Überblick zum Detail in einem Schritt.`,
    },
    {
      path: '/lager',
      target: '[data-tour="stock-filters"]',
      title: 'Drill-down durch alle Daten',
      text: `Suchen, filtern, sortieren – auch bei Tausenden Zeilen schnell, weil der Server blättert und nicht der Browser.

Die Filter stehen in der Adresse: Links lassen sich weitergeben.`,
    },
    {
      target: '[data-tour="excel-export"]',
      title: 'Excel bleibt – als Export',
      text: `Jede gefilterte Tabelle gibt es als **echte .xlsx-Datei** – für alle, die weiter in Excel arbeiten wollen.
Der Unterschied: Die Zahlen kommen jetzt aus einer Quelle.`,
    },
    {
      path: '/import',
      target: '[data-tour="samples"]',
      title: 'Der Import – mit echten Problemfällen',
      text: `Keine Datei zur Hand? Vier Beispieldateien aus typischen Quellen: Alt-ERP mit Semikolon und Umlauten,
SAP-Excel, englische Zahlen, neue Aufträge. **Jede enthält absichtlich fehlerhafte Zeilen.**`,
    },
    {
      target: '[data-tour="import-steps"]',
      title: 'Vier Schritte statt Copy & Paste',
      text: `- **Datei** – Trennzeichen, Zeichensatz und Zahlenformat werden erkannt
- **Spalten** – automatisch zugeordnet, auch bei Tippfehlern
- **Prüfen** – Fehlerbericht für jede Zeile
- **Übernehmen** – nur gültige Zeilen; jeder Import lässt sich zurücknehmen`,
    },
    {
      target: '[data-tour="workspace"]',
      title: 'Ihr eigener Arbeitsbereich',
      text: `Importieren Sie, was Sie wollen: Andere Besucher sehen nichts davon. „Zurücksetzen“ stellt die Demo-Daten wieder her.`,
    },
    {
      title: 'Warum das hilft',
      mood: 'celebrate',
      text: `- Aus Stunden Excel-Arbeit pro Woche werden **Sekunden**
- Datenfehler fallen **beim Import** auf, nicht im Meeting
- Alle sehen **dieselben Zahlen**, jede Zahl ist nachprüfbar

Gebaut mit React, ASP.NET Core und PostgreSQL, abgesichert durch 67 automatisierte Tests.
Probieren Sie den Import mit einer Beispieldatei – über den runden Knopf unten links starten Sie diese Tour jederzeit neu.`,
      nextLabel: 'Selbst ausprobieren',
    },
  ],
}]);

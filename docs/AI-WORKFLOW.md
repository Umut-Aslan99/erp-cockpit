# Wie KI in diesem Projekt eingesetzt wurde

Gebaut mit einem KI-Coding-Agenten (Claude Code). Vorgegeben waren Problem (ERP-Exporte auswerten), Zielgruppe
(Maschinenbau-Mittelstand), Stack (React + C#/.NET + PostgreSQL) und Gestaltung (wie umut-aslan.dev).

Arbeitsweise: Kernlogik zuerst und ohne Datenbank testbar → API mit Integrationstests gegen echte PostgreSQL →
Oberfläche → End-to-End-Tests im Browser → Auslieferung erst, wenn alles grün ist.

**Was die Tests und Prüfungen gefunden haben:**
- Der Datengenerator buchte Abgänge manchmal vor dem Wareneingang derselben Woche – chronologisch negativer Bestand.
  Gefunden durch den Test „Bestand nie negativ“.
- Das Template nutzte `Guid.Empty` als Schlüssel; EF Core hält diesen Wert für „nicht gesetzt“ und speicherte nichts.
- „1.234“ ist in einem deutschen Export tausend, in einem englischen 1,234 – deshalb wird das Zahlenformat je Spalte
  bestimmt und nicht je Wert (eigene Tests für beide Fälle).
- Nach dem ersten Lauf waren 37 % der offenen Aufträge überfällig – unrealistisch. Die Verteilung wurde angepasst
  (jetzt rund 15–25 %), weil eine Demo glaubwürdige Zahlen braucht.

Strenge Werkzeuge: `TreatWarningsAsErrors` mit .NET-Analyzern, TypeScript strict, Tests in der Zeitzone Europe/Berlin.
Jede Änderung wird gelesen und verstanden, bevor sie bleibt.

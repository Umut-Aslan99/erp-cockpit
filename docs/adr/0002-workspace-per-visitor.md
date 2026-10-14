# 0002 · Ein privater Arbeitsbereich je Besucher

**Status:** angenommen

## Kontext
Eine öffentliche Demo, in der jeder Daten importieren und zurücknehmen kann, wäre mit gemeinsamen Daten sofort
unbrauchbar: Besucher würden sich gegenseitig die Kennzahlen verändern.

## Entscheidung
- Jede Zeile trägt eine `WorkspaceId`; der Besucher wird über ein HttpOnly-Cookie (`erp_ws`, SameSite=Strict) zugeordnet.
- Neue Arbeitsbereiche entstehen als Kopie eines Template-Arbeitsbereichs **innerhalb von PostgreSQL**
  (`INSERT … SELECT`, ~20.000 Zeilen in Millisekunden).
- Das Template wird täglich neu erzeugt (deterministischer Generator), damit die Daten immer „bis heute“ reichen.
- Ungenutzte Arbeitsbereiche verschwinden nach 24 Stunden, höchstens 100 gleichzeitig; Löschen per Fremdschlüssel-Kaskade.

## Konsequenzen
+ Besucher können alles ausprobieren, ohne andere zu stören.
+ Gleiches Muster wie Mandantenfähigkeit in echten Anwendungen.
− Jede Abfrage muss nach `WorkspaceId` filtern – durch Tests abgesichert (isolierte Arbeitsbereiche).

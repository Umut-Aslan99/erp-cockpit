# 0001 · Ein Container: ASP.NET Core liefert API und React-Build aus

**Status:** angenommen

## Kontext
Ein einziger Client (die React-App), keine externen API-Konsumenten, keine Benutzerkonten – der Cookie identifiziert
nur den Demo-Arbeitsbereich. Der Server hat begrenzten Arbeitsspeicher.

## Entscheidung
Der React-Build liegt im `wwwroot` der ASP.NET-Core-Anwendung. Ein Prozess, ein Origin: kein CORS, keine zweite
Laufzeitumgebung, Cookie mit `SameSite=Strict`. Fingerprinted Assets werden ein Jahr gecacht, `index.html` nie.

## Abgrenzung zu Einsatz (svelte.umut-aslan.dev)
Dort gibt es zwei sehr unterschiedliche Clients und eine echte Anmeldung – dort lohnt sich ein Backend-for-Frontend.
Hier wäre er eine zusätzliche Schicht ohne Aufgabe.

## Konsequenzen
+ Ein Image, ein Deployment, ~150 MB RAM.
− Frontend und Backend werden gemeinsam ausgeliefert (für dieses Produkt gewollt).

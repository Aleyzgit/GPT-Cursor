# GPT Cursor

Windows-Anwendung mit dem schwarz-weißen Computer-Use-Cursor aus der lokal installierten ChatGPT-/Codex-App, ohne den blauen Glow.

## Starten

`dist\GPT Cursor\GPT Cursor.exe` öffnen und **Enable cursor** anklicken. Englisch ist die Standardsprache; unter **Language** lässt sich jederzeit auf **Deutsch** umstellen. Die Sprache wird gespeichert.

- **Ctrl + Alt + C** schaltet den Effekt ein und aus. Auf die dargestellte Tastenkombination klicken und ein neues Kürzel drücken; Esc bricht ab. Erlaubt sind Strg oder Alt (optional mit Umschalt) plus Buchstabe, Ziffer oder F1–F11. Bei einem belegten Kürzel bleibt das bisherige aktiv. Der Shortcut ist separat abschaltbar.
- Größe von 24 bis 64 Pixeln; Drehung, Dehnung, Stauchung und Nachwippen einzeln abschaltbar. „Bewegungsanimation“ schaltet alle vier Bewegungseffekte gemeinsam aus, ohne die einzelnen Einstellungen zu verlieren.
- Links- und Rechtsklick-Animation separat schaltbar: kurzes Einfedern, auslaufender neutraler Ring beim Linksklick, Doppelring beim Rechtsklick. Einfedern und Ringe sind ebenfalls einzeln schaltbar und funktionieren auch bei ausgeschalteter Bewegungsanimation.
- Bildrate wählbar: 60, 120, 144, 165, **240** (Standard) oder 360 FPS. Die Auswahl sowie alle Effekte und die Größe werden in `settings.json` neben der EXE gespeichert.
- Die Vorschau unterstützt Links- und Rechtsklick auch bei pausiertem Cursor.
- **Show click rings (both buttons)** / **Klickringe anzeigen (beide Tasten)** blendet beide Klickringe aus, ohne das Einfedern auszuschalten.
- **Smooth position** / **Position glätten** und **Smooth animation** / **Animation glätten** sind unabhängig schaltbar. Jeder Schalter besitzt eine eigene Auswahl aus **Sine easing**, **Soft spring** und **Responsive**. Die Positionsglättung bleibt auch ohne Bewegungsverformung nutzbar.
- Standardmäßig werden normaler Pfeil und Link-Hand ersetzt. Optional auch Text-, Lade- und Größenzeiger.
- **In den Infobereich** lässt die App neben der Uhr weiterlaufen.
- Schließen oder **Beenden und Cursor wiederherstellen** beendet die App und blendet den Windows-/MouseX-Zeiger wieder ein.
- Ein unabhängiger Wiederherstellungsprozess blendet den Systemzeiger auch nach einem Absturz des Hauptprozesses wieder ein.
- Die Animation läuft als klickdurchlässiges Windows-Overlay. `MagShowSystemCursor` blendet den darunterliegenden Systemzeiger aus; dessen Bild und Skin bleiben unverändert. MouseX kann geöffnet bleiben.
- Optionaler Autostart, keine Adminrechte, keine Netzwerkverbindung im Betrieb und keine Änderung des gespeicherten Windows-Cursorschemas.

Der Setup-Installer enthält die **.NET 10 Desktop Runtime (x64)**. Die kleine portable Ausgabe unter `dist\GPT Cursor` setzt eine installierte Runtime voraus; sie ist auf diesem PC vorhanden. Für die einfache Weitergabe den Setup-Installer verwenden.

## Animation und Genauigkeit

Das Cursorbild stammt direkt aus dem mitgelieferten App-Bundle. Die Federintegration, Drehung, Stauchung und das Nachwippen verwenden die dort gefundenen Parameter. Der blaue `drop-shadow`-Filter wird nicht übernommen.

Die ursprüngliche App bewegt einen virtuellen Agent-Cursor zwischen einzelnen Zielpunkten, bei weiten Sprüngen entlang berechneter Bézierkurven. Diese Software passt das Verhalten an kontinuierliche physische Mausbewegung an: Die sichtbare Spitze folgt dem echten Windows-Klickpunkt; Bewegungsrichtung und Geschwindigkeit steuern die Verformung. Das Overlay erhält bei 240 FPS alle etwa 4,17 ms ein Update über einen hochauflösenden Windows-Timer. Veraltete Updates werden nicht aufgestaut. Die tatsächliche sichtbare Bildrate hängt auch von Windows, Systemlast und Monitor ab; das Overlay ist nicht mit dem Monitor-VSync synchronisiert. Die Einstellungs-Vorschau läuft ressourcenschonend mit höchstens 60 FPS. Es ist keine identische Wiedergabe der autonomen Bewegungspfade. Die App bewegt die Maus selbst nicht.

Die Klickanimation wurde für dieses Programm ergänzt; sie stammt nicht aus dem OpenAI-Cursor. Ein beobachtender Maus-Hook erfasst kurze Tastendrücke und reicht die Eingaben unverändert weiter. Er ist nur bei aktiviertem Cursor installiert. Beide Klickarten können sich überlagern und laufen nach 420 ms aus; die Pfeilspitze bleibt auch beim Einfedern am tatsächlichen Klickpunkt.

Bei aktivierter Positionsglättung folgt der sichtbare Pfeil der echten Maus leicht verzögert. **Sine easing** nutzt ein Sinusfenster von 32 ms (für Verformungen 65 ms); **Soft spring** eine kritisch gedämpfte Feder; **Responsive** zwei schnelle exponentielle Filterstufen. Alle drei glätten Anfahren und Abbremsen. Beim Klicken und während des Ziehens wird die sichtbare Position direkt synchronisiert; der Windows-Klickpunkt wird nie verändert. Hover-Ziele richten sich weiterhin nach der echten Mausposition. Ohne Positionsglättung folgt die sichtbare Spitze sofort. Der bisherige Absatz zur direkten Position gilt für diesen ausgeschalteten Zustand bzw. beim Klicken und Ziehen.

Programme mit eigenen Cursorbildern, Spiele mit Hardware-/Software-Cursorn und geschützte Windows-Oberflächen können ihre eigenen Zeiger anzeigen. Die Pixelgröße wird explizit gewählt; sie wird nicht pro Monitor automatisch verändert.

## Bauen und prüfen

`--frame-test <Ordner>` misst fünf Sekunden lang die Update-Abstände bei 240 FPS mit laufender Verformung und Klickanimation. Das Ergebnis misst Overlay-Updates, nicht den physischen Bildaufbau des Monitors.

```powershell
dotnet build -c Release
dotnet publish -c Release --no-self-contained -o 'dist\GPT Cursor'
& '.\dist\GPT Cursor\GPT Cursor.exe' --self-test test-output
```

`--self-test` prüft Animation, neutrale Farben, Cursorerzeugung und Klickpunkt, ohne Systemzeiger auszutauschen. `--overlay-test <Ordner>` prüft 500 Overlay-Bilder, Ressourcen und Fokusverhalten. `--smoke-test <Datei>` aktiviert kurz und stellt wieder her; dieser Test benötigt eine interaktive Windows-Sitzung. `--snapshot <Ordner>` speichert die aktuellen Systemzeiger für einen Vorher-/Nachhervergleich. `--activate` aktiviert direkt nach dem Öffnen. `--restore` lädt als manuelle Rückfalloption das gespeicherte Windows-Cursorschema, sofern keine andere Instanz dieser App läuft.

MouseX kann mit angewendetem Skin geöffnet bleiben. GPT Cursor verändert dessen Cursorbilder nicht mehr. Beim Ausschalten wird der aktuelle MouseX-/Windows-Zeiger sichtbar. Die API aus `Magnification.dll` wird ausschließlich zur Cursor-Sichtbarkeit verwendet; es wird keine Bildschirmvergrößerung aktiviert. Spezialcursor werden soweit an ihren Windows-Kennungen erkennbar beibehalten; eigene Cursorbilder und DPI-Varianten anderer Anwendungen sind nicht immer eindeutig klassifizierbar. Mit „Auch Text-, Lade- und Größenzeiger ersetzen“ wird der GPT-Pfeil einheitlich verwendet.

Quellen und genaue Abweichungen: [SOURCES.md](SOURCES.md).

## Setup, Autostart und Richtung (Version 1.1)

`dist\installer\GPT-Cursor-Setup-1.1.0.exe` installiert die Anwendung für den aktuellen Benutzer nach `%LOCALAPPDATA%\Programs\GPT Cursor`. Die .NET-Laufzeit ist enthalten. Deinstallation über Windows → Installierte Apps → GPT Cursor oder die Verknüpfung im Startmenü. Der Installer bietet Englisch/Deutsch, eine optionale Desktop-Verknüpfung und optionalen Autostart.

Im Tab **System** schaltet **Start with Windows** den Autostart um. Dabei wird ausschließlich der Eintrag `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\GPTCursor` verwaltet. Der Start erfolgt aktiviert im Infobereich. Die Deinstallation beendet das Overlay regulär, entfernt die Programmdateien, Verknüpfungen und den eigenen Autostart-Eintrag. Installierte Einstellungen unter `%LOCALAPPDATA%\GPTCursor\settings.json` bleiben für eine erneute Installation erhalten. Die portable Version nutzt weiterhin `settings.json` neben ihrer EXE. Persönliche Einstellungen werden nicht in den Installer gepackt.

**Point in movement direction; keep direction when stopped** im Tab **Cursor** wechselt zwischen der bisherigen Drehanimation und einem Pfeil, der entlang der Bewegung zeigt und die Richtung im Stillstand hält. Stretch und Squash bleiben separat schaltbar. Im Richtungsmodus hat die Bewegungsrichtung Vorrang vor der normalen Drehung und dem Nachwippen.

## Startmenü, erhöhte Fenster und Spiele

Das Startmenü und einige Windows-Shell-Flächen liegen über gewöhnlichen Topmost-Fenstern. Dort gibt GPT Cursor den nativen Windows-/MouseX-Zeiger frei, damit der Zeiger nicht unsichtbar hinter der Shell liegt. Auf der geschützten UAC-Oberfläche ist keine eigene Overlay-Darstellung möglich; auch dort bleibt der native Cursor zuständig. Die App verändert weder UAC noch UIAccess-Richtlinien und installiert keine Zertifikate. Auf normalen Desktop-Fenstern bleibt das Overlay aktiv, soweit Windows den Zugriff zulässt; die Anzeige in jeder erhöhten App ist nicht garantiert.

**Use the app cursor in fullscreen apps** ist standardmäßig aktiv und pausiert das Overlay in Vollbildfenstern (auch in Browser-Vollbild). Für Fensterspiele oder andere Programme stehen **Excluded apps** zur Verfügung, z. B. `game.exe; other.exe`. Die Namen entsprechen den Prozessnamen, ohne Pfad, Groß-/Kleinschreibung ist egal. Programme, die ihren Cursor verbergen, werden berücksichtigt, soweit Windows dies über die Cursor-API meldet. Spiele mit selbst gezeichneten Cursorn lassen sich nicht universell automatisch erkennen; hierfür die Prozessausnahme verwenden. Es gibt keine Spiele-Injektion und keine Eingriffe in Anti-Cheat-Systeme.

Installer bauen: `powershell -File installer\Build-Setup.ps1 -Compiler "C:\Pfad\ISCC.exe"` (Inno Setup 6). `--quit` beendet eine laufende Instanz regulär, auch wenn sie im Infobereich verborgen ist. `--autostart` aktiviert ohne Einstellungsfenster und beendet sich still, wenn die App bereits läuft.


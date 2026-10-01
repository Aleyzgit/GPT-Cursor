# Herkunft der Vorlage

## Öffentliches Repository

Geprüft: https://github.com/openai/codex

Das Repository beschreibt Codex CLI und verweist separat auf die Desktop-App. Der hier verwendete Desktop-Cursor wurde nicht aus diesem öffentlichen Repository gewonnen.

## Lokal installierte Desktop-App

App-Version: `OpenAI.Codex_26.928.2636.0_x64__2p2nqsd0c76g0`.

Read-only untersuchte Quelle: `app/resources/app.asar`.

- `webview/assets/cursor-chat-144c8348ce0a.js`: eingebettetes PNG (46 × 48), DOM-Transformation, Federintegration, Scoot-Verhalten, Nachwippen, separater Glow-Filter.
- `webview/assets/app-shared-a906948d8868.js`: Berechnung der Bézierpfade und Auswahl passender Kurven für Agent-Zielsprünge.

`assets/cursor.png` ist das aus `cursor-chat-144c8348ce0a.js` gelesene PNG. Keine Veränderung der installierten App. Die minifizierten Recherchedateien liegen lokal im ignorierten Ordner `research/` und werden nicht in die EXE eingebaut.

## Übertragene Animationsparameter

| Eigenschaft | Wert |
|---|---|
| Physik-Schritt | 1/240 s |
| Drehfeder | response 0.055, dampingFraction 0.82 |
| Stauchfeder | response 0.12, dampingFraction 0.86 |
| Geschwindigkeitsfeder | response 0.2, dampingFraction 0.85 |
| Richtungsgewicht | x × 0.75 − y × 0.62 |
| Richtungsdrehung | bis 70° |
| Querstauchung | bis 15 % |
| Längsstauchung | clamp(1 − speed/5500, 0.65, 1) |
| Nachwippen | 1.41 s Dauer, 0.66 s Periode, 12.5° Amplitude |

Anpassungen: Klickdurchlässiges natives Windows-Overlay statt DOM-Overlay; Verformung aus laufender Mausgeschwindigkeit statt Fortschritt eines diskreten Agent-Wegs; Pfeilspitze folgt dem Klickpunkt; keine Bézier-Verzögerung der Position, keine Einblend-Unschärfe, kein farbiger Glow. Die vorhandene neutrale Kantenglättung des PNG bleibt erhalten. Die Animation nutzt `UpdateLayeredWindow`; `MagShowSystemCursor` steuert ausschließlich die Sichtbarkeit des nativen Zeigers. Die endgültige Engine ersetzt keine Systemcursorbilder. Damit konkurriert sie nicht mehr mit den Bildaktualisierungen von MouseX.

## Windows-Schnittstellen

Die separat schaltbaren Klickanimationen (420 ms, Einfedern, Einzel-/Doppelring) sind eigene Ergänzungen. Der hochauflösende Frame-Timer ist ebenfalls eine native Ergänzung; Standardziel sind 240 FPS. Die aus der App übernommenen Federparameter bleiben unverändert.

Eigene Ergänzung: unabhängige Glättung für Position und Verformungen. Profile: kausales Hann-/Sinusfenster, analytische kritisch gedämpfte Feder, zwei analytische exponentielle Tiefpassstufen. Diese Profile sind nicht aus der OpenAI-App übernommen. Der echte Windows-Zeiger wird nicht bewegt. Bei Klicks und gedrückten Maustasten wird das Overlay direkt an dessen Position synchronisiert.

- https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc
- https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-createwaitabletimerexw
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey

- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setsystemcursor
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-createiconindirect
- https://learn.microsoft.com/en-us/windows/win32/api/magnification/nf-magnification-magshowsystemcursor

Das Cursorbild stammt von OpenAI. Dieses persönliche lokale Hilfsprogramm ist kein offizielles OpenAI-Produkt. Für das App-Asset wird hier keine eigene Open-Source-Lizenz beansprucht.

Weitere Windows-Grenzen und Installer-Dokumentation:
- https://learn.microsoft.com/en-us/windows/security/application-security/application-control/user-account-control/how-it-works
- https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm
- https://jrsoftware.org/ishelp/topic_registrysection.htm
- https://jrsoftware.org/isdl-verify.php

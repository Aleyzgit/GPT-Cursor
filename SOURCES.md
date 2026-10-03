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

Anpassungen: Klickdurchlässiges natives Windows-Overlay statt DOM-Overlay; Verformung aus laufender Mausgeschwindigkeit statt Fortschritt eines diskreten Agent-Wegs; Pfeilspitze folgt dem Klickpunkt; keine Bézier-Verzögerung der Position, keine Einblend-Unschärfe, kein farbiger Glow. Die vorhandene neutrale Kantenglättung des PNG bleibt erhalten. Die Desktop-Animation nutzt `UpdateLayeredWindow`; `MagShowSystemCursor` steuert die Sichtbarkeit des nativen Zeigers. Seit 1.5.0 ersetzt ein separater statischer Fallback vorübergehend Pfeil/Hand in Start/Suche. Andere Cursorprogramme können ihn überschreiben; nach einem Wiederholungsversuch gibt die App den Vorrang ab. Eine Abhängigkeit von MouseX besteht nicht.

## Oberfläche ab 1.6.0

Read-only analysiert: `OpenAI.Codex_26.930.3930.0_x64__2p2nqsd0c76g0/app/resources/app.asar`.

- `app-shared-6fb15e58cd7f.css`, `app-primary-547a6c7b4fb3.css`, `app-initial-341eb5dd9ad5.css`: neutrale Flächen, Textkontraste, Abstände, Rundungen und Bedienelemente.
- `SegmentedControl-1b0cd2c8f252.css`, `checkbox-a29fd55a92ab.css`: Auswahlzustände und Schalter.
- Die Vorlage verwendet unter anderem `corner-shape: superellipse(1.5)`, teilweise mit um Faktor 1.25 vergrößertem Radius, sowie Übergänge von 150 ms.
- Referenzflächen: Weiß / `#212121`, Sidebar `#f9f9f9` / `#181818`, sekundär `#f3f3f3` / `#303030`; feine neutrale Trennlinien.

Die Oberfläche ist eine eigene WinForms-Implementierung. `UiKit.cs` bildet geglättete Ecken durch abgetastete Superellipsen nach; Schalter bewegen sich über 150 ms. Schrift ist Segoe UI, Dropdown-Popups und Fensterrahmen bleiben Windows-nativ. Navigation, Vorschau und Einstellungsgruppen sind für GPT Cursor gestaltet. Kein CSS, JavaScript oder proprietärer Font der Desktop-App wird mitgeliefert. Die UI ist eine Annäherung an die untersuchte App, keine pixelidentische Kopie.

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

# VSRP DDoS Monitor – Admin-Client (.NET Framework)

Windows-Desktop-Client (C# / WinForms / .NET Framework 4.8) für die PHP-Web-Anwendung im Ordner
`Application_DDOS`. Verbindet sich ausschließlich über die REST-API (`/api`) der Web-Anwendung
mit einem API-Schlüssel – greift nicht direkt auf die Datenbank zu.

## Funktionen

- Live-Status (Bandbreite, Pakete/s, Verbindungen, SYN-RECV), automatische Aktualisierung
- Übersicht aller Vorfälle (Vorfallshistorie mit Spitzenwerten)
- Auffällige IP-Adressen je Vorfall inkl. vorgeschlagenem Blockierbefehl (Kopieren, manuell als
  blockiert/ignoriert markieren – die Anwendung blockiert selbst nichts automatisch)
- API-Schlüssel wird per Windows-DPAPI verschlüsselt gespeichert (`%APPDATA%\VspDdosMonitor\settings.json`)
- Selbstaktualisierung über GitHub Releases (identisches Prinzip wie bei der Mitgliederverwaltung
  U19: Versionsvergleich per Tag, `.msi`-Download, stille Installation, Neustart)

## Einrichtung

1. In der Web-Anwendung als Administrator unter **API-Zugang** einen Schlüssel erstellen.
2. Programm starten, API-Adresse (z. B. `https://ddos.viennastaterp.at/api`) und Schlüssel
   eintragen, „Verbindung testen“, speichern.

## Bauen

Voraussetzung: .NET SDK (aktuell, z. B. 8/9) – erzeugt trotzdem eine .NET-Framework-4.8-Anwendung,
da `TargetFramework` in der `.csproj` auf `net48` steht.

```
dotnet build
dotnet publish -c Release -r win-x64 --self-contained false
```

Der Zielrechner benötigt **.NET Framework 4.8** (auf aktuellem Windows 10/11 und Windows Server
bereits vorinstalliert bzw. über Windows Update verfügbar).

## Installer & automatische Updates

Siehe `installer/README` über die Skripte:

- `installer/build-installer.ps1` – baut die `.msi` (WiX Toolset, als lokales dotnet-Tool).
- `installer/publish-release.ps1` – baut die `.msi` und veröffentlicht sie als GitHub-Release
  (Tag `v<Version>`); installierte Clients erkennen die neue Version automatisch beim Start bzw.
  über „Hilfe → Nach Updates suchen“.

GitHub-Repository für Updates: `Christian445123/DdosMonitor` (in `installer/publish-release.ps1`
und `Services/AppSettings.cs` als Standardwert hinterlegt).

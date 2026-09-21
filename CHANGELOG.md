# Changelog

All notable changes to NutDesk are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and NutDesk uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-21

First release of NutDesk. Changes are relative to
[WinNUT-Client 2.2](https://github.com/nutdotnet/WinNUT-Client), the project it is based on.

### Added

- WPF dashboard: ring gauges for battery charge, load, input, output and battery voltage and frequency; a status
  badge; Connect, Disconnect and Settings in the header.
- Light, dark and follow-Windows themes.
- Complete Italian translation.
- Event log window: severity filters, text search, an "only important events" view, copy to the clipboard, and a
  shortcut to the log file folder. Power failures, restorations, disconnections and reconnections are kept apart
  from the routine polling traffic, so they stay in the list for the whole session.
- Windows notifications for 11 events (power failure and restoration, low battery, shutdown started and
  cancelled, connection lost, restored and abandoned, battery replacement, overload, update available), each one
  selectable, with critical alerts that can stay on screen until dismissed.
- Email notifications over SMTP:
  - STARTTLS, SSL/TLS, automatic or no encryption;
  - password stored encrypted with Windows DPAPI;
  - "Send test email" button;
  - persistent outbox that retries through network outages and across restarts;
  - cooldown that merges repeated events of the same kind into one message;
  - last emails flushed before the computer is shut down.
- New preferences window with inline validation, and min/max checks on the calibration ranges.
- New application menu, anchored to its button and fully usable from the keyboard.
- New icon set.
- In-app updater:
  - automatic check a minute after startup, then daily, weekly (the default) or monthly while NutDesk runs;
  - update window with the release notes, "Skip this version" and the release page;
  - download verified against the SHA-256 published with the release;
  - installation in place with an automatic restart, and the previous version put back if the copy fails;
  - "update available" notification once per version, by email too by default.
- A copy started from another folder takes over the preferences of the previous one, and "Start with Windows"
  follows it.

### Changed

- Rewritten from VB.NET to C#, with no dependency on `Microsoft.VisualBasic`.
- Renamed to NutDesk. Data lives under `%LOCALAPPDATA%\NutDesk`, separate from WinNUT, so the two can be used
  side by side, and no longer in a folder per version: logs and the email outbox survive an update.
- The update check points to this repository and is on by default; releases ship as a portable ZIP instead of an
  installer.
- The crash dialog is translated, and crash reports leave out the NUT server and mail account details.
- Clearer English wording for the connection and shutdown messages.

### Fixed

- Reconnection after the NUT server restarts or the network drops: the connection attempt now has a bounded
  timeout, retries back off from 2 seconds up to 60 seconds, and the window no longer freezes meanwhile.
- Power-failure notifications that could never fire.
- Tray icons missing on a dark taskbar for the low-battery states.
- An invalid poll interval that was "reset" to itself instead of to the default.
- The status badge being reset by a theme change.
- The header not being updated after saving the preferences while disconnected.
- The main window opening larger than small screens.
- Untranslated combo-box values and tooltips in the Italian preferences.
- "Verify Update" in the tray icon's menu, which did nothing.
- A failed update check reported as "no update available" and not tried again for a whole interval.

[1.0.0]: https://github.com/kriseniprak/NutDesk/releases/tag/v1.0.0

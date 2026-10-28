## NutDesk by AwardTech v1.2.0

### Added
- Power Controls for supported NUT instant commands, with safety confirmations defaulting to **No**.
- Live Beeper Status indicator based only on the server-reported `ups.beeper.status`.
- Persistent unsupported-variable detection after three consecutive explicit unsupported responses.
- Unsupported Variables list and Clear list action for rediscovering UPS capabilities.

### Changed
- Compact UPS Commands layout with equal-height beeper buttons and side-by-side Supported Instant Commands and Unsupported Variables panels.
- Command controls remain enabled only when the connected NUT server advertises the corresponding command.

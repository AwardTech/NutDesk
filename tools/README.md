# Testing without a UPS

Two small test doubles let you run NutDesk end to end on a single PC, with no UPS, no NUT server and no real
mailbox. Both are plain Python 3 scripts using only the standard library, and both listen on `127.0.0.1` only.

| Tool | Stands in for | Default port |
|---|---|---|
| [`fake-upsd/fake_upsd.py`](fake-upsd/fake_upsd.py) | a NUT server (`upsd`) with one UPS named `testups` | 3493 |
| [`fake-smtp/fake_smtp.py`](fake-smtp/fake_smtp.py) | an SMTP server that saves every message to disk | 2525 |

Run either with `--help` to see every option.

## Fake NUT server

```powershell
python tools\fake-upsd\fake_upsd.py
```

In NutDesk, open **Settings** and on the **Connection** tab set:

- **NUT host**: `127.0.0.1`
- **NUT Port**: `3493`
- **UPS Name**: `testups`
- **Login / Password**: leave empty (anything is accepted)
- **Re-establish connection**: on, if you want to test reconnection

The simulated UPS starts on mains power with an 87% battery. To change it while NutDesk is connected, write
`fake_upsd_override.json` next to the script. It is read again on every query, and its keys replace the
built-in values:

```powershell
# Power failure
'{"ups.status": "OB DISCHRG", "battery.charge": "40", "battery.runtime": "600"}' | Set-Content tools\fake-upsd\fake_upsd_override.json

# Low battery
'{"ups.status": "OB DISCHRG LB", "battery.charge": "20"}' | Set-Content tools\fake-upsd\fake_upsd_override.json

# Battery needs replacing, overload
'{"ups.status": "OL RB OVER", "ups.load": "112"}' | Set-Content tools\fake-upsd\fake_upsd_override.json

# Back to normal
Remove-Item tools\fake-upsd\fake_upsd_override.json
```

Every connection and command is logged to the console and to `fake_upsd.log` next to the script. Stopping the
script (Ctrl+C) and starting it again is the quickest way to watch NutDesk lose the server and reconnect.

## Fake SMTP server

```powershell
python tools\fake-smtp\fake_smtp.py
python tools\fake-smtp\fake_smtp.py --reject-auth   # every login refused with 535
```

In NutDesk, open **Settings**, go to **Email** and set:

- **SMTP server**: `127.0.0.1`
- **Port**: `2525`
- **Encryption**: `None` - the script offers no STARTTLS
- **Server requires authentication**: either way; any username and password are accepted
  (unless `--reject-auth` is given)
- **Sender address** and **Recipients**: any well-formed addresses

Then press **Send test email**. Each message is saved as `maildir\001.eml`, `002.eml`, ... next to the script,
with the envelope sender, recipients and login user added as `X-Fake-*` headers at the top, and the whole
conversation is logged to `maildir\smtp.log`. Passwords are never logged.

Stop the script to simulate a network outage: NutDesk keeps the emails in its outbox and delivers them once the
script is running again.

## Testing the shutdown path

> **Warning.** When the battery limits are reached, or the server sends FSD with "Shutdown on Nut's FSD Signal"
> turned on, NutDesk really shuts down, suspends or hibernates the computer. The fake UPS makes that trivially
> easy to trigger.
>
> Exercise the shutdown path **only with a Debug build**. In a Debug build `Shutdown_Action` (in
> `src/NutDesk/MainForm.cs`) still disconnects and flushes the last emails, but the power action itself is not
> compiled in, so nothing happens to the machine. A Release build - including every published release - performs
> the action for real.

```powershell
msbuild NutDesk.sln -restore -p:Configuration=Debug
src\NutDesk\bin\Debug\NutDesk.exe
```

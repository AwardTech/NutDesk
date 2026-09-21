#!/usr/bin/env python3
"""A minimal upsd stand-in: just enough of the NUT network protocol to drive NutDesk without a UPS.

Usage:
    python fake_upsd.py [--port 3493] [--host 127.0.0.1] [--log fake_upsd.log]

It serves one UPS, "testups", and answers VER, NETVER, USERNAME, PASSWORD, LOGIN, LOGOUT, GET VAR, GET DESC,
LIST VAR and LIST UPS. Any user name and password are accepted.

Changing the simulated UPS while NutDesk is connected:
    Write fake_upsd_override.json next to this script. It is re-read on every query and its keys replace the
    built-in values, for instance

        {"ups.status": "OB DISCHRG", "battery.charge": "40"}          on battery
        {"ups.status": "OB DISCHRG LB", "battery.charge": "20"}       low battery
        {"ups.status": "FSD OB LB"}                                   forced shutdown from the server

    Delete the file (or write {}) to go back to mains power.

Every connection and every command is logged to the console and appended to the log file, so NutDesk's
reconnection behaviour can be watched from the server side: stop the script, start it again, and see when the
client comes back.

Python 3 standard library only.
"""
import argparse
import json
import os
import socketserver
import sys
import threading
import time

HERE = os.path.dirname(os.path.abspath(__file__))
UPS = "testups"

# Resolved relative to the script rather than the working directory, so the override can be written to the same
# place however the script was started.
OVERRIDE = os.path.join(HERE, "fake_upsd_override.json")

VARS = {
    "ups.mfr": "ACME Power",
    "ups.model": "SmartSim 1500",
    "ups.serial": "SIM-0001",
    "ups.firmware": "1.42",
    "battery.charge": "87",
    "battery.voltage": "13.4",
    "battery.runtime": "1680",
    "battery.capacity": "9",
    "input.frequency": "50.0",
    "input.voltage": "231.2",
    "output.voltage": "230.1",
    "output.frequency.nominal": "50",
    "ups.load": "34",
    "ups.realpower.nominal": "900",
    "ups.status": "OL",
}

_log_lock = threading.Lock()
_log_path = None


def current_vars():
    """The built-in values with the override file, if any, laid over them."""
    merged = dict(VARS)
    try:
        with open(OVERRIDE, encoding="utf-8") as fh:
            merged.update({str(k): str(v) for k, v in json.load(fh).items()})
    except (OSError, ValueError, AttributeError):
        # No file, or one caught half-written: the next query will read it again.
        pass
    return merged


def log(msg):
    line = "%s  %s\n" % (time.strftime("%H:%M:%S"), msg)
    with _log_lock:
        if _log_path:
            with open(_log_path, "a", encoding="utf-8") as fh:
                fh.write(line)
        sys.stdout.write(line)
        sys.stdout.flush()


class NutHandler(socketserver.StreamRequestHandler):
    def handle(self):
        log("CONNECT from %s:%d" % self.client_address[:2])
        try:
            while True:
                raw = self.rfile.readline()
                if not raw:
                    break
                line = raw.decode("ascii", "replace").strip()
                if not line:
                    continue
                # The password is the one thing not worth echoing, even from a test double.
                log("  > %s" % ("PASSWORD ***" if line.upper().startswith("PASSWORD") else line))
                for response in self.respond(line):
                    log("  < %s" % response)
                    self.wfile.write((response + "\n").encode("ascii"))
                self.wfile.flush()
                if line.upper().startswith("LOGOUT"):
                    break
        except Exception as exc:  # noqa: BLE001 - a test double: any error just ends the session
            log("  ! %s" % exc)
        finally:
            log("DISCONNECT %s:%d" % self.client_address[:2])

    def respond(self, line):
        parts = line.split()
        verb = parts[0].upper()

        if verb == "VER":
            return ["Network UPS Tools upsd 2.8.1 - https://www.networkupstools.org/"]
        if verb == "NETVER":
            return ["1.3"]
        if verb in ("USERNAME", "PASSWORD", "LOGIN"):
            return ["OK"]
        if verb == "LOGOUT":
            return ["OK Goodbye"]

        if verb == "GET" and len(parts) >= 4 and parts[1].upper() == "VAR":
            ups, var = parts[2], parts[3]
            if ups != UPS:
                return ["ERR UNKNOWN-UPS"]
            values = current_vars()
            if var in values:
                return ['VAR %s %s "%s"' % (ups, var, values[var])]
            return ["ERR VAR-NOT-SUPPORTED"]

        if verb == "GET" and len(parts) >= 4 and parts[1].upper() == "DESC":
            ups, var = parts[2], parts[3]
            return ['DESC %s %s "Simulated description"' % (ups, var)]

        if verb == "LIST" and len(parts) >= 3 and parts[1].upper() == "VAR":
            ups = parts[2]
            if ups != UPS:
                return ["ERR UNKNOWN-UPS"]
            out = ["BEGIN LIST VAR %s" % ups]
            out += ['VAR %s %s "%s"' % (ups, k, v) for k, v in sorted(current_vars().items())]
            out.append("END LIST VAR %s" % ups)
            return out

        if verb == "LIST" and len(parts) >= 2 and parts[1].upper() == "UPS":
            return ["BEGIN LIST UPS", 'UPS %s "Simulated UPS"' % UPS, "END LIST UPS"]

        return ["ERR UNKNOWN-COMMAND"]


class Server(socketserver.ThreadingTCPServer):
    allow_reuse_address = True
    daemon_threads = True


def main():
    global _log_path

    parser = argparse.ArgumentParser(description="Minimal NUT upsd for testing NutDesk without a UPS.")
    parser.add_argument("--host", default="127.0.0.1", help="address to listen on (default 127.0.0.1)")
    parser.add_argument("--port", type=int, default=3493, help="TCP port (default 3493, the NUT port)")
    parser.add_argument("--log", default=os.path.join(HERE, "fake_upsd.log"),
                        help="log file, appended to (default fake_upsd.log next to this script)")
    args = parser.parse_args()
    _log_path = os.path.abspath(args.log)

    log("=== fake upsd listening on %s:%d for UPS '%s' ===" % (args.host, args.port, UPS))
    log("    override file: %s" % OVERRIDE)
    with Server((args.host, args.port), NutHandler) as srv:
        try:
            srv.serve_forever()
        except KeyboardInterrupt:
            log("=== stopped ===")


if __name__ == "__main__":
    main()

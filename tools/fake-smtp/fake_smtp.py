#!/usr/bin/env python3
"""A minimal SMTP server for exercising NutDesk's email notifications without a real mailbox.

Usage:
    python fake_smtp.py [--port 2525] [--host 127.0.0.1] [--maildir DIR] [--reject-auth]

It speaks plain SMTP only - no STARTTLS is offered - so in NutDesk set the encryption to "None":
EHLO/HELO, AUTH PLAIN/LOGIN, MAIL, RCPT, DATA, RSET, NOOP and QUIT.

    - Every accepted message is saved as <maildir>/001.eml, 002.eml, ... with three extra headers in front
      (X-Fake-Envelope-From, X-Fake-Envelope-To, X-Fake-Auth-User) recording what the client sent outside the
      message itself.
    - Every exchange is appended to <maildir>/smtp.log and echoed to the console. Passwords are never logged.
    - Any user name and password are accepted, unless --reject-auth is given: then every login is refused with
      535, which is how to check that NutDesk reports an authentication failure (and keeps the email queued).

The maildir defaults to a "maildir" folder next to this script.

To simulate a network outage, stop the script: NutDesk keeps the emails in its outbox and delivers them once
the script is started again.

Python 3 standard library only.
"""
import argparse
import base64
import binascii
import os
import socketserver
import sys
import threading
import time

HERE = os.path.dirname(os.path.abspath(__file__))

_lock = threading.Lock()
_maildir = None
_reject_auth = False


def log(msg):
    line = "%s  %s\n" % (time.strftime("%H:%M:%S"), msg)
    with _lock:
        with open(os.path.join(_maildir, "smtp.log"), "a", encoding="utf-8") as fh:
            fh.write(line)
        sys.stdout.write(line)
        sys.stdout.flush()


def save_message(text):
    """Stores one message as the next free NNN.eml and returns its path.

    Numbered from the highest existing file rather than from a count, so deleting an old message never makes a
    new one overwrite another; the file is created exclusively inside the lock, so two sessions finishing
    DATA together cannot pick the same name.
    """
    with _lock:
        numbers = [int(f[:-4]) for f in os.listdir(_maildir) if f.endswith(".eml") and f[:-4].isdigit()]
        n = max(numbers, default=0) + 1
        while True:
            path = os.path.join(_maildir, "%03d.eml" % n)
            try:
                with open(path, "x", encoding="utf-8") as fh:
                    fh.write(text)
                return path
            except FileExistsError:
                n += 1


def b64(text):
    """Decodes a base64 SASL token; a malformed one yields '?' rather than killing the session."""
    try:
        return base64.b64decode(text or "")
    except (binascii.Error, ValueError):
        return b"?"


class Handler(socketserver.StreamRequestHandler):
    def send(self, text):
        log("  < " + text)
        self.wfile.write((text + "\r\n").encode("utf-8"))
        self.wfile.flush()

    def readline(self):
        raw = self.rfile.readline()
        if not raw:
            return None
        return raw.decode("utf-8", "replace").rstrip("\r\n")

    def handle(self):
        log("CONNECT %s:%d" % self.client_address[:2])
        self.send("220 fake-smtp ESMTP ready")
        mail_from, rcpts, user = None, [], None
        while True:
            line = self.readline()
            if line is None:
                break
            words = line.split(" ")
            verb = words[0].upper()
            # AUTH carries the credentials inline; show the mechanism only.
            log("  > " + (" ".join(words[:2]) + " ***" if verb == "AUTH" else line))

            if verb == "EHLO":
                self.send("250-fake-smtp")
                self.send("250-AUTH PLAIN LOGIN")
                self.send("250 8BITMIME")
            elif verb == "HELO":
                self.send("250 fake-smtp")
            elif verb == "AUTH":
                mech = words[1].upper() if len(words) > 1 else ""
                if mech == "PLAIN":
                    blob = words[2] if len(words) > 2 else None
                    if blob is None:
                        self.send("334 ")
                        blob = self.readline()
                    fields = b64(blob).split(b"\0")
                    user = fields[1].decode("utf-8", "replace") if len(fields) > 1 else "?"
                elif mech == "LOGIN":
                    if len(words) > 2:
                        user = b64(words[2]).decode("utf-8", "replace")
                    else:
                        self.send("334 VXNlcm5hbWU6")  # "Username:"
                        user = b64(self.readline()).decode("utf-8", "replace")
                    self.send("334 UGFzc3dvcmQ6")  # "Password:"
                    self.readline()
                else:
                    self.send("504 5.5.4 Unsupported mechanism")
                    continue
                if _reject_auth:
                    log("AUTH refused for user '%s'" % user)
                    self.send("535 5.7.8 Authentication credentials invalid")
                else:
                    log("AUTH accepted for user '%s'" % user)
                    self.send("235 2.7.0 Authentication successful")
            elif verb == "MAIL":
                mail_from, rcpts = line[10:].strip(), []
                self.send("250 OK")
            elif verb == "RCPT":
                rcpts.append(line[8:].strip())
                self.send("250 OK")
            elif verb == "DATA":
                self.send("354 End data with <CR><LF>.<CR><LF>")
                body = []
                complete = False
                while True:
                    data_line = self.readline()
                    if data_line is None:
                        break
                    if data_line == ".":
                        complete = True
                        break
                    # Undo the client's dot-stuffing so the saved message is what was written.
                    body.append(data_line[1:] if data_line.startswith("..") else data_line)
                if not complete:
                    # The client went away in the middle of DATA: a real server would not deliver that either.
                    log("DATA interrupted, partial message discarded")
                    break
                path = save_message(
                    "X-Fake-Envelope-From: %s\nX-Fake-Envelope-To: %s\nX-Fake-Auth-User: %s\n"
                    % (mail_from, ", ".join(rcpts), user) + "\n".join(body) + "\n")
                log("MESSAGE saved to %s (from %s to %s)" % (os.path.basename(path), mail_from, ", ".join(rcpts)))
                self.send("250 OK queued")
            elif verb == "RSET":
                mail_from, rcpts = None, []
                self.send("250 OK")
            elif verb == "NOOP":
                self.send("250 OK")
            elif verb == "QUIT":
                self.send("221 Bye")
                break
            else:
                self.send("502 5.5.1 Command not implemented")
        log("DISCONNECT %s:%d" % self.client_address[:2])


class Server(socketserver.ThreadingTCPServer):
    allow_reuse_address = True
    daemon_threads = True


def main():
    global _maildir, _reject_auth

    parser = argparse.ArgumentParser(description="Minimal plain-text SMTP server for testing NutDesk's email.")
    parser.add_argument("--host", default="127.0.0.1", help="address to listen on (default 127.0.0.1)")
    parser.add_argument("--port", type=int, default=2525, help="TCP port (default 2525)")
    parser.add_argument("--maildir", default=os.path.join(HERE, "maildir"),
                        help="where messages and smtp.log are written (default: maildir next to this script)")
    parser.add_argument("--reject-auth", action="store_true", help="refuse every login with 535")
    args = parser.parse_args()

    _maildir = os.path.abspath(args.maildir)
    _reject_auth = args.reject_auth
    os.makedirs(_maildir, exist_ok=True)

    log("=== fake SMTP on %s:%d, maildir %s%s ===" % (args.host, args.port, _maildir,
                                                     " (rejecting auth)" if _reject_auth else ""))
    with Server((args.host, args.port), Handler) as srv:
        try:
            srv.serve_forever()
        except KeyboardInterrupt:
            log("=== stopped ===")


if __name__ == "__main__":
    main()

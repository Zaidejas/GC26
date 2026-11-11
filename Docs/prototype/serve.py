"""Serves the Last Bastion prototype on a local web server.

Usage:
    python serve.py          # http://127.0.0.1:8001, opens the browser
    python serve.py --lan    # also reachable from a phone on the same Wi-Fi

Files are sent with "no-store" so edits show up on the next reload.
"""
import argparse
import functools
import http.server
import socket
import webbrowser
from pathlib import Path

HERE = Path(__file__).resolve().parent


class NoCacheHandler(http.server.SimpleHTTPRequestHandler):
    """Static file handler that tells the browser not to cache anything."""

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()


def lan_address():
    """Best guess at this PC's address on the local network (no data is sent)."""
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
            s.connect(("10.255.255.255", 1))
            return s.getsockname()[0]
    except OSError:
        return None


def main():
    parser = argparse.ArgumentParser(description="Serve the Last Bastion prototype.")
    parser.add_argument("--port", type=int, default=8001)
    parser.add_argument("--lan", action="store_true", help="listen on all network interfaces")
    parser.add_argument("--no-browser", action="store_true", help="don't open a browser")
    args = parser.parse_args()

    host = "0.0.0.0" if args.lan else "127.0.0.1"
    handler = functools.partial(NoCacheHandler, directory=str(HERE))
    server = http.server.ThreadingHTTPServer((host, args.port), handler)

    local_url = f"http://127.0.0.1:{args.port}/"
    print(f"Serving {HERE} at {local_url}")
    if args.lan and lan_address():
        print(f"On your phone (same Wi-Fi): http://{lan_address()}:{args.port}/")
    print("Press Ctrl+C to stop.")

    if not args.no_browser:
        webbrowser.open(local_url)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopped.")
    finally:
        server.server_close()


if __name__ == "__main__":
    main()

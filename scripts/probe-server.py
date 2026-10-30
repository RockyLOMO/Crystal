"""Read-only Crystal handshake probe; does not create accounts or characters."""
import argparse
import hashlib
import json
from pathlib import Path
import socket
import struct


def receive(sock):
    def exact(count):
        output = b""
        while len(output) < count:
            chunk = sock.recv(count - len(output))
            if not chunk:
                raise RuntimeError("Connection closed")
            output += chunk
        return output
    length, packet_id = struct.unpack("<Hh", exact(4))
    if not 4 <= length <= 65535:
        raise ValueError("Invalid packet length")
    return packet_id, exact(length - 4)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=7000)
    parser.add_argument("--client", default=r"D:\Program Files\Game\Mir2\Client.exe")
    args = parser.parse_args()
    with Path(args.client).open("rb") as stream:
        version_hash = hashlib.file_digest(stream, "md5").digest()
    with socket.create_connection((args.host, args.port), timeout=10) as sock:
        packet_id, payload = receive(sock)
        assert packet_id == 0, (packet_id, payload)
        packet = struct.pack("<Hhi", 24, 0, len(version_hash)) + version_hash
        sock.sendall(packet)
        while True:
            packet_id, payload = receive(sock)
            if packet_id == 3:
                continue
            assert packet_id == 1 and payload == b"\x01", (packet_id, payload)
            break
        sock.sendall(struct.pack("<Hhq", 12, 2, 123456789))
        packet_id, payload = receive(sock)
        assert packet_id == 3 and struct.unpack("<q", payload)[0] == 123456789
        sock.sendall(struct.pack("<Hh", 4, 1))
    print(json.dumps({"endpoint": f"{args.host}:{args.port}",
                      "connected_packet": "PASS", "client_version": "PASS",
                      "keepalive_roundtrip": "PASS", "account_changes": False}))


if __name__ == "__main__":
    main()

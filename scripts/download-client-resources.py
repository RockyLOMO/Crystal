"""Download the Crystal guide's resource manifest, preserving existing files."""
import argparse
import concurrent.futures
import gzip
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import struct
import tempfile
import threading
import time
import urllib.parse
import urllib.request

BASE = "https://mirfiles.com/resources/mir2/crystal/patch/"


def request(url):
    return urllib.request.urlopen(urllib.request.Request(
        url, headers={"User-Agent": "CrystalResourceSetup/1.0"}), timeout=90)


def parse_manifest(raw):
    if raw.startswith(b"\x1f\x8b"):
        raw = gzip.decompress(raw)
    stream = io.BytesIO(raw)
    count, = struct.unpack("<i", stream.read(4))
    if not 0 < count < 100000:
        raise ValueError("Invalid resource manifest count")
    entries = []
    for _ in range(count):
        length = shift = 0
        while True:
            value = stream.read(1)[0]
            length |= (value & 127) << shift
            shift += 7
            if not value & 128:
                break
            if shift > 35:
                raise ValueError("Invalid manifest string")
        name = stream.read(length).decode("utf-8").replace("\\", "/")
        size, compressed, stamp = struct.unpack("<iiq", stream.read(16))
        path = PurePosixPath(name)
        if path.is_absolute() or ".." in path.parts or ":" in name:
            raise ValueError("Unsafe manifest path")
        if path.parts[0] not in {"Data", "DirectX", "Map", "Sound"}:
            raise ValueError("Unexpected resource directory: " + name)
        if size < 0 or compressed < 0:
            raise ValueError("Invalid resource length")
        entries.append({"path": name, "size": size, "compressed": compressed})
    if stream.read(1):
        raise ValueError("Unexpected trailing manifest bytes")
    return entries


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--destination", required=True)
    parser.add_argument("--workspace", required=True)
    parser.add_argument("--temp", required=True)
    parser.add_argument("--workers", type=int, default=8)
    parser.add_argument("--verify-only", action="store_true")
    args = parser.parse_args()
    destination = Path(args.destination).resolve()
    workspace = Path(args.workspace).resolve()
    temp = Path(args.temp).resolve()
    temp.mkdir(parents=True, exist_ok=True)
    manifest_path = temp / "PList.gz"
    if not manifest_path.exists():
        with request(BASE + "PList.gz") as response:
            manifest_path.write_bytes(response.read())
    raw = manifest_path.read_bytes()
    entries = parse_manifest(raw)
    total = sum(e["size"] for e in entries)
    print(json.dumps({"files": len(entries), "bytes": total,
                      "manifest_sha256": hashlib.sha256(raw).hexdigest()}), flush=True)
    lock = threading.Lock()
    completed = downloaded = skipped = done_bytes = 0
    failures = []
    hashes = {}
    started = time.monotonic()

    def fetch(entry):
        nonlocal completed, downloaded, skipped, done_bytes
        target = destination.joinpath(*PurePosixPath(entry["path"]).parts)
        if not target.is_relative_to(destination):
            raise ValueError("Resource escapes destination")
        existed = target.exists()
        if existed:
            if target.is_symlink() or target.stat().st_size != entry["size"]:
                raise ValueError("Preserved conflicting existing file: " + str(target))
        elif args.verify_only:
            raise ValueError("Missing resource: " + entry["path"])
        else:
            remote = entry["path"]
            if entry["compressed"] != entry["size"]:
                remote += ".gz"
            url = BASE + urllib.parse.quote(remote, safe="/")
            for attempt in range(4):
                partial = temp / (hashlib.sha256(remote.encode()).hexdigest() + ".part")
                try:
                    with request(url) as response, partial.open("wb") as output:
                        shutil.copyfileobj(response, output, 1024 * 1024)
                    if entry["compressed"] != entry["size"]:
                        unpacked = partial.with_suffix(".unpacked")
                        with gzip.open(partial, "rb") as source, unpacked.open("wb") as output:
                            shutil.copyfileobj(source, output, 1024 * 1024)
                        partial.unlink()
                        partial = unpacked
                    if partial.stat().st_size != entry["size"]:
                        raise ValueError("Downloaded resource size mismatch")
                    target.parent.mkdir(parents=True, exist_ok=True)
                    with target.open("xb") as output, partial.open("rb") as source:
                        shutil.copyfileobj(source, output, 1024 * 1024)
                    partial.unlink()
                    break
                except Exception:
                    if attempt == 3:
                        raise
                    time.sleep(2 ** attempt)
        with target.open("rb") as source:
            digest = hashlib.file_digest(source, "sha256").hexdigest()
        with lock:
            hashes[entry["path"]] = digest
            completed += 1
            downloaded += not existed
            skipped += existed
            done_bytes += entry["size"]

    def progress():
        return {"completed": completed, "files": len(entries), "bytes": done_bytes,
                "total_bytes": total, "downloaded": downloaded, "existing": skipped,
                "failures": len(failures), "seconds": round(time.monotonic() - started)}

    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as executor:
        futures = {executor.submit(fetch, entry): entry for entry in entries}
        pending = set(futures)
        while pending:
            done, pending = concurrent.futures.wait(pending, timeout=10,
                return_when=concurrent.futures.FIRST_COMPLETED)
            for future in done:
                try:
                    future.result()
                except Exception as error:
                    failures.append({"path": futures[future]["path"], "error": str(error)})
                    print(json.dumps(failures[-1]), flush=True)
            if time.monotonic() - getattr(progress, "last", started) >= 15:
                print(json.dumps(progress()), flush=True)
                progress.last = time.monotonic()
    report = {"source": BASE, "manifest_sha256": hashlib.sha256(raw).hexdigest(),
              **progress(), "errors": failures}
    report_dir = workspace / "docs"
    report_dir.mkdir(exist_ok=True)
    (report_dir / "client-resource-download.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8")
    (workspace / ".ai" / "client-resource-sha256.json").write_text(
        json.dumps(hashes, indent=2), encoding="utf-8")
    print(json.dumps(report), flush=True)
    if failures:
        raise SystemExit(1)


if __name__ == "__main__":
    main()

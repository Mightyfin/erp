#!/usr/bin/env python3
"""Live M54.3 verification from inside the server: public erp-web client, ROPC grant."""
import json
import urllib.request
import urllib.parse

KC = "http://localhost:18081/realms/mightyfin-sandbox/protocol/openid-connect/token"
BASE = "http://localhost:28912/api/hrm"
USER = "georgemunganga@gmail.com"
PW = "JesusisKing#202!"


def token():
    data = urllib.parse.urlencode({
        "grant_type": "password",
        "client_id": "erp-web",
        "username": USER,
        "password": PW,
    }).encode()
    req = urllib.request.Request(KC, data=data)
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.load(r)["access_token"]


def get(path, tok, header=None):
    req = urllib.request.Request(f"{BASE}{path}")
    req.add_header("Authorization", f"Bearer {tok}")
    if header:
        req.add_header("X-Shell-Location", header)
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return r.status, json.load(r)
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read().decode() or "{}")


def main():
    tok = token()
    NDOLA = "01a02102-17eb-7470-93f2-d00655be4060"
    LUSAKA = "01a01ea9-f25e-7ccc-bbe4-57030f0e9f94"
    for label, hdr in [("org-wide (no header)", None), ("NDOLA (org unit)", NDOLA), ("LUSAKA (org unit)", LUSAKA)]:
        s_code, shell = get("/shell", tok, hdr)
        w_code, workers = get("/workers?page=1&pageSize=100", tok, hdr)
        items = workers.get("items") if isinstance(workers, dict) else workers
        print(f"\n=== {label} ===")
        print(f"shell[{s_code}]: locationId={shell.get('locationId')} orgUnitId={shell.get('orgUnitId')} scopedToBranch={shell.get('scopedToBranch')} entityId={shell.get('entityId')}")
        print(f"workers[{w_code}]: totalCount={workers.get('totalCount') if isinstance(workers, dict) else len(items)}")
        for w in items or []:
            print(f"   - {w.get('fullName')} | orgUnit={w.get('orgUnitName')} | loc={w.get('locationName')}")


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Live M54.3 verification: org-unit scope resolution via the shell echo and workers list."""
import json
import urllib.request
import urllib.parse
import sys

BASE = "https://erp.mightyfinance.co.zm/api/hrm"
KC_HOST = "172.16.1.2:18081"
REALM = "mightyfin-sandbox"
CLIENT = "erp"

def token(user, pw):
    data = urllib.parse.urlencode({
        "grant_type": "password",
        "client_id": CLIENT,
        "username": user,
        "password": pw,
    }).encode()
    req = urllib.request.Request(f"http://{KC_HOST}/realms/{REALM}/protocol/openid-connect/token", data=data)
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.load(r)["access_token"]

def get(path, token, header=None, base=BASE):
    req = urllib.request.Request(f"{base}{path}")
    req.add_header("Authorization", f"Bearer {token}")
    if header:
        req.add_header("X-Shell-Location", header)
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return r.status, json.load(r)
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read().decode() or "{}")

def main():
    user = sys.argv[1] if len(sys.argv) > 1 else "georgemunganga@gmail.com"
    pw = sys.argv[2] if len(sys.argv) > 2 else "JesusisKing#202!"
    tok = token(user, pw)

    NDOLA = "01a02102-17eb-7470-93f2-d00655be4060"
    LUSAKA = "01a01ea9-f25e-7ccc-bbe4-57030f0e9f94"

    for label, hdr in [("org-wide (no header)", None), ("NDOLA (org unit)", NDOLA), ("LUSAKA (org unit)", LUSAKA)]:
        s_code, shell = get("/shell", tok, hdr)
        w_code, workers = get("/workers?page=1&pageSize=100", tok, hdr)
        items = workers.get("items") if isinstance(workers, dict) else workers
        names = [(w.get("fullName"), w.get("orgUnitName")) for w in (items or [])]
        print(f"\n=== {label} ===")
        print(f"shell[{s_code}]: locationId={shell.get('locationId')} orgUnitId={shell.get('orgUnitId')} scopedToBranch={shell.get('scopedToBranch')} entityId={shell.get('entityId')}")
        print(f"workers[{w_code}]: total={workers.get('totalCount') if isinstance(workers, dict) else len(items)}")
        for n, u in names:
            print(f"   - {n} ({u})")

if __name__ == "__main__":
    main()

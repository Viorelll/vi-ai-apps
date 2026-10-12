#!/usr/bin/env python3
"""Verify the seeded dataset through the running public API."""
import json
import sys
from urllib.request import Request, urlopen

base_url = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:8080"


def get_json(path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    request = Request(base_url + path, data=data, headers={"Content-Type": "application/json"} if data else {})
    with urlopen(request, timeout=20) as response:
        return json.load(response)


datasets = get_json("/api/datasets")
sample = next((item for item in datasets if item["name"] == "Sample sales · EUR"), None)
if sample is None:
    raise SystemExit("Seed sales dataset was not found")
result = get_json("/api/analyze", {"datasetId": sample["id"], "question": "Venituri pe categorie"})
actual = {row["label"]: row["value"] for row in result["rows"]}
expected = {"Hardware": 25, "Software": 21}
if result["metric"] != "revenue" or actual != expected or result["total"] != 46 or not result["resultHash"]:
    raise SystemExit("Seed aggregation did not match the documented EUR values")
print(f"Seed aggregation passed: {actual['Hardware']:.2f} + {actual['Software']:.2f} = {result['total']:.2f} EUR (hash {result['resultHash'][:12]}).")

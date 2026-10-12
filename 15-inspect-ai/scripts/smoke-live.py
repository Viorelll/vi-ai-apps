#!/usr/bin/env python3
"""Opt-in billable Foundry smoke. Requires real configured deployment and CU."""
import importlib.util
from pathlib import Path

spec = importlib.util.spec_from_file_location(
    "smoke", Path(__file__).with_name("smoke-placeholder.py")
)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
call, upload, wait = module.call, module.upload, module.wait
from pathlib import Path
import uuid

root = Path(__file__).resolve().parents[1]
photo = upload(root / "seeds/inspection.png")
manual = upload(root / "seeds/manual.txt")
for id in (photo, manual):
    assert (
        wait("/api/documents/" + id, "status", {"Ready", "Failed"})["status"] == "Ready"
    )
i = call(
    "/api/inspections",
    {
        "equipmentLabel": "Live artificial inspection",
        "equipmentModel": "P-100",
        "manualVersion": "1.0",
    },
)
id = i["id"]
i = call(
    f"/api/inspections/{id}/sources",
    {"documentId": photo, "role": "photo", "revision": 1},
)
i = call(
    f"/api/inspections/{id}/sources",
    {
        "documentId": manual,
        "role": "manual",
        "revision": i["revision"],
        "manualModel": "P-100",
        "manualVersion": "1.0",
    },
)
call(
    f"/api/inspections/{id}/analyze",
    {"revision": i["revision"], "requestId": str(uuid.uuid4())},
)
i = wait(f"/api/inspections/{id}", "state", {"NeedsReview", "Ready", "Failed"})
assert i["state"] in {"NeedsReview", "Ready"}, i.get("errorCode")
assert i["findings"] and i["checklist"] and i["report"]
call(f"/api/inspections/{id}/export")
a = call(
    f"/api/inspections/{id}/questions",
    {
        "question": "Ce spune manualul despre pata observată?",
        "requestId": str(uuid.uuid4()),
        "revision": i["revision"],
    },
)
assert a["answer"] and a["evidence"]
print(
    "Live Foundry report, evidence, export and scoped question passed. Human review of visual interpretation is still required."
)

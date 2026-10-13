#!/usr/bin/env python3
import json
import re
import os
from pathlib import Path


def escape_annotation(value: str) -> str:
    return (
        value.replace("%", "%25")
        .replace("\r", "%0D")
        .replace("\n", "%0A")
        .replace(":", "%3A")
        .replace(",", "%2C")
    )


state_path = Path("/tmp/moderateai-compose-state.jsonl")
logs_path = Path("/tmp/moderateai-compose-logs.txt")
report = ["## Container startup diagnostics", "### Service state"]

for line in state_path.read_text().splitlines() if state_path.exists() else []:
    try:
        item = json.loads(line)
    except json.JSONDecodeError:
        continue
    message = " ".join(
        str(item.get(key, "")) for key in ("Service", "State", "Health", "ExitCode")
    ).strip()
    if message:
        report.append(message)
        print(f"::error title=Compose service state::{escape_annotation(message)}")

report.append("### Relevant container logs")
if logs_path.exists():
    lines = logs_path.read_text(errors="replace").splitlines()
    relevant = [
        line
        for line in lines
        if re.search(
            r"error|failed|forbidden|denied|refused|unhealthy|fatal|exception|unavailable",
            line,
            re.IGNORECASE,
        )
    ][-12:]
    report.extend(relevant or ["No matching error lines were emitted by containers."])
    for line in relevant:
        print(f"::error title=Compose startup log::{escape_annotation(line[-700:])}")

summary = os.environ.get("GITHUB_STEP_SUMMARY")
if summary:
    with Path(summary).open("a") as stream:
        stream.write("\n".join(report) + "\n")

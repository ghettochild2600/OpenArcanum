#!/usr/bin/env python3
"""Generate deterministic, pixel-level evidence from two native 800x600 UI captures.

This tool is development-only. It never resizes either input and does not produce a
perceptual score. Pillow and NumPy are its only dependencies.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import shutil
from collections import deque
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

import numpy as np
from PIL import Image, ImageChops


EXPECTED_SIZE = (800, 600)

# Native 800x600 framebuffer rectangles. Coordinates are left/top/right/bottom,
# with right and bottom exclusive. These define comparison subjects, not results.
CROPS = {
    "A_character_identity": (108, 40, 490, 145),
    "B_left_attribute_column": (112, 145, 251, 307),
    "C_right_attribute_column": (251, 145, 410, 307),
    "D_alignment_meter": (0, 165, 116, 295),
    "E_health_fatigue": (405, 145, 493, 300),
    "F_derived_resistance": (14, 310, 492, 438),
    "G_skills_magic_panel": (498, 96, 744, 439),
    "H_contextual_help": (188, 446, 612, 600),
}

SHOPKEEPER_CROPS = {
    "A_merchant_inventory": (0, 160, 350, 400),
    "B_player_inventory": (358, 0, 710, 400),
    "C_gold_no_badge": (358, 0, 455, 82),
    "D_gold_hover_panel": (175, 420, 625, 600),
    "E_resource_counters": (710, 52, 800, 188),
    "F_scrollbar": (323, 160, 352, 400),
    "G_drag_operation": (358, 0, 710, 400),
    "H_relocated_item": (358, 0, 710, 400),
    "I_merchant_quote": (80, 115, 350, 165),
    "J_full_screen": (0, 0, 800, 600),
}


@dataclass(frozen=True)
class ControlRegion:
    field_roi: tuple[int, int, int, int]
    numeral_roi: tuple[int, int, int, int]
    field_kind: str = "dark"
    visible_oval: bool = False


CONTROLS = {
    "ST": ControlRegion((170, 148, 247, 187), (197, 160, 220, 173), visible_oval=True),
    "CN": ControlRegion((170, 188, 247, 227), (197, 200, 220, 213), visible_oval=True),
    "DX": ControlRegion((170, 228, 247, 267), (197, 240, 220, 253), visible_oval=True),
    "BE": ControlRegion((170, 268, 247, 307), (197, 280, 220, 293), visible_oval=True),
    "IN": ControlRegion((309, 148, 390, 187), (336, 160, 359, 173), visible_oval=True),
    "WP": ControlRegion((309, 188, 390, 227), (336, 200, 359, 213), visible_oval=True),
    "PE": ControlRegion((309, 228, 390, 267), (336, 240, 359, 253), visible_oval=True),
    "CH": ControlRegion((309, 268, 390, 307), (336, 280, 359, 293), visible_oval=True),
    "Health": ControlRegion((422, 174, 473, 217), (435, 184, 459, 204), "red"),
    "Fatigue": ControlRegion((422, 244, 473, 287), (435, 254, 459, 274), "blue"),
    "Alignment": ControlRegion((39, 236, 75, 260), (51, 237, 64, 258)),
}


FONT_MANIFEST = {
    "identity_and_primary_values": {
        "source_font_id": 27,
        "openarcanum_font_id": 27,
        "source_tint_rgb": [255, 255, 255],
        "openarcanum_tint_rgb": [255, 255, 255],
    },
    "health_and_fatigue_values": {
        "source_font_id": 27,
        "openarcanum_font_id": 27,
        "source_tint_rgb": [255, 255, 255],
        "openarcanum_tint_rgb": [255, 255, 255],
    },
    "alignment_value": {
        "source_font_id": 171,
        "openarcanum_font_id": 171,
        "source_tint_rgb": [255, 255, 255],
        "openarcanum_tint_rgb": [255, 255, 255],
    },
    "derived_and_resistance_headings": {
        "source_font_id": 27,
        "openarcanum_font_id": 27,
        "source_tint_rgb": [255, 255, 255],
        "openarcanum_tint_rgb": [255, 255, 255],
    },
    "derived_and_resistance_rows": {
        "source_font_id": 229,
        "openarcanum_font_id": 229,
        "source_tint_rgb": [255, 255, 255],
        "openarcanum_tint_rgb": [255, 255, 255],
    },
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--openarcanum", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--label", default="character")
    return parser.parse_args()


def open_native_png(path: Path) -> Image.Image:
    if not path.is_file():
        raise FileNotFoundError(path)
    with Image.open(path) as decoded:
        if decoded.format != "PNG":
            raise ValueError(f"{path} is {decoded.format}, not PNG")
        if decoded.size != EXPECTED_SIZE:
            raise ValueError(
                f"{path} decodes to {decoded.width}x{decoded.height}; "
                f"required native size is {EXPECTED_SIZE[0]}x{EXPECTED_SIZE[1]}"
            )
        decoded.load()
        return decoded.convert("RGB")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def sobel_edges(image: Image.Image) -> Image.Image:
    gray = np.asarray(image.convert("L"), dtype=np.float32)
    padded = np.pad(gray, 1, mode="edge")
    gx = (
        -padded[:-2, :-2] + padded[:-2, 2:]
        - 2 * padded[1:-1, :-2] + 2 * padded[1:-1, 2:]
        - padded[2:, :-2] + padded[2:, 2:]
    )
    gy = (
        padded[:-2, :-2] + 2 * padded[:-2, 1:-1] + padded[:-2, 2:]
        - padded[2:, :-2] - 2 * padded[2:, 1:-1] - padded[2:, 2:]
    )
    magnitude = np.clip(np.hypot(gx, gy) / 4.0, 0, 255).astype(np.uint8)
    return Image.fromarray(magnitude, mode="L")


def save_outputs(source: Image.Image, target: Image.Image, output: Path,
                 raw_source: Path | None = None, raw_target: Path | None = None) -> None:
    output.mkdir(parents=True, exist_ok=True)
    if raw_source is not None and raw_target is not None:
        shutil.copyfile(raw_source, output / "01_source.png")
        shutil.copyfile(raw_target, output / "02_openarcanum.png")
    else:
        source.save(output / "01_source.png", compress_level=9)
        target.save(output / "02_openarcanum.png", compress_level=9)

    side = Image.new("RGB", (source.width + target.width, max(source.height, target.height)))
    side.paste(source, (0, 0))
    side.paste(target, (source.width, 0))
    side.save(output / "03_side_by_side.png", compress_level=9)

    Image.blend(source, target, 0.5).save(output / "04_overlay_50.png", compress_level=9)
    ImageChops.difference(source, target).save(output / "05_difference.png", compress_level=9)

    source_edges = sobel_edges(source)
    target_edges = sobel_edges(target)
    source_edges.save(output / "06_edges_source.png", compress_level=9)
    target_edges.save(output / "07_edges_openarcanum.png", compress_level=9)
    source_array = np.asarray(source_edges, dtype=np.uint8)
    target_array = np.asarray(target_edges, dtype=np.uint8)
    edge_overlay = np.stack((source_array, target_array, target_array), axis=2)
    Image.fromarray(edge_overlay, mode="RGB").save(output / "08_edges_overlay.png", compress_level=9)


def connected_components(mask: np.ndarray) -> Iterable[tuple[int, tuple[int, int, int, int]]]:
    height, width = mask.shape
    visited = np.zeros_like(mask, dtype=bool)
    for y in range(height):
        for x in range(width):
            if not mask[y, x] or visited[y, x]:
                continue
            queue = deque([(x, y)])
            visited[y, x] = True
            count = 0
            left = right = x
            top = bottom = y
            while queue:
                current_x, current_y = queue.popleft()
                count += 1
                left = min(left, current_x)
                right = max(right, current_x)
                top = min(top, current_y)
                bottom = max(bottom, current_y)
                for next_x, next_y in (
                    (current_x - 1, current_y), (current_x + 1, current_y),
                    (current_x, current_y - 1), (current_x, current_y + 1),
                ):
                    if (0 <= next_x < width and 0 <= next_y < height
                            and mask[next_y, next_x] and not visited[next_y, next_x]):
                        visited[next_y, next_x] = True
                        queue.append((next_x, next_y))
            yield count, (left, top, right + 1, bottom + 1)


def largest_component_bounds(mask: np.ndarray, origin: tuple[int, int], minimum_pixels: int) -> tuple[int, int, int, int]:
    candidates = [component for component in connected_components(mask) if component[0] >= minimum_pixels]
    if not candidates:
        raise ValueError("no rendered component met the configured pixel threshold")
    _, bounds = max(candidates, key=lambda candidate: candidate[0])
    left, top, right, bottom = bounds
    return left + origin[0], top + origin[1], right + origin[0], bottom + origin[1]


def union_component_bounds(mask: np.ndarray, origin: tuple[int, int], minimum_pixels: int) -> tuple[int, int, int, int]:
    bounds = [component[1] for component in connected_components(mask) if component[0] >= minimum_pixels]
    if not bounds:
        raise ValueError("no rendered glyph component met the configured pixel threshold")
    return (
        min(bound[0] for bound in bounds) + origin[0],
        min(bound[1] for bound in bounds) + origin[1],
        max(bound[2] for bound in bounds) + origin[0],
        max(bound[3] for bound in bounds) + origin[1],
    )


def measure_bounds(image: Image.Image, control: ControlRegion) -> dict[str, object]:
    field = np.asarray(image.crop(control.field_roi), dtype=np.uint8)
    if control.field_kind == "red":
        red = field[:, :, 0].astype(np.int16)
        green = field[:, :, 1].astype(np.int16)
        blue = field[:, :, 2].astype(np.int16)
        field_mask = (red >= 55) & (red >= green * 13 // 10) & (red >= blue * 13 // 10)
    elif control.field_kind == "blue":
        red = field[:, :, 0].astype(np.int16)
        green = field[:, :, 1].astype(np.int16)
        blue = field[:, :, 2].astype(np.int16)
        field_mask = (blue >= 55) & (blue >= red * 13 // 10) & (blue >= green * 13 // 10)
    elif control.visible_oval:
        # The central black oval is the largest contiguous near-black region.
        # Measuring the union would incorrectly include the separate +/- wells.
        field_brightness = field.max(axis=2)
        field_mask = field_brightness <= 24
    else:
        field_brightness = field.max(axis=2)
        field_mask = field_brightness <= 48
    field_bounds = (largest_component_bounds(field_mask, control.field_roi[:2], 200)
                    if control.visible_oval else
                    union_component_bounds(
                        field_mask,
                        control.field_roi[:2],
                        minimum_pixels=4 if control.field_kind in {"red", "blue"} else 30,
                    ))

    numeral = np.asarray(image.crop(control.numeral_roi), dtype=np.int16)
    numeral_min = numeral.min(axis=2)
    numeral_max = numeral.max(axis=2)
    numeral_mask = (numeral_min >= 80) & ((numeral_max - numeral_min) <= 90)
    numeral_bounds = union_component_bounds(numeral_mask, control.numeral_roi[:2], minimum_pixels=1)

    def center(bounds: tuple[int, int, int, int]) -> tuple[float, float]:
        left, top, right, bottom = bounds
        return ((left + right - 1) / 2.0, (top + bottom - 1) / 2.0)

    field_center = center(field_bounds)
    numeral_center = center(numeral_bounds)
    return {
        "oval_bounds": list(field_bounds),
        "oval_center": list(field_center),
        "numeral_bounds": list(numeral_bounds),
        "numeral_center": list(numeral_center),
        "numeral_minus_oval": [
            numeral_center[0] - field_center[0],
            numeral_center[1] - field_center[1],
        ],
    }


def write_measurements(source: Image.Image, target: Image.Image, output: Path) -> None:
    records: list[dict[str, object]] = []
    nested: dict[str, dict[str, object]] = {}
    for name, region in CONTROLS.items():
        try:
            source_measurement = measure_bounds(source, region)
        except ValueError as error:
            raise ValueError(f"{name} source measurement failed: {error}") from error
        try:
            target_measurement = measure_bounds(target, region)
        except ValueError as error:
            raise ValueError(f"{name} OpenArcanum measurement failed: {error}") from error
        nested[name] = {"source": source_measurement, "openarcanum": target_measurement}
        for build, measurement in (("source", source_measurement), ("openarcanum", target_measurement)):
            records.append({
                "control": name,
                "build": build,
                "oval_bounds": ",".join(map(str, measurement["oval_bounds"])),
                "oval_center_x": measurement["oval_center"][0],
                "oval_center_y": measurement["oval_center"][1],
                "numeral_bounds": ",".join(map(str, measurement["numeral_bounds"])),
                "numeral_center_x": measurement["numeral_center"][0],
                "numeral_center_y": measurement["numeral_center"][1],
                "numeral_delta_x": measurement["numeral_minus_oval"][0],
                "numeral_delta_y": measurement["numeral_minus_oval"][1],
            })

    with (output / "measurements.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(records[0].keys()))
        writer.writeheader()
        writer.writerows(records)
    (output / "measurements.json").write_text(json.dumps(nested, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    args = parse_args()
    source = open_native_png(args.source)
    target = open_native_png(args.openarcanum)
    args.output.mkdir(parents=True, exist_ok=True)

    save_outputs(source, target, args.output, args.source, args.openarcanum)
    crop_manifest: dict[str, list[int]] = {}
    crops = SHOPKEEPER_CROPS if args.label == "starting-shopkeeper" else CROPS
    for name, bounds in crops.items():
        crop_manifest[name] = list(bounds)
        save_outputs(source.crop(bounds), target.crop(bounds), args.output / "crops" / name)

    if args.label != "starting-shopkeeper":
        write_measurements(source, target, args.output)
        (args.output / "font_manifest.json").write_text(
            json.dumps(FONT_MANIFEST, indent=2) + "\n", encoding="utf-8")
    metadata = {
        "label": args.label,
        "required_dimensions": list(EXPECTED_SIZE),
        "source": {
            "path": str(args.source.resolve()),
            "decoded_dimensions": list(source.size),
            "sha256": sha256(args.source),
        },
        "openarcanum": {
            "path": str(args.openarcanum.resolve()),
            "decoded_dimensions": list(target.size),
            "sha256": sha256(args.openarcanum),
        },
        "crops": crop_manifest,
        "notes": [
            "No input was resized or color-corrected.",
            "Measurements are extracted from rendered pixels inside declared regions.",
            "No perceptual or acceptance score is produced.",
        ],
    }
    (args.output / "metadata.json").write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(metadata, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

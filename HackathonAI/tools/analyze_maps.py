#!/usr/bin/env python3
import base64
import json
import mimetypes
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:
    Image = None
    ImageDraw = None


OPENAI_ENDPOINT = "https://api.openai.com/v1/responses"
DEFAULT_MODEL = os.environ.get("OPENAI_VISION_MODEL", "gpt-4o-mini")

def load_dotenv(project_root: Path) -> None:
    env_path = project_root / ".env"
    if not env_path.exists():
        return

    for raw_line in env_path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue

        key, value = line.split("=", 1)
        key = key.strip()
        value = value.strip().strip('"').strip("'")
        os.environ.setdefault(key, value)


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def read_json(path: Path):
    return json.loads(read_text(path))


def image_data_url(path: Path) -> str:
    mime_type = mimetypes.guess_type(path.name)[0] or "image/png"
    encoded = base64.b64encode(path.read_bytes()).decode("ascii")
    return f"data:{mime_type};base64,{encoded}"


def resolve_path(project_root: Path, maybe_relative: str) -> Path:
    path = Path(maybe_relative)
    if path.is_absolute():
        return path
    return project_root / path


def call_openai(payload: dict, retries: int = 3) -> str:
    api_key = os.environ.get("OPENAI_API_KEY")
    if not api_key:
        raise RuntimeError("OPENAI_API_KEY environment variable is not set.")

    for attempt in range(1, retries + 1):
        request = urllib.request.Request(
            OPENAI_ENDPOINT,
            data=json.dumps(payload).encode("utf-8"),
            headers={
                "Authorization": f"Bearer {api_key}",
                "Content-Type": "application/json",
            },
            method="POST",
        )

        try:
            with urllib.request.urlopen(request, timeout=120) as response:
                body = json.loads(response.read().decode("utf-8"))
            break
        except urllib.error.HTTPError as error:
            detail = error.read().decode("utf-8", errors="replace")
            if error.code >= 500 and attempt < retries:
                time.sleep(attempt * 2)
                continue
            raise RuntimeError(f"OpenAI API error {error.code}: {detail}") from error

    if "output_text" in body:
        return body["output_text"]

    text_parts = []
    for item in body.get("output", []):
        for content in item.get("content", []):
            if content.get("type") == "output_text":
                text_parts.append(content.get("text", ""))

    return "\n".join(text_parts).strip()


def build_map_payload(
    map_id: str,
    structure_ascii: str,
    visual_ascii: str,
    structure_json: dict,
    tile_categories_json: dict,
    screenshot_path: Path,
) -> dict:
    prompt = f"""
You analyze a Unity 2D tilemap for procedural transition-map generation.
The screenshot is the authority for visual theme. If the map is mostly white,
icy, snowy, frozen, or contains many blue ice/snow decorations, classify it as
"snow", "ice", "winter", or "frozen" rather than generic "natural".

Return ONLY valid JSON with this shape:
{{
  "map_id": "{map_id}",
  "structure": {{
    "wall_ratio": number,
    "walkable_ratio": number,
    "closedness": number,
    "openness": number,
    "path_direction": "left_to_right|right_to_left|top_to_bottom|bottom_to_top|unknown",
    "movement_summary": string,
    "notable_structure": [string]
  }},
  "visual": {{
    "theme": string,
    "mood": string,
    "grass_ratio": number,
    "dirt_ratio": number,
    "water_ratio": number,
    "stone_or_snow_ratio": number,
    "decorative_object_summary": string,
    "dominant_features": [string]
  }},
  "tile_categories": {{
    "base": [string],
    "path": [string],
    "blocker": [string],
    "water": [string],
    "decoration": [string]
  }}
}}

ASCII map legend:
# = blocked/wall
. = walkable
S = start
G = goal
O = obstacle
? = unknown tile

Visual ASCII legend:
g = grass/green ground
p = path/road/dirt
w = water/lake
s = snow/ice
# = blocking object/border/tree
. = generic walkable
S/G/O = start/goal/obstacle

Rule-based structure JSON from Unity:
{json.dumps(structure_json, ensure_ascii=False)}

Unity Tilemap category metadata:
{json.dumps(tile_categories_json, ensure_ascii=False)}

Structure ASCII map:
{structure_ascii}

Visual ASCII map:
{visual_ascii}

Important visual rules:
- White/blue terrain that visually reads as snow or ice must increase stone_or_snow_ratio.
- Do not call a snowy/icy map only "natural"; use a specific winter/snow/ice theme.
- Distinguish green grass, brown dirt/path, blue water, gray stone, and white snow/ice.
""".strip()

    return {
        "model": DEFAULT_MODEL,
        "input": [
            {
                "role": "user",
                "content": [
                    {"type": "input_text", "text": prompt},
                    {"type": "input_image", "image_url": image_data_url(screenshot_path)},
                ],
            }
        ],
        "temperature": 0.2,
    }


def analyze_single_map(project_root: Path, map_info: dict) -> dict:
    map_id = map_info["map_id"]
    structure_ascii_path = resolve_path(project_root, map_info.get("structure_ascii_path", map_info["ascii_path"]))
    visual_ascii_path = resolve_path(project_root, map_info.get("visual_ascii_path", map_info.get("ascii_path")))
    structure_path = resolve_path(project_root, map_info["structure_json_path"])
    tile_categories_path = resolve_path(project_root, map_info.get("tile_categories_json_path", map_info["structure_json_path"]))
    screenshot_path = resolve_path(project_root, map_info["screenshot_path"])

    payload = build_map_payload(
        map_id=map_id,
        structure_ascii=read_text(structure_ascii_path),
        visual_ascii=read_text(visual_ascii_path),
        structure_json=read_json(structure_path),
        tile_categories_json=read_json(tile_categories_path),
        screenshot_path=screenshot_path,
    )
    raw_text = call_openai(payload)
    return parse_json_text(raw_text, f"{map_id} analysis")


def build_transition_payload(request_data: dict, map_a_analysis: dict, map_b_analysis: dict) -> dict:
    prompt = f"""
You plan multiple transition maps between two Unity maps.

Return ONLY valid JSON with this shape:
{{
  "transition_sequence": [
    {{
      "map_id": "transition_01",
      "theme": string,
      "theme_progress": number,
      "map_size": {{"width": integer, "height": integer}},
      "bsp": {{
        "room_count": integer,
        "split_depth": integer,
        "min_room_size": integer,
        "max_room_size": integer,
        "corridor_width": integer
      }},
      "connection": {{
        "start_side": "left|right|top|bottom",
        "goal_side": "left|right|top|bottom"
      }},
      "difficulty": {{
        "enemy_count": integer,
        "average_level": number,
        "average_range": number
      }},
      "tileset_ratio": {{
        "map_a_theme": number,
        "middle_theme": number,
        "map_b_theme": number
      }},
      "tile_blending": {{
        "base": {{
          "map_a_ratio": number,
          "map_b_ratio": number,
          "map_a_tile_examples": [string],
          "map_b_tile_examples": [string]
        }},
        "path": {{
          "map_a_ratio": number,
          "map_b_ratio": number,
          "map_a_tile_examples": [string],
          "map_b_tile_examples": [string]
        }},
        "decoration": {{
          "map_a_ratio": number,
          "map_b_ratio": number,
          "map_a_tile_examples": [string],
          "map_b_tile_examples": [string]
        }}
      }},
      "design_notes": [string]
    }}
  ]
}}

User request:
{request_data.get("user_prompt", "")}

Map A analysis:
{json.dumps(map_a_analysis, ensure_ascii=False, indent=2)}

Map B analysis:
{json.dumps(map_b_analysis, ensure_ascii=False, indent=2)}

Plan 3 transition maps unless the map analyses strongly suggest fewer.
Ensure S/G connection progresses from Map A toward Map B.
Use tile_categories to plan tile blending:
- base blends broad surface tiles, e.g. grass to snow
- path blends road/path tiles, e.g. dirt path to snow path
- decoration blends optional visual details
- blocker/water should usually stay structural and should not be blended into walkable path tiles
""".strip()

    return {
        "model": DEFAULT_MODEL,
        "input": [{"role": "user", "content": [{"type": "input_text", "text": prompt}]}],
        "temperature": 0.35,
    }


def parse_json_text(text: str, label: str):
    cleaned = text.strip()
    if cleaned.startswith("```"):
        cleaned = cleaned.strip("`")
        if cleaned.startswith("json"):
            cleaned = cleaned[4:].strip()

    try:
        return json.loads(cleaned)
    except json.JSONDecodeError as error:
        raise RuntimeError(f"Could not parse {label} as JSON:\n{cleaned}") from error


def load_visual_ascii(project_root: Path, map_info: dict) -> list[str]:
    visual_ascii_path = resolve_path(project_root, map_info.get("visual_ascii_path", map_info.get("ascii_path")))
    return read_text(visual_ascii_path).splitlines()


def average_color_from_visual_ascii(image_path: Path, visual_lines: list[str], target_markers: set[str]):
    if Image is None:
        return None

    image = Image.open(image_path).convert("RGB")
    if not visual_lines:
        return None

    grid_height = len(visual_lines)
    grid_width = max(len(line) for line in visual_lines)
    samples = []

    for y, line in enumerate(visual_lines):
        for x, marker in enumerate(line):
            if marker not in target_markers:
                continue

            px = min(image.width - 1, max(0, int((x + 0.5) / grid_width * image.width)))
            py = min(image.height - 1, max(0, int((y + 0.5) / grid_height * image.height)))
            samples.append(image.getpixel((px, py)))

    if not samples:
        return None

    return tuple(round(sum(pixel[i] for pixel in samples) / len(samples)) for i in range(3))


def sample_tile_patches(image_path: Path, visual_lines: list[str], target_markers: set[str], tile_size: int = 64, limit: int = 64):
    if Image is None:
        return []

    image = Image.open(image_path).convert("RGB")
    if not visual_lines:
        return []

    grid_height = len(visual_lines)
    grid_width = max(len(line) for line in visual_lines)
    cell_width = image.width / grid_width
    cell_height = image.height / grid_height
    patches = []

    for y, line in enumerate(visual_lines):
        for x, marker in enumerate(line):
            if marker not in target_markers:
                continue

            left = int(x * cell_width)
            top = int(y * cell_height)
            right = max(left + 1, int((x + 1) * cell_width))
            bottom = max(top + 1, int((y + 1) * cell_height))
            patch = image.crop((left, top, right, bottom)).resize((tile_size, tile_size), Image.Resampling.NEAREST)
            patches.append(patch)

            if len(patches) >= limit:
                return patches

    return patches


def load_tile_category_images(project_root: Path, map_info: dict, category: str, tile_size: int = 64):
    tile_categories_path = map_info.get("tile_categories_json_path")
    if not tile_categories_path:
        return []

    metadata = read_json(resolve_path(project_root, tile_categories_path))
    images = []

    for category_info in metadata.get("categories", []):
        if category_info.get("category") != category:
            continue

        for image_path in category_info.get("tile_image_paths", []):
            full_path = resolve_path(project_root, image_path)
            if not full_path.exists():
                continue

            image = Image.open(full_path).convert("RGBA").resize((tile_size, tile_size), Image.Resampling.NEAREST)
            background = Image.new("RGBA", image.size, (0, 0, 0, 0))
            background.alpha_composite(image)
            images.append(background.convert("RGB"))

    return images


def average_patch(patches: list, fallback_color):
    if Image is None:
        return None

    if not patches:
        return Image.new("RGB", (64, 64), fallback_color)

    width, height = patches[0].size
    output = Image.new("RGB", (width, height))
    pixels = []

    for y in range(height):
        row = []
        for x in range(width):
            values = [patch.getpixel((x, y)) for patch in patches]
            row.append(tuple(round(sum(value[i] for value in values) / len(values)) for i in range(3)))
        pixels.append(row)

    for y, row in enumerate(pixels):
        for x, color in enumerate(row):
            output.putpixel((x, y), color)

    return output


def blend_tile_images(tile_a, tile_b, map_a_ratio: float):
    if Image is None:
        return None

    map_a_ratio = max(0.0, min(1.0, map_a_ratio))
    map_b_ratio = 1.0 - map_a_ratio

    if tile_a is None and tile_b is None:
        return None
    if tile_a is None:
        return tile_b.copy()
    if tile_b is None:
        return tile_a.copy()

    tile_b = tile_b.resize(tile_a.size, Image.Resampling.NEAREST)
    output = Image.new("RGB", tile_a.size)

    for y in range(tile_a.height):
        for x in range(tile_a.width):
            a = tile_a.getpixel((x, y))
            b = tile_b.getpixel((x, y))
            output.putpixel(
                (x, y),
                tuple(round(a[i] * map_a_ratio + b[i] * map_b_ratio) for i in range(3)),
            )

    return output


def blend_color(color_a, color_b, map_a_ratio: float):
    if color_a is None and color_b is None:
        return (180, 180, 180)
    if color_a is None:
        return color_b
    if color_b is None:
        return color_a

    map_a_ratio = max(0.0, min(1.0, map_a_ratio))
    map_b_ratio = 1.0 - map_a_ratio
    return tuple(round(color_a[i] * map_a_ratio + color_b[i] * map_b_ratio) for i in range(3))


def marker_color(marker: str):
    return {
        "#": (25, 25, 25),
        "g": (74, 122, 42),
        "p": (105, 72, 50),
        "d": (105, 72, 50),
        "w": (22, 87, 102),
        "s": (218, 242, 255),
        ".": (230, 230, 230),
        "o": (80, 160, 220),
        "S": (0, 220, 0),
        "G": (230, 0, 0),
        "O": (255, 150, 0),
        "?": (140, 140, 140),
    }.get(marker, (45, 45, 45))


def render_blended_preview(
    visual_lines: list[str],
    category_markers: set[str],
    blended_color,
    output_path: Path,
    label: str,
    scale: int = 6,
):
    if Image is None:
        return

    height = len(visual_lines)
    width = max(len(line) for line in visual_lines)
    label_height = 22
    image = Image.new("RGB", (width * scale, height * scale + label_height), (32, 32, 32))
    draw = ImageDraw.Draw(image)

    for y, line in enumerate(visual_lines):
        for x, marker in enumerate(line.ljust(width)):
            color = blended_color if marker in category_markers else marker_color(marker)
            x0 = x * scale
            y0 = y * scale + label_height
            draw.rectangle([x0, y0, x0 + scale - 1, y0 + scale - 1], fill=color)

    draw.text((6, 4), label, fill=(255, 255, 255))
    image.save(output_path)


def generate_blended_preview_images(project_root: Path, request_data: dict, result: dict) -> list[dict]:
    if Image is None:
        print("Pillow is not installed; skipping generated preview images.", file=sys.stderr)
        return []

    map_a_info = request_data["map_a"]
    map_b_info = request_data["map_b"]
    map_a_visual = load_visual_ascii(project_root, map_a_info)
    map_b_visual = load_visual_ascii(project_root, map_b_info)
    map_a_image = resolve_path(project_root, map_a_info["screenshot_path"])
    map_b_image = resolve_path(project_root, map_b_info["screenshot_path"])
    output_dir = resolve_path(project_root, request_data["map_a"]["ascii_path"]).parent.parent / "generated_previews"
    output_dir.mkdir(parents=True, exist_ok=True)

    categories = {
        "base": {"g", "s", "."},
        "path": {"p", "d"},
        "decoration": {"o"},
    }
    ratios = [(70, 30), (50, 50), (30, 70)]
    manifest = []

    for category, markers in categories.items():
        color_a = average_color_from_visual_ascii(map_a_image, map_a_visual, markers)
        color_b = average_color_from_visual_ascii(map_b_image, map_b_visual, markers)

        for a_percent, b_percent in ratios:
            blended = blend_color(color_a, color_b, a_percent / 100.0)
            filename = f"{category}_generate_a_{a_percent}_b_{b_percent}.png"
            output_path = output_dir / filename
            label = f"{category}: map A {a_percent}% + map B {b_percent}%"
            render_blended_preview(map_a_visual, markers, blended, output_path, label)
            manifest.append(
                {
                    "category": category,
                    "map_a_ratio": a_percent / 100.0,
                    "map_b_ratio": b_percent / 100.0,
                    "image_path": str(output_path.relative_to(project_root)),
                    "sampled_map_a_color": color_a,
                    "sampled_map_b_color": color_b,
                    "blended_color": blended,
                }
            )

    manifest_path = output_dir / "preview_manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    return manifest


def generate_blended_tile_images(project_root: Path, request_data: dict) -> list[dict]:
    if Image is None:
        print("Pillow is not installed; skipping generated tile images.", file=sys.stderr)
        return []

    map_a_info = request_data["map_a"]
    map_b_info = request_data["map_b"]
    map_a_visual = load_visual_ascii(project_root, map_a_info)
    map_b_visual = load_visual_ascii(project_root, map_b_info)
    map_a_image = resolve_path(project_root, map_a_info["screenshot_path"])
    map_b_image = resolve_path(project_root, map_b_info["screenshot_path"])
    output_dir = resolve_path(project_root, request_data["map_a"]["ascii_path"]).parent.parent / "generated_tiles"
    output_dir.mkdir(parents=True, exist_ok=True)

    categories = {
        "base": {"g", "s", "."},
        "path": {"p", "d"},
        "decoration": {"o"},
        "water": {"w"},
    }
    fallback_colors = {
        "base": (160, 180, 130),
        "path": (110, 80, 55),
        "decoration": (80, 160, 220),
        "water": (20, 90, 110),
    }
    ratios = [(70, 30), (50, 50), (30, 70)]
    manifest = []

    for category, markers in categories.items():
        patches_a = load_tile_category_images(project_root, map_a_info, category)
        patches_b = load_tile_category_images(project_root, map_b_info, category)

        if not patches_a:
            patches_a = sample_tile_patches(map_a_image, map_a_visual, markers)

        if not patches_b:
            patches_b = sample_tile_patches(map_b_image, map_b_visual, markers)

        tile_a = average_patch(patches_a, fallback_colors[category])
        tile_b = average_patch(patches_b, fallback_colors[category])

        source_a_path = output_dir / f"{category}_source_a.png"
        source_b_path = output_dir / f"{category}_source_b.png"
        tile_a.save(source_a_path)
        tile_b.save(source_b_path)

        for a_percent, b_percent in ratios:
            blended = blend_tile_images(tile_a, tile_b, a_percent / 100.0)
            filename = f"{category}_tile_a_{a_percent}_b_{b_percent}.png"
            output_path = output_dir / filename
            blended.save(output_path)
            manifest.append(
                {
                    "category": category,
                    "map_a_ratio": a_percent / 100.0,
                    "map_b_ratio": b_percent / 100.0,
                    "tile_image_path": str(output_path.relative_to(project_root)),
                    "source_a_tile_path": str(source_a_path.relative_to(project_root)),
                    "source_b_tile_path": str(source_b_path.relative_to(project_root)),
                    "sampled_a_patch_count": len(patches_a),
                    "sampled_b_patch_count": len(patches_b),
                }
            )

    manifest_path = output_dir / "tile_manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    return manifest


def main() -> int:
    if len(sys.argv) != 2:
        print("Usage: analyze_maps.py <request.json>", file=sys.stderr)
        return 2

    request_path = Path(sys.argv[1]).resolve()
    project_root = Path.cwd()
    load_dotenv(project_root)
    request_data = read_json(request_path)

    map_a_analysis = analyze_single_map(project_root, request_data["map_a"])
    map_b_analysis = analyze_single_map(project_root, request_data["map_b"])

    transition_raw = call_openai(build_transition_payload(request_data, map_a_analysis, map_b_analysis))
    transition_plan = parse_json_text(transition_raw, "transition plan")

    result = {
        "map_a": map_a_analysis,
        "map_b": map_b_analysis,
        "transition_plan": transition_plan,
    }

    result["generated_preview_images"] = generate_blended_preview_images(project_root, request_data, result)
    result["generated_tile_images"] = generate_blended_tile_images(project_root, request_data)

    output_path = request_path.parent / "analysis_result.json"
    output_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"Analysis result written: {output_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

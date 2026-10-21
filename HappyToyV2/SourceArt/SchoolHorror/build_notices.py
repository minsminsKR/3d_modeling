"""Render original faded Korean school forms over the retained photographic paper scan.

Run from any directory with the adjacent pinned requirements-surfaces.txt environment.
No network, installed system font, or workstation-specific path is used.
"""
from pathlib import Path
import hashlib
import json
import uuid

import numpy as np
import PIL
from PIL import Image, ImageDraw, ImageFilter, ImageFont

PROJECT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
OUT = PROJECT / "Assets/Resources/SchoolHorror/notices"
FONT = PROJECT / "Assets/Resources/Fonts/Korean.ttf"
OFL = FONT.with_name("OFL.txt")
PAPER = PROJECT / "Assets/Resources/GraphicsPbr/paper-aged/albedo.png"
DIRT = PROJECT / "Assets/Resources/SchoolHorror/wall-alternate/albedo.png"
NAMESPACE = uuid.UUID("0f39c6b3-8f9f-49a6-966b-a8ec45eed318")
FORMS = [
    ("교내 안전 수칙", "학생 생활 지도부", ["복도에서는 뛰지 않습니다.", "창문과 난간에 기대지 않습니다.", "마지막 사람이 전등을 끕니다.", "파손된 시설은 선생님께 알립니다."], "1998년 7월 16일"),
    ("도서 반납 안내", "도서실", ["여름방학 전 대출 도서를", "아래 날짜까지 반납해 주세요.", "반납함은 도서실 문 옆입니다.", "이름과 학년을 꼭 확인하세요."], "반납 마감  7월 20일"),
    ("정독실 이용 안내", "학습 자료실", ["이용 시간   08:30 ~ 17:00", "책상과 의자는 제자리에 둡니다.", "개인 물품은 두고 가지 않습니다.", "퇴실 전 창문 잠금을 확인합니다."], "시설 점검 중"),
    ("가 정 통 신 문", "학부모님께", ["여름방학 생활 안내", "학생들의 안전한 생활을 위하여", "등하교 시간과 연락처를", "다시 한 번 확인해 주십시오."], "제 1998 - 47 호"),
    ("시 설 점 검 표", "행정실", ["교실 번호      점검 일자", "창문 잠금      이상 유무", "전등 상태      누수 흔적", "담당자 서명"], "본관  2층"),
    ("음악실 사용 안내", "음악 담당", ["악기는 허락을 받고 사용합니다.", "악보는 지정된 선반에 둡니다.", "의자와 악보대를 정리합니다.", "퇴실할 때는 문을 닫아 주세요."], "합창 연습  수요일 16:00"),
]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def meta(path, folder=False):
    if path.exists():
        return
    guid = uuid.uuid5(NAMESPACE, path.relative_to(PROJECT).as_posix()).hex
    if folder:
        text = f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    else:
        original = (PAPER.with_suffix(PAPER.suffix + ".meta")).read_text(encoding="utf-8")
        text = original.replace(original.split("guid: ", 1)[1].splitlines()[0], guid, 1)
        text = text.replace("maxTextureSize: 2048", "maxTextureSize: 1024").replace("aniso: 16", "aniso: 4")
    path.write_text(text, encoding="utf-8", newline="\n")


def render(index, form):
    width, height = 512, 768
    rng = np.random.default_rng(9517 + index)
    source = Image.open(PAPER).convert("RGB")
    x = (index * 211) % (source.width - 680)
    y = (index * 137) % (source.height - 1000)
    crop = source.crop((x, y, x + 680, y + 1000)).resize((width, height), Image.Resampling.LANCZOS)
    scan = np.asarray(crop).astype(np.float32) / 255
    luminance = scan.mean(axis=2)
    # Preserve actual fibres and transfer a little of the acquired damp plaster
    # scan as surface dirt. Broad tide marks remain visible at gameplay distance.
    yy, xx = np.mgrid[0:height, 0:width]
    edge = np.minimum.reduce((xx / width, (width - xx) / width, yy / height, (height - yy) / height))
    dirt_image = Image.open(DIRT).convert("RGB")
    dx, dy = (index * 193) % 900, (index * 271) % 900
    dirt_crop = dirt_image.crop((dx, dy, dx + 900, dy + 900)).resize((width, height), Image.Resampling.LANCZOS)
    dirt = np.asarray(dirt_crop).astype(np.float32).mean(axis=2) / 255
    dirt = np.clip((.54 - dirt) * 2.0, 0, 1)
    wet = np.zeros((height, width), dtype=np.float32)
    tide = np.zeros_like(wet)
    for stain in range(3):
        cx, cy = rng.uniform(-.25, 1.25) * width, rng.uniform(-.15, 1.15) * height
        rx, ry = rng.uniform(.35, .85) * width, rng.uniform(.18, .48) * height
        radius = np.sqrt(((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2)
        broken_edge = radius + .055 * np.sin(xx / 21 + np.sin(yy / 49)) + .025 * np.cos(yy / 17)
        wet = np.maximum(wet, np.clip((1.06 - broken_edge) * 5, 0, 1))
        tide += np.exp(-((broken_edge - 1.0) / .028) ** 2)
    diagonal = xx - (width * (.20 + .04 * index) + yy * .32)
    fold = np.exp(-(diagonal / 3.2) ** 2)
    fold_light = np.exp(-((diagonal - 4.5) / 2.6) ** 2)
    horizontal = np.exp(-((yy - height * .52 - 8 * np.sin(xx / 94)) / 3.2) ** 2)
    ragged_edge = np.exp(-edge * (22 + 4 * np.sin(xx / 17) + 3 * np.cos(yy / 29)))
    density = np.clip(.64 + (luminance - .7) * .48 - ragged_edge * .13
                      - wet * .13 - tide * .08 - dirt * (.035 + .065 * wet)
                      - fold * .09 + fold_light * .035 - horizontal * .045, .23, .78)
    colour = np.stack((density * .995, density * 1.015, density * .975), axis=2)
    base = Image.fromarray(np.uint8(np.clip(colour, 0, 1) * 255), "RGB")
    ink = Image.new("L", (width, height), 0)
    draw = ImageDraw.Draw(ink)
    title, subtitle, lines, footer = form
    heading = ImageFont.truetype(str(FONT), 33 if len(title) <= 9 else 29)
    small = ImageFont.truetype(str(FONT), 19)
    body = ImageFont.truetype(str(FONT), 23)
    draw.text((width // 2, 70), title, fill=190, font=heading, anchor="mm")
    draw.line((39, 105, width - 39, 105), fill=164, width=2)
    draw.text((width // 2, 134), subtitle, fill=155, font=small, anchor="mm")
    for row, line in enumerate(lines):
        y = 220 + row * 70
        draw.text((43, y), line, fill=150 + (row % 2) * 12, font=body)
        if index == 4:
            draw.line((40, y + 47, width - 40, y + 47), fill=118, width=1)
            draw.line((width // 2 + 45, y - 9, width // 2 + 45, y + 47), fill=98, width=1)
    draw.text((width // 2, 613), footer, fill=144, font=small, anchor="mm")
    draw.text((width // 2, 663), "교  무  실", fill=154, font=body, anchor="mm")
    draw.rectangle((366, 645, 425, 695), outline=103, width=3)
    draw.text((395, 668), "확인", fill=117, font=small, anchor="mm")
    alpha = np.asarray(ink.filter(ImageFilter.GaussianBlur(.32))).astype(np.float32) / 255
    # Broad soaked fold bands erase ink, correlated across each physical sheet.
    wear = np.asarray(Image.fromarray(np.uint8(rng.uniform(0, 255, (24, 16))), "L").resize((width, height), Image.Resampling.BICUBIC)).astype(np.float32) / 255
    alpha *= np.clip(.45 + wear * .55 - fold * .32 - horizontal * .20 - wet * .12, .1, 1)
    pixels = np.asarray(base).astype(np.float32)
    inkcolour = np.array([39, 43, 37] if index % 3 else [44, 46, 40], dtype=np.float32)
    pixels = pixels * (1 - alpha[..., None]) + inkcolour * alpha[..., None]
    pixels += rng.normal(0, .72, pixels.shape[:2])[..., None]
    image = Image.fromarray(np.uint8(np.clip(pixels, 0, 255)), "RGB")
    target = OUT / f"notice-{index}.png"
    image.save(target, optimize=True)
    meta(target.with_suffix(".png.meta"))
    return {"path": target.relative_to(PROJECT).as_posix(), "sha256": sha(target), "title": title, "size": [width, height]}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    meta(OUT.with_suffix(".meta"), True)
    outputs = [render(i, form) for i, form in enumerate(FORMS)]
    manifest = {
        "purpose": "Original faded Korean school notices for the derelict school; no copied document or external logo.",
        "generator": "SourceArt/SchoolHorror/build_notices.py",
        "generatorSha256": sha(Path(__file__)),
        "dependencies": "SourceArt/SchoolHorror/requirements-surfaces.txt",
        "software": {"Pillow": PIL.__version__, "numpy": np.__version__},
        "inputs": [{"path": p.relative_to(PROJECT).as_posix(), "sha256": sha(p)} for p in (PAPER, DIRT, FONT, OFL)],
        "paperCredit": "Retained paper-aged photographic PBR scan; see ThirdParty/Graphics/Graphics-Credits.txt and source manifests.",
        "dirtCredit": "Poly Haven peeling_painted_wall CC0 scan, preserved in SourceArt/SchoolHorror/Acquired; see surface-manifest.json for primary attribution, original files and hashes.",
        "fontCredit": "Retained Korean.ttf, Adobe Source font, SIL Open Font License 1.1; original OFL.txt remains beside font.",
        "method": "Scanned paper fibres and acquired plaster dirt transfer, deterministic irregular grey water tide marks and diagonal folds, original Korean text and rule forms, spatially correlated faded photocopy ink.",
        "outputs": outputs,
    }
    (HERE / "notice-manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({"status": "GENERATED", "count": len(outputs), "output": str(OUT)}, ensure_ascii=False))


if __name__ == "__main__":
    main()

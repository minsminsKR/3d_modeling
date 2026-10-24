"""Preserve the actual Baby PBR transparency keyword combinations in player builds.

Run from any directory; input textures/material and outputs are repository-relative.
Unity may append its serialized material-version metadata when importing.
"""
from pathlib import Path
import re

PROJECT = Path(__file__).resolve().parents[2]
SOURCE = PROJECT / "Assets/Resources/EnemyStudio/Baby/Skin.mat"
OUTPUT = PROJECT / "Assets/Resources/SchoolAnomalies"


def prepare() -> None:
    source = SOURCE.read_text(encoding="utf-8-sig").split("\n--- !u!114")[0].rstrip() + "\n"
    OUTPUT.mkdir(exist_ok=True)
    for emissive in (False, True):
        name = "BabyFadeEmissive" if emissive else "BabyFade"
        text = source.replace("m_Name: Skin", "m_Name: " + name)
        keywords = "  - _SURFACE_TYPE_TRANSPARENT\n" + ("  - _EMISSION\n" if emissive else "")
        text = text.replace("  m_InvalidKeywords:", keywords + "  m_InvalidKeywords:")
        text = text.replace("m_CustomRenderQueue: -1", "m_CustomRenderQueue: 3000").replace("RenderType: Opaque", "RenderType: Transparent")
        for key, value in {"_Surface": 1, "_SrcBlend": 5, "_DstBlend": 10, "_DstBlendAlpha": 10,
                           "_ZWrite": 0, "_BlendModePreserveSpecular": 0}.items():
            text, count = re.subn(r"(?m)^(    - " + re.escape(key) + r": )[^\n]+$", rf"\g<1>{value}", text)
            if count != 1:
                raise ValueError("Expected one source material property: " + key)
        if emissive:
            text = text.replace("_EmissionColor: {r: 0, g: 0, b: 0, a: 1}", "_EmissionColor: {r: 1, g: 1, b: 1, a: 1}")
        text = "\n".join(line.rstrip() for line in text.splitlines()) + "\n"
        (OUTPUT / (name + ".mat")).write_text(text, encoding="utf-8")
        print(name)


if __name__ == "__main__":
    prepare()

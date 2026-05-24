from __future__ import annotations
from pathlib import Path
from ursina import Audio


class AudioManager:
    def __init__(self, audio_root: Path | None = None, event_bus=None):
        self.audio_root = audio_root
        self.sounds = {}
        for key in ("shoot", "pistol", "smg", "rifle", "shotgun", "laser", "minigun", "rocket", "hit", "gate", "death", "upgrade", "pickup"):
            self.sounds[key] = self._load_optional(key)
        self.music = self._load_optional("music", loop=True, autoplay=True)
        if event_bus:
            event_bus.on("weapon_fired", self._on_weapon_fired)
            event_bus.on("weapon_upgraded", self._on_weapon_upgraded)

    def _load_optional(self, stem: str, loop: bool = False, autoplay: bool = False):
        if not self.audio_root or not self.audio_root.exists():
            return None
        for ext in (".wav", ".ogg", ".mp3"):
            path = self.audio_root / f"{stem}{ext}"
            if path.exists():
                try:
                    return Audio(str(path), loop=loop, autoplay=autoplay)
                except Exception:
                    return None
        return None

    def play(self, key: str, fallback: str | None = None):
        sound = self.sounds.get(key) or (self.sounds.get(fallback) if fallback else None)
        if sound:
            sound.play()

    def _on_weapon_fired(self, sound_key: str = "shoot", **_):
        self.play(sound_key, "shoot")

    def _on_weapon_upgraded(self, **_):
        self.play("upgrade", "gate")

    def play_shoot(self):
        self.play("shoot")

    def play_hit(self):
        self.play("hit")

    def play_gate(self):
        self.play("gate")

    def play_death(self):
        self.play("death")

    def play_pickup(self):
        self.play("pickup")

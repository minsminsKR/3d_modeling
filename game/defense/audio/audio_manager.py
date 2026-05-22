from __future__ import annotations
from pathlib import Path
from ursina import Audio

class AudioManager:
    def __init__(self, audio_root: Path | None = None):
        self.audio_root = audio_root
        self.shoot_sound = self._load_optional("shoot")
        self.hit_sound = self._load_optional("hit")
        self.gate_sound = self._load_optional("gate")
        self.death_sound = self._load_optional("death")
        self.music = self._load_optional("music", loop=True, autoplay=True)

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

    def play_shoot(self):
        if self.shoot_sound:
            self.shoot_sound.play()

    def play_hit(self):
        if self.hit_sound:
            self.hit_sound.play()

    def play_gate(self):
        if self.gate_sound:
            self.gate_sound.play()

    def play_death(self):
        if self.death_sound:
            self.death_sound.play()

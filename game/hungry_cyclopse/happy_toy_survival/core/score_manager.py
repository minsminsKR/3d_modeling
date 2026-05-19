from __future__ import annotations


class ScoreManager:
    def __init__(self):
        self.score = 0
        self.survival_time = 0.0
        self._time_score_accumulator = 0.0

    def update(self, dt: float, paused: bool, dead: bool):
        if paused or dead:
            return
        self.survival_time += dt
        self._time_score_accumulator += dt
        while self._time_score_accumulator >= 1.0:
            self.score += 1
            self._time_score_accumulator -= 1.0

    def add_eat_score(self):
        self.score += 10

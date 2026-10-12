"""Reproducible, asymmetric head cocks; no short periodic sway or frame noise."""
import math
import random


def make_beats(profile):
    rng = random.Random(profile['seed'])
    end = profile['duration_seconds']
    beats = [(0.0, (0.0, 0.0, 0.0))]
    time = 0.0
    sign = 1
    while time < end - 2.4:
        # A deliberate early cock is followed by unequal holds and sudden reversals.
        sign = -sign if rng.random() < .73 else sign
        roll = sign * rng.uniform(.48, 1.0) * profile['roll_degrees']
        target = (rng.uniform(-1, 1) * profile['yaw_degrees'], roll,
                  rng.uniform(-.4, 1) * profile['pitch_degrees'])
        travel = rng.choice(profile['transition_seconds'])
        hold = rng.uniform(*profile['hold_seconds'])
        if time + travel + hold > end - 1.5:
            break
        time += travel
        beats.append((time, target))
        time += hold
        beats.append((time, target))
    beats.extend([(end - .6, (0.0, 0.0, 0.0)), (end, (0.0, 0.0, 0.0))])
    return beats


def sample_degrees(beats, time):
    for (start, a), (end, b) in zip(beats, beats[1:]):
        if time <= end:
            u = max(0.0, min(1.0, (time - start) / (end - start)))
            # Smooth acceleration inside a short snap, with genuinely motionless holds.
            mix = u * u * u * (u * (u * 6 - 15) + 10)
            value = [x + (y - x) * mix for x, y in zip(a, b)]
            if a == b and any(a):
                envelope = math.sin(math.pi * u) ** 2
                value[1] += envelope * (.85 * math.sin(time * 31.7) + .5 * math.sin(time * 47.3))
            return value
    return list(beats[-1][1])

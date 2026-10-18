"""Seeded horror choreography shared by the editable Blender action and reports."""
import math
import random


def beats(profile, seed, limits, head=False):
    rng = random.Random(seed)
    result = [(0., (0., 0., 0.))]
    time = .12
    prefix = 'head' if head else 'limb'
    while time < profile['duration_seconds'] - 1.1:
        travel = rng.choice(profile[prefix + '_transition_seconds'])
        hold = rng.uniform(*profile[prefix + '_hold_seconds'])
        target = tuple(rng.uniform(-1, 1) * limit for limit in limits)
        time += travel
        result.append((time, target))
        time += hold
        result.append((time, target))
    result.extend([(profile['duration_seconds'] - .2, (0., 0., 0.)),
                   (profile['duration_seconds'], (0., 0., 0.))])
    return sorted(result)


def sample(sequence, time):
    for (start, a), (end, b) in zip(sequence, sequence[1:]):
        if time <= end:
            u = max(0., min(1., (time-start)/max(1e-9, end-start)))
            mix = u*u*(3-2*u)
            return [x+(y-x)*mix for x, y in zip(a, b)]
    return list(sequence[-1][1])


def head_pose(profile, sequence, time):
    result = sample(sequence, time)
    envelope = min(1., time/.15, (profile['duration_seconds']-time)/.2)
    # Unequal frequencies and a slow gate make violent fine tremor arrive in
    # bursts instead of adding the same periodic sway to every head cock.
    burst = .25+.75*(.5+.5*math.sin(time*3.31+1.2))**3
    for axis, amplitude in enumerate(profile['head_tremor_degrees']):
        result[axis] += amplitude*envelope*burst*(
            .67*math.sin(time*(87.1+axis*17.2))+
            .33*math.sin(time*(131.7+axis*11.4)+axis))
    return result

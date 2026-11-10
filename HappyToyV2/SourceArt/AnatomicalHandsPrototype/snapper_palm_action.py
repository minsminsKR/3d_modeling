"""Author the palm-to-finger snapper action on top of the retargeted rig.

The sampled channels keep the existing socket path and release timing. They
start from a relaxed support pose, bring thumb/index to the old precision grip,
then open the index at release and settle back to the same support pose.
"""
import copy


def smooth(value):
    value = max(0.0, min(1.0, value))
    return value * value * (3.0 - 2.0 * value)


def blend(first, second, weight):
    result = copy.deepcopy(first)
    for dst, left, right in zip(result, first, second):
        for field in ('curl', 'spread', 'twist'):
            dst[field] = round(left[field] + (right[field] - left[field]) * weight, 5)
    return result


def author(document):
    poses = {pose['name']: pose for pose in document['poses']}
    snapper = poses['Snapper']
    pinch = copy.deepcopy(snapper['bones'])
    idle = copy.deepcopy(poses['Trap']['bones'])
    # A slightly cupped hand supports the small pellet without closing around it.
    snapper['bones'] = idle
    # Reach toward the supported pellet before it begins to rise. The former
    # idle-to-pinch interpolation moved the object while both fingers were far
    # from it, making it visibly float off the palm.
    reach = copy.deepcopy(idle)
    reach_angles = {
        'thumb_1': (16, -45, -18), 'thumb_2': (38, 0, 0), 'thumb_3': (20, 0, 0),
        'index_1': (38, -8, 0), 'index_2': (60, 0, 0), 'index_3': (28, 0, 0),
    }
    for channel in reach:
        if channel['name'] in reach_angles:
            channel['curl'], channel['spread'], channel['twist'] = reach_angles[channel['name']]
    flick = copy.deepcopy(idle)
    for channel in flick:
        if channel['name'].startswith('index_'):
            channel['curl'] = {'index_1': 2, 'index_2': 5, 'index_3': 3}[channel['name']]
        if channel['name'] == 'thumb_1':
            channel.update(curl=14, spread=-42, twist=-20)
        if channel['name'] == 'thumb_2':
            channel['curl'] = 24
    open_pose = poses['Open']['bones']
    for clip in document['clips']:
        if clip['item'] != 'Snapper':
            continue
        for frame in clip['frames']:
            time = frame['time']
            if clip['name'] == 'EquipSnapper':
                frame['bones'] = blend(open_pose, idle, smooth(time / clip['duration']))
            elif clip['name'] == 'UnequipSnapper':
                frame['bones'] = blend(idle, open_pose, smooth(time / clip['duration']))
            elif clip['name'] == 'UseSnapper':
                if time <= .10:
                    frame['bones'] = blend(idle, reach, smooth(time / .10))
                elif time <= .22:
                    frame['bones'] = blend(reach, pinch, smooth((time - .10) / .12))
                elif time <= clip['releaseTime']:
                    frame['bones'] = blend(pinch, flick, smooth((time - .22) / (clip['releaseTime'] - .22)))
                else:
                    frame['bones'] = blend(flick, idle, smooth((time - clip['releaseTime']) / (clip['duration'] - clip['releaseTime'])))

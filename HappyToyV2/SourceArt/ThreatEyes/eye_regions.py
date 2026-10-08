"""Anatomical eye regions measured on the existing 800px source face crops.

All regions preserve their source pupil/iris or empty aperture centre. They do
not add eyeballs. Coordinates are source-inspection pixels, never runtime HUD.
"""
import math


def ellipse(name, centre, outer, clear, rotation=0, feature='original iris/pupil'):
    return dict(name=name, kind='ellipseAnnulus', centre=centre, outer=outer, clear=clear,
        rotation=rotation, feather=2.2, peak=.92, preservedFeature=feature)


REGIONS = {
    'Cyclopse': [ellipse('original central eye', (404, 424), (24, 12), (12.5, 10), 0)],
    'Uncat': [
        ellipse('original left eye', (345, 467), (12, 6), (7.5, 4.7), -6),
        ellipse('original right eye', (443, 454), (11, 6), (6.8, 4.7), -16),
    ],
    'Hwacat_angry': [
        ellipse('original left eye below fringe', (378, 468), (21, 6.8), (11, 4.8), 7),
        ellipse('original right eye below fringe', (532, 442), (21, 7.2), (11, 5.0), -24),
    ],
    'Baby': [
        ellipse('original left stone eyeball', (292, 394), (34, 22), (24, 16), 12, 'original white stone eyeball centre'),
        ellipse('original right stone eyeball', (468, 389), (29, 22), (21, 16), -17, 'original white stone eyeball centre'),
    ],
    'Mannequin': [
        ellipse('original left dark eye', (302, 456), (24, 14), (17, 10), -22, 'original dark eye/pupil cavity'),
        ellipse('original right dark eye', (420, 433), (21, 17), (14.5, 11.5), -15, 'original dark eye/pupil cavity'),
    ],
    'LanternMask': [
        dict(name='original left open aperture lip',kind='apertureLip',centre=(274,389),anchor=(275,418),
            feather=1.2,lipWidth=5.2,preservedFeature='open empty eye aperture',
            boundary=((207,395),(224,378),(252,364),(277,356),(301,357),(319,370),(331,389),
                (338,407),(319,413),(293,417),(267,417),(243,413),(225,406))),
        dict(name='original right open aperture lip',kind='apertureLip',centre=(516,387),anchor=(511,418),
            feather=1.2,lipWidth=5.2,preservedFeature='open empty eye aperture',
            boundary=((457,392),(474,377),(495,364),(516,355),(532,353),(549,366),(568,383),
                (575,394),(559,406),(538,414),(515,417),(492,414),(473,407))),
    ],
}

# Larger existing stone/central eye surfaces get a broader graded edge, while
# the narrower human eye windows keep enough side glow to remain identifiable.
REGIONS['Cyclopse'][0].update(feather=3.0,peak=.92)
for region in REGIONS['Uncat']: region.update(feather=1.6,peak=.92)
for region in REGIONS['Baby']: region.update(feather=3.4,peak=.82)
for region in REGIONS['LanternMask']: region.update(feather=2.2,peak=.88)


def outer_samples(region, samples=40):
    if region['kind']=='apertureLip': return list(region['boundary'])
    angle=math.radians(region['rotation']); c,s=math.cos(angle),math.sin(angle)
    result=[]
    for index in range(samples):
        theta=index*math.tau/samples; x=region['outer'][0]*math.cos(theta); y=region['outer'][1]*math.sin(theta)
        result.append((region['centre'][0]+c*x-s*y,region['centre'][1]+s*x+c*y))
    return result

# Award poster — work in progress

`normal.png` is a fictional Korean student portrait generated with the built-in
image generation tool on 2026-11-01. `anomaly.png` is an edit of that image,
requesting the same framing and clothing with pale skin, dark eyes and an
unnatural smile. No actual person was used as a reference.

Run `python HappyToyV2/SourceArt/SchoolAwardPoster/make_posters.py` with Pillow
installed to build matching Korean-text poster images, using the existing
project font. The two output images have been visually reviewed as source art;
they have not yet been imported or verified in Unity.

`anomaly-ghost.png` is a stronger haunted-photograph edit requested on
2026-11-01; the same script creates `poster-anomaly-ghost.png` from it.
The source is imported as a texture and `SchoolAwardPosterPresentation` switches
the three loop-section copies in response to the anomaly director. A visual
in-game review remains pending.

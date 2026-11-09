# First-person hand and grip reference review

Reviewed 2026-11-10. These are visual references, not assets imported into the
game. The game images linked below remain the property of their respective
owners. Do not copy their models or textures into the project.

## Inspected references

* **Resident Evil Requiem (2026), lighter in first person:** the
  [IGN gameplay article and screenshots](https://tech.yahoo.com/gaming/articles/brief-look-resident-evil-requiem-223617232.html)
  show the small lighter held near the lower left of the view. Use the visible
  thumb, curled fingers and compact hand/object silhouette as a reference for a
  small held object and first-person framing. The image is relatively dark and
  does not resolve individual fingertip contact; do not infer hidden geometry.
  The [official PlayStation page](https://www.playstation.com/en-us/games/resident-evil-requiem/)
  identifies the game and its 2026 release.
* **Amnesia: The Bunker (2023), dynamo flashlight:** this
  [gameplay image](https://img.bolumsonucanavari.com/images/Upload/34b0f500-7031-41cb-9d55-0f981dc978c8.jpg)
  and its [article](https://m.bolumsonucanavari.com/Haberler-SOMA_gelistiricilerinden_Amnesia_The_Bunker_bizleri_korku_dolu_ortama_goturuyor-117726.htm)
  show one hand supporting the light while the other operates it, with visible
  irregular skin tone and wrapped wrists. It is an older comparison, and its
  dynamo has a different shape from our cylindrical flashlight; do not copy its
  exact grip or assume the hidden fingertips are in contact.
* **User-supplied paper grip:** `References/talisman-grip-user-20261109.jpg` is
  the primary reference for the talisman. The thumb presses the front near the
  lower-right corner, the index supports from behind, and the sheet rises above
  and left of the grip. Other fingers relax toward the palm. Preserve this
  lower-corner orientation even if another general description allows the top.

## Current comparison and acceptance targets

The current view was inspected in `Builds/HandRigAudit-20261109/`, using the
hand-camera captures rather than a complete environment composite. The prior
flashlight comparison shows the fingers as thick, nearly parallel bands; the
talisman image shows a circular thumb/index opening instead of the compact pad
contact in the user's photograph. Skin and nail detail is difficult to read.
These captures do not prove what happens in every animation frame.

For the next art pass:

1. Fit the existing Blender base's joints to the actual finger centerlines and
   repaint weights across joints and the thumb web. Inspect deformations from
   palm, back and side, including self-intersections and volume loss.
2. Author skin, nail beds and nails in the mesh UVs, with appropriate roughness
   and normal detail; check seams and skin color under the real scene lighting.
   The current v7 texture is a generated front/back projection and is not a
   scanned or finished skin material.
3. For the flashlight, separately shape each finger's contact around the
   measured handle and make the opposing thumb and hand-to-wrist silhouette
   readable. Test the skin surface against the actual item over animation, not
   just fingertip markers or the current static cylindrical approximation.
4. For the talisman, use the user's lower-corner photo as the explicit camera
   target: opposed thumb/index pads on the front and back, remaining fingers
   naturally flexed, and no circular gap or paper penetration.
5. Give snapper, incense and trap individually authored grips and review each
   idle, equip, use, release and movement frame. Existing grip catalog and item
   behavior remain the integration boundary.
6. Compare a representative full-scene game-camera view as well as the isolated
   hand layer. Visual review, contact measurements and relevant tests are all
   required; an isolated still or passing logic tests alone do not establish
   realistic hands.

The references have been recorded, not yet applied as a new mesh, texture or
animation. No new in-game visual or collision certification is claimed.

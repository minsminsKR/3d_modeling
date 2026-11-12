# Weathered village fire notice

Generated with the built-in image generation tool, transparent background, 2026-11-12. The original unmodified PNG is preserved alongside this record and copied byte-for-byte to `Assets/Resources/AlleyNotices/fire-notice.png`. SHA-256: `72adea1602804244e9d1a1072e8070b30ac11d88a178dbf9582cf8ef827db4d4`.

Prompt:

Use case: historical-scene. Asset: an in-game wall decal for a realistic 1990s Korean residential alley. Create a single old neighborhood fire-prevention paper notice, seen absolutely straight on, flat and rectangular, filling most of the frame, with the entire slightly torn perimeter visible against a fully transparent background. Authentic thin yellowed off-white paper, subtle rain stains, creases and worn printed ink; understated and believable, not scary or stylized. Korean text must be clean and correctly spelled, with exactly two lines: large bold heading '산불 조심' and small footer '우암동 주민회'. No other writing, no numerals, no logos, no illustrations, no border or drop shadow. Soft diffuse archival scan lighting; neutral perspective, no wall or objects.

Import with `HappyToy.V2.Editor.AlleyNoticeImport.Apply` to create the cutout material. The texture is for static, non-interactive wall dressing.


## Coal delivery flyer

Generated 2026-11-12 with the built-in image generation tool; the original is `coal-delivery-original.png` and the Unity texture is its byte-for-byte copy. SHA-256: `c335df45f4cf13e2e752d616f9d7ee07d73e2d0e6174fc82cf80900794b67dbb`.

Prompt: Use case: game-asset. A single old, weathered 1990s South Korean neighborhood paper flyer, as a flat wall decal for a photorealistic Korean residential alley game. Rectangular vertically oriented pale cream paper, lightly torn corners and aged fibrous paper texture, faded black Korean ink, tiny restrained faded red accent. One clearly legible large Korean heading, exact text: "연탄 배달". Under it in smaller print exact text: "동네 어디든". No telephone number, no corporate brands, no pictures, no additional writing or garbled lettering. Show the entire paper front-on, flat with no perspective, no folds raised from the surface, no shadow, transparent background around the paper, suitable as a subtle real-world wall decal.

Copy the original to `Assets/Resources/AlleyNotices/coal-delivery.png` and run Unity editor method `HappyToy.V2.Editor.AlleyNoticeImport.ApplyCoalDelivery`. The material uses the existing fire-notice cutout settings. `AlleyWallDressing.AddCoalNotice` places it on the curved brick wall, without collision. The D3D11 village gameplay-camera review inspected the sign from the route and from an explicitly repositioned close-up; the wall-support ray and absence of a collider passed. Actual player performance and a standalone build were not tested.

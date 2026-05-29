# Lighting System: Dynamic Fluorescents & Optimization

The lighting of the Backrooms balances atmospheric dread with high rendering performance.

## Ceiling Panels
- Ceiling lights are represented as thin rectangular panels ($1.2\text{m} \times 0.6\text{m}$) embedded in the ceiling.
- Each panel utilizes a cloned, unique `MeshStandardMaterial` with a muted warm color (`0x6f674f`) and low emissive intensity. The emissive surface represents the tube itself; nearby objects are lit by the pooled real lights.

## PBR Materials & Shadows
- Characters, monsters, doors, cabinets, keys, props, walls, floors, and ceilings use light-reactive `MeshStandardMaterial` or `MeshPhysicalMaterial`-compatible materials.
- Character FBX textures are applied as `map` textures on `MeshStandardMaterial`; no monster material is unlit or self-emissive.
- The renderer uses `SRGBColorSpace`, `ACESFilmicToneMapping`, exposure `0.8`, and a soft shadow map.
- Only the player flashlight casts dynamic shadows (`512px` shadow map). Ceiling and item accent lights stay shadowless for performance.

## Dynamic Light Culling
To prevent WebGL rendering lag and light limit errors:
- We do **not** spawn a Three.js light entity for every ceiling panel.
- Instead, a small fixed `PointLight` pool is pre-allocated and assigned to the closest panels near the player.
- Each active ceiling light uses a low intensity (`0.55`), shorter range (`14m`), and stronger falloff so room lighting remains secondary to the flashlight.
- Lights further than $24\text{m}$ are parked off-screen with intensity `0`, while their muted panel meshes remain visible.

## Flickering Lights
- In flickering rooms only some panels flicker, and standard corridors have an 8% random chance per panel.
- A frame-rate independent `flickerTimer` controls light state changes:
  - **Dim State**: The panel color dips to dark beige (`0x333026`), and PointLight intensity drops to 24% of normal. (Dim duration: $0.05\text{s} - 0.21\text{s}$)
  - **On State**: The panel returns to muted warm beige (`0x6f674f`), and PointLight intensity returns to normal. (On duration: $1.4\text{s} - 6.6\text{s}$)
- This creates subtle, intermittent buzzing without making every corridor visibly blink.

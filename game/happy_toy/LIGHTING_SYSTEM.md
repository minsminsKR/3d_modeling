# Lighting System: Dynamic Fluorescents & Optimization

The lighting of the Backrooms balances atmospheric dread with high rendering performance.

## Ceiling Panels
- Ceiling lights are represented as thin rectangular panels ($1.2\text{m} \times 0.6\text{m}$) embedded in the ceiling.
- Each panel utilizes a cloned, unique `MeshBasicMaterial` with emissive capabilities (`color: 0xfffee4`), allowing individual flickering patterns without changing other lights.

## Dynamic Light Culling
To prevent WebGL rendering lag and light limit errors:
- We do **not** spawn permanent Three.js light entities for every ceiling panel.
- Instead, the distance between the player and each light panel is queried dynamically.
- A `THREE.PointLight(0xfffee2, 1.8, 14, 1.25)` is instantiated and added to the scene **only** if the light panel is within $24\text{m}$ of the player.
- Lights further than $24\text{m}$ are removed and disposed, while their emissive meshes remain visible.

## Flickering Lights
- In flickering rooms, or with a 15% random chance in standard corridors, panels are configured as flickering.
- A frame-rate independent `flickerTimer` controls light state changes:
  - **Off State**: The panel emissive color is set to dark beige (`0x3a3930`), and PointLight intensity is set to `0`. (Off duration: $0.05\text{s} - 0.25\text{s}$)
  - **On State**: Emissive color returns to bright yellow (`0xfffee4`), and PointLight intensity returns to normal. (On duration: $1.0\text{s} - 6.0\text{s}$)
- This creates realistic, organic buzzing and flickering lights throughout the corridors.

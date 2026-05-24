# Web Porting Notes

## Rendering Strategy

The Ursina version creates many Python `Entity` objects, which becomes expensive when allies and bullets grow. The web version moves the hot path to the browser GPU:

- Allies: one `InstancedMesh`
- Bullets: one `InstancedMesh`
- Enemies: one `InstancedMesh`
- Pickups: one `InstancedMesh`
- Particles: one `InstancedMesh`

Only a small number of nearby enemies and bosses receive FBX model decorators.

## Asset Mapping

| Gameplay Role | Asset |
| --- | --- |
| Player | Uncat |
| Basic enemy | Cyclopse |
| Mid boss | Hwacat |
| Big boss | Hwacat_angry |
| Ally visual style | lightweight blue crowd proxy |

FBX models are loaded with `FBXLoader`, textured with `model_textured.jpg`, scaled to target height, and grounded using a bounding box pass. The web version uses `MeshBasicMaterial` for textured character meshes because these generated textures were authored for the `happy_toy` pipeline and read too dark under standard lighting.

Verified through the Flask `/assets` route on 2026-05-23:

| Asset URL | Result |
| --- | --- |
| `/assets/characters/Uncat/mixamo/Run.fbx` | 200 |
| `/assets/characters/Uncat/source/model_textured.jpg` | 200 |
| `/assets/characters/Cyclopse/mixamo/Run.fbx` | 200 |
| `/assets/characters/Cyclopse/source/model_textured.jpg` | 200 |
| `/assets/characters/Hwacat/mixamo/Normal_standing.fbx` | 200 |
| `/assets/characters/Hwacat/source/model_textured.jpg` | 200 |
| `/assets/characters/Hwacat_angry/mixamo/Zombie%20Run.fbx` | 200 |
| `/assets/characters/Hwacat_angry/source/model_textured.jpg` | 200 |

`Zombie Run.fbx` contains a space in the filename. The loader encodes request URLs with `encodeURI`, so the browser requests it as `Zombie%20Run.fbx`.

## Character Loader Notes

- Loader status is stored in `CharacterLoader.status` and logged during development.
- FBX failures are reported with `console.error` before using the capsule fallback.
- Texture failures no longer hide model success; the FBX stays visible with a fallback material and a warning.
- Texture settings use `SRGBColorSpace` and `flipY = true`, matching the current FBX texture orientation path.
- Meshes without UVs receive a neutral fallback material and are counted in loader status.
- FBX roots are wrapped in a normalized group. The model child carries scale, centering, and grounding offsets, while gameplay code can safely set the wrapper position every frame.
- `cloneCharacter` performs a local skinned clone with bone remapping so FBX decorators keep independent skeleton bindings without requiring an extra vendor file.
- The player uses the loaded Uncat model directly, while its capsule fallback remains available if the FBX load fails.

## Model Decorator Integration

`static/src/modelLayer.js` provides an extracted `ModelDecoratorSystem` with a boss-first slot plan. With the current `LIMITS.modelDecorators = 28`, the plan starts with:

- 2 big boss slots: `hwacatAngry`
- 2 mid boss slots: `hwacat`
- remaining nearby basic enemy slots: `cyclopse`

This guarantees spawned bosses get matching FBX slots before basic enemies consume the remaining decorators. If waves later allow multiple simultaneous bosses, raise `LIMITS.modelDecorators` to 14-16 and keep at least two slots per boss class.

Minimal `main.js` integration:

```js
import { CharacterLoader } from "./characterLoader.js";
import { ModelDecoratorSystem } from "./modelLayer.js";
```

Then remove the local `ModelDecoratorSystem` and `importance` definitions from `main.js`; the existing constructor call can stay:

```js
this.models = new ModelDecoratorSystem(this.scene, this.loader);
```

The model layer exposes `this.models.isDecorated(enemy)`, but the current gameplay keeps the lightweight enemy proxy visible as a readability and fallback layer. This prevents a failed or delayed FBX decorator from making monsters disappear.

## Performance Rules

- No per-bullet mesh creation during gameplay.
- No per-hit DOM allocation.
- Collision uses simple X/Z distance checks.
- Far allies update less often.
- Model decorators are limited and reused.
- Effects are short-lived instanced particles, not individual meshes.

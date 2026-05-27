# Assets

## Asset Root

`E:\AI\3d_modeling\game\assets`

## Auto-Detected Characters

- Cyclopse: `characters/Cyclopse/mixamo/Run.fbx`
- Hwacat: `characters/Hwacat/mixamo/Hip Hop Dancing.fbx`
- Uncat: `characters/Uncat/mixamo/Run.fbx`
- Hwacat_angry: `characters/Hwacat_angry/mixamo/Zombie Run.fbx`

Textures are searched from each character's `source/model_textured` file.

## Props

All `.glb` files under the asset root that look like props are collected and randomly placed in chunks. If no props are found or a prop fails to load, the game uses a cube fallback.

## Changing Mappings

Edit `core/config.py`:

- `ASSET_ROOT`
- `character_specs`
- texture search keywords
- prop filtering

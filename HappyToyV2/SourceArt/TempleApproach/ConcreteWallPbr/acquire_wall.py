from pathlib import Path
import importlib.util
import json

root = Path(__file__).resolve().parent
shared = root.parent.parent / 'GraphicsUpgrade' / 'acquire_pbr.py'
spec = importlib.util.spec_from_file_location('pbr_acquire', shared)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
module.ROOT = root
module.DEST = root / 'staged'
result = module.acquire('alley-concrete-wall', 'concrete_wall_007')
(root / 'manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')

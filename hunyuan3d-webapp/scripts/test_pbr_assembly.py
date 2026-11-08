import tempfile
import unittest
from pathlib import Path

import torch
import trimesh

from dual_bake import assemble


class PBRAssemblyTests(unittest.TestCase):
    def test_two_tiles_make_full_size_pbr_glb(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            raw = root / "raw.pt"
            uv = root / "uv.pt"
            torch.save({"layout": {"base_color": slice(0, 3), "metallic": slice(3, 4),
                                   "roughness": slice(4, 5), "alpha": slice(5, 6)}}, raw)
            torch.save({"vertices": torch.tensor([[0., 0., 0.], [1., 0., 0.], [0., 1., 0.]]),
                        "faces": torch.tensor([[0, 1, 2]], dtype=torch.int32),
                        "uvs": torch.tensor([[0., 0.], [1., 0.], [0., 1.]]),
                        "normals": torch.tensor([[0., 0., 1.]] * 3)}, uv)
            paths = []
            for start in (0, 2):
                attrs = torch.zeros((2, 4, 6))
                attrs[..., :3] = 0.4
                attrs[..., 3] = 0.2
                attrs[..., 4] = 0.6
                attrs[..., 5] = 1.0
                mask = torch.ones((2, 4), dtype=torch.bool)
                if start == 0:
                    mask[1, 1] = False
                path = root / f"tile{start}.pt"
                torch.save({"attrs": attrs, "mask": mask, "row_start": start,
                            "row_end": start + 2, "size": 4}, path)
                paths.append(path)
            output = root / "model.glb"
            result = assemble(raw, uv, paths, output)
            self.assertEqual(result["texture_size"], 4)
            self.assertEqual(result["valid_pixels"], 15)
            scene = trimesh.load(output, force="scene")
            geometry = next(iter(scene.geometry.values()))
            self.assertEqual(geometry.visual.material.baseColorTexture.size, (4, 4))
            self.assertEqual(geometry.visual.material.metallicRoughnessTexture.size, (4, 4))


if __name__ == "__main__":
    unittest.main()

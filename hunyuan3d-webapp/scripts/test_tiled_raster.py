import unittest

import torch

from dual_bake import rasterize_tile


@unittest.skipUnless(torch.cuda.is_available(), "CUDA required")
class TileRasterTests(unittest.TestCase):
    def test_global_face_ids_and_barycentrics_match_full_raster(self):
        uvs = torch.tensor([[0.1, 0.1], [0.9, 0.1], [0.1, 0.9],
                            [0.9, 0.9], [0.4, 0.5]], device="cuda")
        faces = torch.tensor([[0, 1, 4], [1, 3, 4], [3, 2, 4], [2, 0, 4]],
                             dtype=torch.int32, device="cuda")
        full = rasterize_tile(uvs, faces, 64, 0, 64, chunk_size=2)
        tiled = torch.cat([rasterize_tile(uvs, faces, 64, 0, 32, chunk_size=2),
                           rasterize_tile(uvs, faces, 64, 32, 64, chunk_size=2)], dim=1)
        self.assertTrue(torch.equal(full, tiled))
        self.assertGreaterEqual(int(full[..., 3].max().item()), 3)


if __name__ == "__main__":
    unittest.main()

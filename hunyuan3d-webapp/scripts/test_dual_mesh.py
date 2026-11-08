import tempfile
import unittest
from pathlib import Path

import numpy as np
import trimesh

from dual_mesh import merge, partition


class PartitionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "parts"
        mesh = trimesh.creation.icosphere(subdivisions=2)
        self.vertices = mesh.vertices.astype(np.float32)
        self.faces = mesh.faces.astype(np.int32)
        self.manifest = partition(self.vertices, self.faces, self.root, 4)
        for shard in range(4):
            np.save(self.root / f"out_vertices_{shard}.npy", np.load(self.root / f"vertices_{shard}.npy"))
            np.save(self.root / f"out_faces_{shard}.npy", np.load(self.root / f"faces_{shard}.npy"))
            ids = np.load(self.root / f"ids_{shard}.npy")
            seam_ids = np.load(self.root / "seam_ids.npy")
            np.save(self.root / f"out_seam_ids_{shard}.npy",
                    np.where(np.isin(ids, seam_ids), ids, -1).astype(np.int32))

    def test_exact_weld_preserves_closed_oriented_mesh(self):
        vertices, faces = merge(self.root)
        self.assertEqual(len(faces), len(self.faces))
        self.assertEqual(len(vertices), len(self.vertices))
        self.assertTrue(trimesh.Trimesh(vertices=vertices, faces=faces, process=False).is_watertight)

    def test_moved_seam_rejected(self):
        seam = np.load(self.root / "seam_vertices.npy")[0]
        for shard in range(4):
            vertices = np.load(self.root / f"out_vertices_{shard}.npy")
            hit = np.flatnonzero(np.all(vertices == seam, axis=1))
            if len(hit):
                vertices[hit[0], 0] += 0.001
                np.save(self.root / f"out_vertices_{shard}.npy", vertices)
                break
        with self.assertRaisesRegex(ValueError, "seam"):
            merge(self.root)

    def test_reversed_face_rejected(self):
        faces = np.load(self.root / "out_faces_0.npy")
        faces[0] = faces[0, ::-1]
        np.save(self.root / "out_faces_0.npy", faces)
        with self.assertRaisesRegex(ValueError, "orientations"):
            merge(self.root)

    def test_hole_rejected(self):
        faces = np.load(self.root / "out_faces_0.npy")
        np.save(self.root / "out_faces_0.npy", faces[1:])
        with self.assertRaisesRegex(ValueError, "Shared seam edge|non-manifold/boundary"):
            merge(self.root)

    def test_interior_seam_coordinate_collision_preserves_distinct_ids(self):
        vertices = self.vertices.copy()
        seam_ids = np.load(self.root / "seam_ids.npy")
        interior_id = int(np.flatnonzero(~np.isin(np.arange(len(vertices)), seam_ids))[0])
        vertices[interior_id] = vertices[seam_ids[0]]
        root = self.root.parent / "collision"
        partition(vertices, self.faces, root, 4)
        for shard in range(4):
            np.save(root / f"out_vertices_{shard}.npy", np.load(root / f"vertices_{shard}.npy"))
            np.save(root / f"out_faces_{shard}.npy", np.load(root / f"faces_{shard}.npy"))
            ids = np.load(root / f"ids_{shard}.npy")
            seam_ids = np.load(root / "seam_ids.npy")
            np.save(root / f"out_seam_ids_{shard}.npy",
                    np.where(np.isin(ids, seam_ids), ids, -1).astype(np.int32))
        merged_vertices, merged_faces = merge(root)
        self.assertEqual(len(merged_vertices), len(vertices))
        self.assertEqual(len(merged_faces), len(self.faces))

    def test_two_seam_ids_can_share_coordinates(self):
        vertices = self.vertices.copy()
        seam_ids = np.load(self.root / "seam_ids.npy")
        vertices[seam_ids[1]] = vertices[seam_ids[0]]
        root = self.root.parent / "duplicate-seam-position"
        partition(vertices, self.faces, root, 4)
        for shard in range(4):
            np.save(root / f"out_vertices_{shard}.npy", np.load(root / f"vertices_{shard}.npy"))
            np.save(root / f"out_faces_{shard}.npy", np.load(root / f"faces_{shard}.npy"))
            ids = np.load(root / f"ids_{shard}.npy")
            np.save(root / f"out_seam_ids_{shard}.npy",
                    np.where(np.isin(ids, np.load(root / "seam_ids.npy")), ids, -1).astype(np.int32))
        merged_vertices, merged_faces = merge(root)
        self.assertEqual(len(merged_vertices), len(vertices))
        self.assertEqual(len(merged_faces), len(self.faces))

    def test_open_source_only_allowed_as_intermediate(self):
        root = self.root.parent / "open-source"
        partition(self.vertices, self.faces[1:], root, 4)
        for shard in range(4):
            np.save(root / f"out_vertices_{shard}.npy", np.load(root / f"vertices_{shard}.npy"))
            np.save(root / f"out_faces_{shard}.npy", np.load(root / f"faces_{shard}.npy"))
            ids = np.load(root / f"ids_{shard}.npy")
            seam_ids = np.load(root / "seam_ids.npy")
            np.save(root / f"out_seam_ids_{shard}.npy",
                    np.where(np.isin(ids, seam_ids), ids, -1).astype(np.int32))
        _, faces = merge(root, require_closed=False)
        self.assertEqual(len(faces), len(self.faces) - 1)
        with self.assertRaisesRegex(ValueError, "non-manifold/boundary"):
            merge(root)


if __name__ == "__main__":
    unittest.main()

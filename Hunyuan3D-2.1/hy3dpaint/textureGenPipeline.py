# Hunyuan 3D is licensed under the TENCENT HUNYUAN NON-COMMERCIAL LICENSE AGREEMENT
# except for the third-party components listed below.
# Hunyuan 3D does not impose any additional limitations beyond what is outlined
# in the repsective licenses of these third-party components.
# Users must comply with all terms and conditions of original licenses of these third-party
# components and must ensure that the usage of the third party components adheres to
# all relevant laws and regulations.

# For avoidance of doubts, Hunyuan 3D means the large language models and
# their software and algorithms, including trained model weights, parameters (including
# optimizer states), machine-learning model code, inference-enabling code, training-enabling code,
# fine-tuning enabling code and other elements of the foregoing made publicly available
# by Tencent in accordance with TENCENT HUNYUAN COMMUNITY LICENSE AGREEMENT.

import os
import torch
import copy
import trimesh
import cv2
from pathlib import Path
import numpy as np
from PIL import Image
from typing import List
from DifferentiableRenderer.MeshRender import MeshRender
from utils.simplify_mesh_utils import remesh_mesh
from utils.multiview_utils import multiviewDiffusionNet
from utils.pipeline_utils import ViewProcessor
from utils.image_super_utils import imageSuperNet
from utils.uvwrap_utils import mesh_uv_wrap
from DifferentiableRenderer.mesh_utils import convert_obj_to_glb
import warnings

warnings.filterwarnings("ignore")
from diffusers.utils import logging as diffusers_logging

diffusers_logging.set_verbosity(50)


class Hunyuan3DPaintConfig:
    def __init__(self, max_num_view, resolution):
        self.device = "cuda"

        self.multiview_cfg_path = "hy3dpaint/cfgs/hunyuan-paint-pbr.yaml"
        self.custom_pipeline = "hunyuanpaintpbr"
        self.multiview_pretrained_path = "tencent/Hunyuan3D-2.1"
        self.dino_ckpt_path = "facebook/dinov2-giant"
        self.realesrgan_ckpt_path = "ckpt/RealESRGAN_x4plus.pth"

        self.raster_mode = "cr"
        self.bake_mode = "back_sample"
        self.render_size = 1024 * 2
        self.texture_size = 1024 * 4
        self.max_selected_view_num = max_num_view
        self.resolution = resolution
        self.bake_exp = 4
        self.merge_method = "fast"

        # view selection
        self.candidate_camera_azims = [0, 90, 180, 270, 0, 180]
        self.candidate_camera_elevs = [0, 0, 0, 0, 90, -90]
        self.candidate_view_weights = [1, 0.1, 0.5, 0.1, 0.05, 0.05]

        for azim in range(0, 360, 30):
            self.candidate_camera_azims.append(azim)
            self.candidate_camera_elevs.append(20)
            self.candidate_view_weights.append(0.01)

            self.candidate_camera_azims.append(azim)
            self.candidate_camera_elevs.append(-20)
            self.candidate_view_weights.append(0.01)


class Hunyuan3DPaintPipeline:

    def __init__(self, config=None) -> None:
        self.config = config if config is not None else Hunyuan3DPaintConfig()
        self.models = {}
        self.stats_logs = {}
        self.render = MeshRender(
            default_resolution=self.config.render_size,
            texture_size=self.config.texture_size,
            bake_mode=self.config.bake_mode,
            raster_mode=self.config.raster_mode,
            device=getattr(self.config, "render_device", "cuda"),
        )
        self.view_processor = ViewProcessor(self.config, self.render)

    def load_models(self):
        if self.models:
            return
        torch.cuda.empty_cache()
        self.models["super_model"] = imageSuperNet(self.config)
        self.models["multiview_model"] = multiviewDiffusionNet(self.config)
        print("Models Loaded.")

    def _fallback_project_texture(self, image_prompt, camera_elevs, camera_azims):
        candidate_views = list(dict.fromkeys(zip(camera_elevs, camera_azims)))
        if not candidate_views:
            candidate_views = list(zip(self.config.candidate_camera_elevs, self.config.candidate_camera_azims))

        best_texture = None
        best_mask = None
        best_score = -1
        source_image = image_prompt[0]
        for camera_elev, camera_azim in candidate_views[:12]:
            project_texture, project_cos_map, _ = self.render.back_project(source_image, camera_elev, camera_azim)
            score = int(torch.count_nonzero(project_cos_map).item())
            if score > best_score:
                best_score = score
                best_texture = project_texture
                best_mask = project_cos_map > 1e-8

        if best_texture is None:
            best_texture = torch.zeros(self.render.texture_size + (3,), device=self.render.device)
            best_mask = torch.zeros(self.render.texture_size + (1,), dtype=torch.bool, device=self.render.device)
        return best_texture, best_mask

    def _planar_projection_texture(self, mesh, image_prompt):
        source = image_prompt[0].convert("RGBA")
        source_rgba = np.array(source)

        alpha = source_rgba[..., 3]
        if np.any(alpha > 0):
            ys, xs = np.where(alpha > 0)
            image_aspect = max(1.0, (xs.max() - xs.min() + 1) / max(1, ys.max() - ys.min() + 1))
        else:
            image_aspect = max(1.0, source_rgba.shape[1] / max(1, source_rgba.shape[0]))

        vertices = np.asarray(mesh.vertices, dtype=np.float32)
        faces = np.asarray(mesh.faces, dtype=np.int32)
        uvs = np.asarray(mesh.visual.uv, dtype=np.float32)
        tex_h, tex_w = self.render.texture_size

        axis_candidates = [(0, 2), (0, 1), (1, 2)]
        best_axes = axis_candidates[0]
        best_score = float("inf")
        for axes in axis_candidates:
            projected = vertices[:, axes]
            extent = projected.max(axis=0) - projected.min(axis=0)
            aspect = max(extent[0], 1e-6) / max(extent[1], 1e-6)
            score = abs(np.log(max(aspect, 1e-6)) - np.log(max(image_aspect, 1e-6)))
            if score < best_score:
                best_score = score
                best_axes = axes

        projected = vertices[:, best_axes]
        projected -= projected.min(axis=0, keepdims=True)
        extent = projected.max(axis=0, keepdims=True)
        extent[extent < 1e-6] = 1.0
        projected = projected / extent
        projected[:, 1] = 1.0 - projected[:, 1]
        src_points = np.empty_like(projected, dtype=np.float32)
        src_points[:, 0] = projected[:, 0] * (source_rgba.shape[1] - 1)
        src_points[:, 1] = projected[:, 1] * (source_rgba.shape[0] - 1)

        uv_points = np.empty_like(uvs, dtype=np.float32)
        uv_points[:, 0] = uvs[:, 0] * (tex_w - 1)
        uv_points[:, 1] = (1.0 - uvs[:, 1]) * (tex_h - 1)

        texture = np.zeros((tex_h, tex_w, 3), dtype=np.uint8)
        coverage = np.zeros((tex_h, tex_w), dtype=np.uint8)

        for face in faces:
            src_tri = src_points[face]
            dst_tri = uv_points[face]
            if abs(cv2.contourArea(dst_tri.astype(np.float32))) < 1.0:
                continue

            src_rect = cv2.boundingRect(src_tri.astype(np.float32))
            dst_rect = cv2.boundingRect(dst_tri.astype(np.float32))
            if src_rect[2] <= 0 or src_rect[3] <= 0 or dst_rect[2] <= 0 or dst_rect[3] <= 0:
                continue

            src_patch = source_rgba[
                src_rect[1] : src_rect[1] + src_rect[3], src_rect[0] : src_rect[0] + src_rect[2]
            ]
            if src_patch.size == 0:
                continue

            src_tri_rect = src_tri - np.array([src_rect[0], src_rect[1]], dtype=np.float32)
            dst_tri_rect = dst_tri - np.array([dst_rect[0], dst_rect[1]], dtype=np.float32)
            warp = cv2.getAffineTransform(src_tri_rect.astype(np.float32), dst_tri_rect.astype(np.float32))
            warped = cv2.warpAffine(
                src_patch,
                warp,
                (dst_rect[2], dst_rect[3]),
                flags=cv2.INTER_LINEAR,
                borderMode=cv2.BORDER_REFLECT_101,
            )

            tri_mask = np.zeros((dst_rect[3], dst_rect[2]), dtype=np.uint8)
            cv2.fillConvexPoly(tri_mask, np.round(dst_tri_rect).astype(np.int32), 255)
            valid = (tri_mask > 0) & (warped[..., 3] > 0)
            if not np.any(valid):
                continue

            y0, x0 = dst_rect[1], dst_rect[0]
            texture_slice = texture[y0 : y0 + dst_rect[3], x0 : x0 + dst_rect[2]]
            coverage_slice = coverage[y0 : y0 + dst_rect[3], x0 : x0 + dst_rect[2]]
            texture_slice[valid] = warped[..., :3][valid]
            coverage_slice[valid] = 255

        if np.any(coverage == 0) and np.any(coverage > 0):
            texture = cv2.inpaint(texture, (coverage == 0).astype(np.uint8) * 255, 3, cv2.INPAINT_TELEA)

        texture_tensor = torch.tensor(texture / 255.0, dtype=torch.float32, device=self.render.device)
        mask_tensor = torch.tensor((coverage > 0)[..., None], dtype=torch.bool, device=self.render.device)
        return texture_tensor, mask_tensor

    @torch.no_grad()
    def __call__(self, mesh_path=None, image_path=None, output_mesh_path=None, use_remesh=True, save_glb=True):
        """Generate texture for 3D mesh using multiview diffusion"""
        # Ensure image_prompt is a list. The web app passes multiple PIL images.
        if isinstance(image_path, str):
            image_prompt = Image.open(image_path)
        elif isinstance(image_path, Image.Image):
            image_prompt = image_path
        elif isinstance(image_path, List):
            image_prompt = image_path
        else:
            raise TypeError("image_path must be a path, PIL image, or list of PIL images.")

        if not isinstance(image_prompt, List):
            image_prompt = [image_prompt]

        # Process mesh
        path = os.path.dirname(mesh_path)
        if use_remesh:
            processed_mesh_path = os.path.join(path, "white_mesh_remesh.obj")
            remesh_mesh(mesh_path, processed_mesh_path)
        else:
            processed_mesh_path = mesh_path

        # Output path
        if output_mesh_path is None:
            output_mesh_path = os.path.join(path, f"textured_mesh.obj")

        # Load mesh
        mesh = trimesh.load(processed_mesh_path)
        mesh = mesh_uv_wrap(mesh)
        self.render.load_mesh(mesh=mesh)

        ########### View Selection #########
        selected_camera_elevs, selected_camera_azims, selected_view_weights = self.view_processor.bake_view_selection(
            self.config.candidate_camera_elevs,
            self.config.candidate_camera_azims,
            self.config.candidate_view_weights,
            self.config.max_selected_view_num,
        )

        normal_maps = self.view_processor.render_normal_multiview(
            selected_camera_elevs, selected_camera_azims, use_abs_coor=True
        )
        position_maps = self.view_processor.render_position_multiview(selected_camera_elevs, selected_camera_azims)

        ##########  Style  ###########
        image_caption = "high quality"
        image_style = []
        for image in image_prompt:
            image = image.resize((512, 512))
            if image.mode == "RGBA":
                white_bg = Image.new("RGB", image.size, (255, 255, 255))
                white_bg.paste(image, mask=image.getchannel("A"))
                image = white_bg
            image_style.append(image)
        image_style = [image.convert("RGB") for image in image_style]

        ###########  Multiview  ##########
        self.load_models()
        multiviews_pbr = self.models["multiview_model"](
            image_style,
            normal_maps + position_maps,
            prompt=image_caption,
            custom_view_size=self.config.resolution,
            resize_input=True,
        )
        ###########  Enhance  ##########
        enhance_images = {}
        enhance_images["albedo"] = copy.deepcopy(multiviews_pbr["albedo"])
        enhance_images["mr"] = copy.deepcopy(multiviews_pbr["mr"])

        for i in range(len(enhance_images["albedo"])):
            enhance_images["albedo"][i] = self.models["super_model"](enhance_images["albedo"][i])
            enhance_images["mr"][i] = self.models["super_model"](enhance_images["mr"][i])

        ###########  Bake  ##########
        for i in range(len(enhance_images)):
            enhance_images["albedo"][i] = enhance_images["albedo"][i].resize(
                (self.config.render_size, self.config.render_size)
            )
            enhance_images["mr"][i] = enhance_images["mr"][i].resize((self.config.render_size, self.config.render_size))
        texture, mask = self.view_processor.bake_from_multiview(
            enhance_images["albedo"], selected_camera_elevs, selected_camera_azims, selected_view_weights
        )
        if torch.count_nonzero(mask) == 0:
            texture, mask = self._fallback_project_texture(image_style, selected_camera_elevs, selected_camera_azims)
        if torch.count_nonzero(mask) == 0 or float(texture.max().item()) < 1e-4:
            texture, mask = self._planar_projection_texture(mesh, image_prompt)
        mask_np = (mask.squeeze(-1).cpu().numpy() * 255).astype(np.uint8)
        texture_mr, mask_mr = self.view_processor.bake_from_multiview(
            enhance_images["mr"], selected_camera_elevs, selected_camera_azims, selected_view_weights
        )
        mask_mr_np = (mask_mr.squeeze(-1).cpu().numpy() * 255).astype(np.uint8)

        ##########  inpaint  ###########
        texture = self.view_processor.texture_inpaint(texture, mask_np)
        self.render.set_texture(texture, force_set=True)
        if "mr" in enhance_images:
            texture_mr = self.view_processor.texture_inpaint(texture_mr, mask_mr_np)
            self.render.set_texture_mr(texture_mr)

        self.render.save_mesh(output_mesh_path, downsample=True)

        if save_glb:
            output_glb_path = output_mesh_path.replace(".obj", ".glb")
            if not convert_obj_to_glb(output_mesh_path, output_glb_path):
                self._export_glb_without_blender(output_mesh_path, output_glb_path)

        return output_mesh_path

    def _export_glb_without_blender(self, obj_path, glb_path):
        """Fallback for Windows environments where the bpy module cannot load."""
        obj_path = Path(obj_path)
        glb_path = Path(glb_path)
        loaded = trimesh.load(obj_path, force="scene")
        loaded.export(glb_path)
        if not glb_path.exists():
            raise RuntimeError("Blender and trimesh GLB export both failed.")

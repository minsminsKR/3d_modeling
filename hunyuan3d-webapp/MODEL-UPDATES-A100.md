# Model review - deployment 2026.10.11.1

Changes: CHG-20261010-A100-002 / CHG-20261010-A100-003.
Target: ai-server-04@192.168.0.16, two A100-SXM4-40GB cards.

## Applied

Hunyuan3D 2.1 and Omni source heads matched their official repositories during the 2026-10-10 review. Existing compatibility patches are retained. Omni uses the official EMA inference variant. Hunyuan PBR supports 6-view 512/768 generation with genuine 4K/8K exported maps; split mode uses shape GPU 0 and PBR GPU 1, and independent standard jobs can use both cards.

Official TRELLIS.2-4B is now deployed in a separate Torch 2.6.0 environment. Both required DINOv3 and RMBG-2.0 model approvals and downloads completed. Real native tests passed for 512 on both GPUs concurrently, 1024/4K and 1536/8K. Browser upload, generation, GLB/PBR download and actual 3D rendering also passed. Exact source/model revisions and validation paths are in deployment-a100.json and logs/trellis-a100-models.json.

GPU 0 retains its RTSP allocation. Native high-resolution jobs use GPU 1; optional GPU 0 high-resolution testing and preparation were rejected by automatic approval review and were not executed. Each job must fit its physical card; there is no 80GB pooled inference mode. Model/profile choices affect quality, and a larger exported texture map alone does not create additional detail. Results vary by input.

Hunyuan3D-Buffalo 1.0 was reviewed on 2026-10-10: the official code link was labelled Stay Tuned and no runnable release was verified for deployment.

## Sources

- https://github.com/Tencent-Hunyuan/Hunyuan3D-2.1
- https://github.com/Tencent-Hunyuan/Hunyuan3D-Omni#inference
- https://github.com/microsoft/TRELLIS.2
- https://huggingface.co/facebook/dinov3-vitl16-pretrain-lvd1689m
- https://huggingface.co/briaai/RMBG-2.0
- https://tencent-hunyuan.github.io/Hunyuan3D-Buffalo1.0/

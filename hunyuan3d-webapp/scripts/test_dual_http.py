"""Non-GPU HTTP contract smoke for experimental dual postprocess."""
import io
import json
import os
from pathlib import Path
import tempfile
import time

root = Path(os.environ.get("DUAL_HTTP_TEMP_ROOT", Path(__file__).resolve().parents[1] / "instance"))
root.mkdir(parents=True, exist_ok=True)
with tempfile.TemporaryDirectory(prefix="dual-http-", dir=root) as folder:
    directory = Path(folder)
    os.environ["APP_INSTANCE_DIR"] = folder
    output = directory / "outputs" / "review"
    output.mkdir(parents=True)
    model = output / "model.glb"
    model.write_bytes(b"glTF")
    options = {"model": "trellis2", "gpu_id": 1, "texture_gpu_id": 0, "gpu_mode": "split",
               "trellis_pipeline": "1536_cascade", "trellis_texture_size": 8192,
               "trellis_dual_postprocess": True, "texture": True}
    (directory / "jobs.json").write_text(json.dumps({"review": {
        "id": "review", "status": "review_required", "message": "검토 필요",
        "created_at": time.time(), "finished_at": time.time(),
        "options": options, "model_path": str(model), "used_texture": True,
        "warnings": ["시각 품질 검토 필요"]}}))
    import dotenv
    dotenv.load_dotenv = lambda *args, **kwargs: None
    import app as module
    module.trellis_ready = lambda: True
    module.trellis_dual_ready = lambda: True
    module.available_gpu_ids = lambda: [0, 1]
    client = module.app.test_client()
    index = client.get("/")
    assert index.status_code == 200, index.status_code
    assert b"trellis_dual_postprocess" in index.data
    review = client.get("/jobs/review")
    assert review.status_code == 200, review.status_code
    assert b"review_required" in review.data
    assert client.get("/jobs/review/model").status_code == 200
    assert client.get("/jobs/review/download").status_code == 200
    assert client.post("/api/jobs/review/saved", json={"saved": True}).status_code == 400
    for field in (
        {"model": "hunyuan3d-2.1", "gpu_id": "split", "trellis_dual_postprocess": "on"},
        {"model": "trellis2", "gpu_id": "split", "trellis_pipeline": "1024_cascade",
         "trellis_texture_size": "8192", "trellis_dual_postprocess": "on"},
        {"model": "trellis2", "gpu_id": "1", "trellis_pipeline": "1536_cascade",
         "trellis_texture_size": "8192", "trellis_dual_postprocess": "on"},
    ):
        response = client.post("/api/jobs", data={**field, "images": (io.BytesIO(b"test"), "test.png")},
                               content_type="multipart/form-data")
        assert response.status_code == 400, (field, response.status_code, response.data[:300])
    assert len(json.loads((directory / "jobs.json").read_text())) == 1
    from hunyuan_service import GeneratedAsset
    class InlineExecutor:
        def __init__(self, *args, **kwargs):
            pass
        def submit(self, fn, *args, **kwargs):
            fn(*args, **kwargs)
    module.ThreadPoolExecutor = InlineExecutor
    def fake_generation(images, output_dir, options, progress):
        assert options.trellis_dual_postprocess
        assert options.trellis_max_tokens == 131072
        assert options.gpu_mode == "split"
        output_dir.mkdir(parents=True, exist_ok=True)
        generated = output_dir / "model.glb"
        generated.write_bytes(b"glTF")
        metadata = output_dir / "metadata.json"
        metadata.write_text("{}")
        return GeneratedAsset(generated, metadata, True, ("시각 검토 필요",), True)
    module.run_isolated_generation = fake_generation
    second = module.create_app().test_client()
    valid = {"model": "trellis2", "gpu_id": "split", "trellis_pipeline": "1536_cascade",
             "trellis_texture_size": "8192", "trellis_dual_postprocess": "on"}
    response = second.post("/api/jobs", data={**valid, "images": (io.BytesIO(b"test"), "test.png")},
                           headers={"Idempotency-Key": "dual-http-smoke-unique"},
                           content_type="multipart/form-data")
    assert response.status_code == 201, (response.status_code, response.data[:300])
    created = response.json["jobs"][0]
    assert created["status"] == "review_required", created
    assert created["options"]["trellis_max_tokens"] == 131072
    replay = second.post("/api/jobs", data={**valid, "images": (io.BytesIO(b"test"), "test.png")},
                         headers={"Idempotency-Key": "dual-http-smoke-unique"},
                         content_type="multipart/form-data")
    assert replay.status_code == 200 and replay.json["replayed"], replay.status_code
    assert len(json.loads((directory / "jobs.json").read_text())) == 2
    print("HTTP smoke passed: form, review preview/download, save rejection, input validation, opt-in job, idempotent replay")

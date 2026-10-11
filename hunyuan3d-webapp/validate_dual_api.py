"""Real multipart/counter/default contracts with isolated jobs; no inference."""
from __future__ import annotations
import importlib.util
import io
import json
import sys
import time
import uuid
import shutil
from pathlib import Path
from PIL import Image

BASE = Path(__file__).resolve().parent
sys.path.insert(0, str(BASE))
source = BASE / "app.py"
spec = importlib.util.spec_from_file_location("dual_api_validation", source)
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
exec(compile(source.read_text().replace("app = create_app()", "app = None"), str(source), "exec"), module.__dict__)
directory = BASE / "instance/DATA-20261011-A100-009-API" / str(uuid.uuid4())
module.INSTANCE_DIR = directory
module.UPLOAD_DIR = directory / "uploads"
module.OUTPUT_DIR = directory / "outputs"
module.JOBS_FILE = directory / "jobs.json"
submitted = []


class CaptureExecutor:
    def __init__(self, **kwargs):
        pass
    def submit(self, *args):
        submitted.append(args)


module.ThreadPoolExecutor = CaptureExecutor
module.available_gpu_ids = lambda: [0, 1]
module.trellis_ready = lambda: True
module.trellis_dual_ready = lambda: True


def fixture_asset(image_paths, output, options, progress):
    from hunyuan_service import GeneratedAsset
    output.mkdir(parents=True, exist_ok=True)
    model = output / "model.glb"
    model.write_bytes(b"glTF-isolated-API-fixture")
    metadata = output / "metadata.json"
    metadata.write_text(json.dumps({"options": options.__dict__}))
    return GeneratedAsset(model, metadata, True, ())


module.run_isolated_generation = fixture_asset
app = module.create_app()
fixture_templates = directory / "templates"
shutil.copytree(BASE / "templates", fixture_templates)
app.template_folder = str(fixture_templates)
client = app.test_client()
stream = io.BytesIO()
Image.new("RGBA", (4, 4), (20, 180, 100, 0)).save(stream, format="PNG")
png = stream.getvalue()
cases = []


def check(name, condition):
    assert condition, name
    cases.append(name)


def upload(mode, pipeline="1536_cascade", count=1):
    return client.post("/jobs", data={"model": "trellis2", "gpu_id": mode, "trellis_pipeline": pipeline,
                                      "images": [(io.BytesIO(png), f"캐릭터{index}.PNG") for index in range(count)]}, content_type="multipart/form-data")


check("default-two-gpu-mode", module.default_gpu_mode_for_model("trellis2") == "split")
page = client.get("/")
check("native-default-jinja", page.status_code == 200 and 'data-default-gpu-mode="split"' in page.text)
response = upload("split")
check("split-upload-accepted", response.status_code == 302)
function, job_id, image_paths, options = submitted[-1]
check("physical-placement", options.gpu_id == 1 and options.texture_gpu_id == 0 and options.gpu_mode == "split")
check("split-higher-token-budget", options.trellis_max_tokens == 65536)
health = client.get("/health").json
check("both-gpu-counters-queued", health["gpu_queue_load"] == {"0": 1, "1": 1})
function(job_id, image_paths, options)
check("both-gpu-counters-released", client.get("/health").json["gpu_queue_load"] == {"0": 0, "1": 0})
check("completed-job-kept", client.get("/api/jobs/" + job_id).json["status"] == "completed")
check("split-result-label", "형상 1 / 재질 0" in client.get("/jobs/" + job_id).text)
module.trellis_dual_ready = lambda: False
check("unready-split-rejected", upload("split").status_code == 503)
module.trellis_dual_ready = lambda: True
response = upload("1")
function, job_id, image_paths, options = submitted[-1]
check("single-retained", response.status_code == 302 and options.gpu_id == 1 and options.texture_gpu_id is None)
check("single-token-budget-retained", options.trellis_max_tokens == 49152)
function(job_id, image_paths, options)
check("high-single-gpu0-protected", upload("0").status_code == 400)
start = len(submitted)
response = upload("auto", "512", 2)
new = submitted[start:]
check("batch-distributed", response.status_code == 302 and {entry[3].gpu_id for entry in new} == {0, 1} and all(entry[3].texture_gpu_id is None for entry in new))
for function, job_id, image_paths, options in new:
    function(job_id, image_paths, options)
check("batch-counters-released", client.get("/health").json["gpu_queue_load"] == {"0": 0, "1": 0})
report = {"change_id": "CHG-20261011-A100-009", "data_id": "DATA-20261011-A100-009-API", "passed": True,
          "case_count": len(cases), "cases": cases, "gpu_inference": False, "data_directory": str(directory)}
(BASE / "logs/trellis-dual-api-validation.json").write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))

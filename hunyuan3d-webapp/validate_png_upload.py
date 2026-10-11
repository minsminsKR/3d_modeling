"""Real multipart/job-storage regression tests in an isolated DATA store."""
from __future__ import annotations
import importlib.util
import io
import json
import sys
import uuid
from pathlib import Path
from urllib.parse import quote
from PIL import Image

BASE = Path(__file__).resolve().parent
sys.path.insert(0, str(BASE))
spec = importlib.util.spec_from_file_location("png_upload_validation", BASE / "app.py")
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
exec(compile((BASE / "app.py").read_text().replace("app = create_app()", "app = None"), str(BASE / "app.py"), "exec"), module.__dict__)
directory = BASE / "instance/DATA-20261011-A100-007-PNG" / str(uuid.uuid4())
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
app = module.create_app()
client = app.test_client()
image = Image.new("RGBA", (8, 8), (40, 150, 100, 0))
image.putpixel((4, 4), (200, 100, 40, 255))
buffer = io.BytesIO()
image.save(buffer, format="PNG")
png = buffer.getvalue()
cases = []
names = ["캐릭터.png", "이미지.PNG", "character.png", "👾.png", "日本語.PNG", "../../outside.png", r"C:\fakepath\한글.PNG", ".png", "a" * 250 + ".png"]
for name in names:
    if "\\" in name:
        # The test client's quoted filename encoder consumes backslashes.
        # RFC 5987 carries the actual Windows path unchanged to the parser.
        boundary = "png-test-boundary"
        body = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"images\"; filename*=UTF-8''{quote(name, safe='')}\r\nContent-Type: image/png\r\n\r\n".encode() + png + f"\r\n--{boundary}--\r\n".encode())
        response = client.post("/jobs", data=body, content_type=f"multipart/form-data; boundary={boundary}")
    else:
        response = client.post("/jobs", data={"images": (io.BytesIO(png), name, "image/png"), "model": "trellis2", "gpu_id": "auto"}, content_type="multipart/form-data")
    assert response.status_code == 302, (name, response.status_code, response.data)
    job_id = response.headers["Location"].split("/")[-1]
    job = json.loads(module.JOBS_FILE.read_text())[job_id]
    stored = Path(job["image_paths"][0])
    assert stored.resolve().is_relative_to(module.UPLOAD_DIR.resolve())
    assert stored.suffix == ".png" and stored.read_bytes() == png and len(stored.name) <= 164
    assert job["source_filename"] == name.replace("\\", "/").rsplit("/", 1)[-1], (name, job["source_filename"])
    with Image.open(stored) as decoded:
        assert decoded.mode == "RGBA" and decoded.getpixel((0, 0))[3] == 0 and decoded.getpixel((4, 4))[3] == 255
    cases.append({"filename": name, "http": response.status_code, "stored_name": stored.name, "rgba_preserved": True})
for name in ("캐릭터.exe", "no-extension", "bad.png.exe"):
    response = client.post("/jobs", data={"images": (io.BytesIO(png), name, "image/png")}, content_type="multipart/form-data")
    assert response.status_code == 400
    cases.append({"filename": name, "http": 400})
assert len(submitted) == len(names)
report = {"change_id": "CHG-20261011-A100-007", "data_id": "DATA-20261011-A100-007-PNG", "passed": True, "cases": cases, "case_count": len(cases), "gpu_inference": False, "directory": str(directory)}
(BASE / "logs/png-upload-validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2))
print(json.dumps(report, ensure_ascii=False, indent=2))

"""Pure metadata for supported submissions and read-only diagnostic reports."""
from __future__ import annotations

from copy import deepcopy
import math

from modeling_contract import ModelingError

PARAMETERS = {
    "model": {"type": "string", "enum": ["hunyuan3d-2.1", "hunyuan3d-omni", "trellis2"]},
    "gpu_id": {"type": "string", "description": "Physical GPU index, auto or split; inspect model constraints."},
    "seed": {"type": "integer", "minimum": 0, "maximum": 2**32 - 1},
    "remove_background": {"type": "string", "enum": ["on", "off"]},
    "texture": {"type": "string", "enum": ["on", "off"]},
    "trellis_pipeline": {"type": "string", "enum": ["512", "1024_cascade", "1536_cascade"]},
    "trellis_steps": {"type": "integer", "minimum": 1, "maximum": 50},
    "trellis_texture_size": {"type": "integer", "enum": [4096, 8192], "minimum": 4096, "maximum": 8192},
    "num_inference_steps": {"type": "integer", "minimum": 1, "maximum": 100},
    "guidance_scale": {"type": "number", "minimum": 0.1, "maximum": 20},
    "octree_resolution": {"type": "integer", "minimum": 64, "maximum": 512},
    "num_chunks": {"type": "integer", "minimum": 1000, "maximum": 300000},
    "texture_profile": {"type": "string", "enum": ["standard", "detail", "ultra"]},
}


def validate_submission_fields(form):
    """API errors must not silently replace bad fields with a server default."""
    for name in form:
        schema = PARAMETERS.get(name)
        if schema is None:
            raise ModelingError("Unsupported generation field: " + name)
        values = form.getlist(name)
        if len(values) != 1:
            raise ModelingError("Supply each generation field once: " + name)
        raw = values[0]
        value = raw
        try:
            if schema["type"] == "integer":
                value = int(raw)
            elif schema["type"] == "number":
                value = float(raw)
                if not math.isfinite(value):
                    raise ValueError()
        except (ValueError, TypeError):
            raise ModelingError("Invalid numeric value for " + name) from None
        if "enum" in schema and value not in schema["enum"]:
            raise ModelingError("Unsupported value for " + name)
        if "minimum" in schema and not schema["minimum"] <= value <= schema["maximum"]:
            raise ModelingError("Value is outside the supported range for " + name)
        if name == "gpu_id" and value not in {"auto", "split"} and not str(value).isdecimal():
            raise ModelingError("gpu_id must be auto, split or a nonnegative physical GPU index.")


def build_capabilities(facts):
    defaults = facts["defaults"]
    gpu_ids = facts["gpu_ids"]
    parameters = deepcopy(PARAMETERS)
    parameters["gpu_id"]["enum"] = (["auto"] + (["split"] if len(gpu_ids) >= 2 else []) + [str(index) for index in gpu_ids])
    models = {}
    common = {"seed": defaults["seed"], "remove_background": "on" if defaults["remove_background"] else "off",
              "num_inference_steps": 50, "guidance_scale": 7.5, "octree_resolution": 512,
              "num_chunks": 50000, "texture_profile": facts["texture_profile"]}
    for name, readiness in facts["model_readiness"].items():
        submitted = {**common, "model": name, "gpu_id": facts["gpu_modes"][name],
                     "texture": "on" if name == "trellis2" else "off"}
        if name == "trellis2":
            submitted.update({key: value for key, value in facts["trellis_defaults"].items() if key != "trellis_max_tokens"})
        fields = set(PARAMETERS) - ({"trellis_pipeline", "trellis_steps", "trellis_texture_size"}
                                  if name != "trellis2" else
                                  {"num_inference_steps", "guidance_scale", "octree_resolution", "num_chunks", "texture_profile"})
        model_parameters = {key: deepcopy(parameters[key]) for key in sorted(fields)}
        if name == "trellis2" and not facts["trellis_dual_ready"]:
            model_parameters["gpu_id"]["enum"] = [value for value in parameters["gpu_id"]["enum"] if value != "split"]
        for key, schema in model_parameters.items():
            if key in submitted:
                schema["default"] = submitted[key]
        mode = submitted["gpu_id"]
        required = []
        placement_reasons = []
        if mode == "split":
            if name == "trellis2":
                required = [facts["texture_gpu"]]
                if len(gpu_ids) < 2 or not facts["trellis_dual_ready"]:
                    placement_reasons.append("verified_two_gpu_configuration_required")
            else:
                required = [facts["shape_gpu"], facts["texture_gpu"]]
        elif mode == "auto":
            required = [facts["default_gpu"]] if facts["default_gpu"] is not None else []
        elif str(mode).isdecimal():
            required = [int(mode)]
            if name == "trellis2" and submitted["trellis_pipeline"] != "512" and int(mode) != facts["texture_gpu"]:
                placement_reasons.append("high_resolution_manual_gpu_required")
        else:
            placement_reasons.append("invalid_default_gpu_mode")
        if not gpu_ids or any(index not in gpu_ids for index in required):
            placement_reasons.append("required_physical_gpu_unavailable")
        default_configuration = {"valid": not placement_reasons, "gpu_mode": mode,
                                 "required_gpu_ids": required, "unavailable_reasons": placement_reasons}
        models[name] = {**readiness, "defaults": {key: value for key, value in submitted.items() if key in fields},
                        "default_configuration": default_configuration,
                        "web_defaults": {"texture": "on" if name == "trellis2" or facts["texture_ready"] else "off"},
                        "parameters": model_parameters,
                        "readiness_scope": "Configuration, installed files and CUDA only; no inference is run by this query.",
                        "runtime_loaded": None, "runtime_loaded_by_check": False, "inference_checked": False,
                        "constraints": (["Texture is always enabled for TRELLIS.2.",
                                         f"1024/1536 manual placement uses physical GPU {facts['texture_gpu']}.",
                                         "Split mode requires the verified two-GPU configuration."]
                                        if name == "trellis2" else
                                        [f"High-quality texture generation uses GPU {facts['texture_gpu']} or split mode.",
                                         "An omitted API texture field means off; the web form explicitly sends its checkbox value."])}
    return {"schema_version": 1, "api_version": "1.1", "service": "hunyuan3d-webapp",
            "ok": True, "models": models, "parameters": parameters, "defaults": defaults,
            "gpu_ids": gpu_ids, "limits": facts["limits"],
            "transport": {"submission": "multipart/form-data", "images_field": "images",
                          "boolean_values": {"true": "on", "false": "off"}},
            "features": {"idempotent_submissions": True, "request_history": True,
                         "per_image_batch": True, "provenance_archives": True,
                         "versioned_archive_cache": True, "archive_http_range": True,
                         "resumable_downloads": "client",
                         "partial_collection": "client", "structured_errors": True,
                         "rigging": facts["rigging_ready"], "visual_joint_editor": "web"},
            "endpoints": {"capabilities": "/api/capabilities", "doctor": "/api/doctor",
                          "create": "/api/jobs", "history": "/api/jobs",
                          "status": "/api/jobs/{job_id}", "record": "/api/jobs/{job_id}/record",
                          "archive": "/api/jobs/{job_id}/archive", "web": "/jobs/{job_id}"}}


def build_doctor(capabilities, facts, storage, queue, job_count, rigging_tasks):
    default_name = capabilities["defaults"]["model"]
    default_available = (capabilities["models"][default_name]["available"] and
                         capabilities["models"][default_name]["default_configuration"]["valid"])
    checks = {
        "authentication": {"ok": True, "message": "The current access path authorized this API request."},
        "cuda": {"ok": bool(facts["gpu_ids"]), "message": "Physical CUDA devices detected." if facts["gpu_ids"] else "No usable CUDA devices detected."},
        "storage": {"ok": storage["writable"] and storage["free_bytes"] > 0,
                    "message": "Job storage must be writable and have free space.", **storage},
        "default_model": {"ok": default_available, "model": default_name,
                          "message": "Default-model configuration is ready." if default_available else "Inspect this model's unavailable_reasons."},
        "workers": {"ok": facts["workers"] > 0, "count": facts["workers"]},
    }
    ready = all(check["ok"] for check in checks.values())
    return {"schema_version": 1, "api_version": "1.1", "ok": ready,
            "status": "ready" if ready else "degraded", "checks": checks,
            "runtime": {"gpu_stats": facts["gpu_stats"], "gpu_queue_load": queue,
                        "generation_workers": facts["workers"], "jobs": job_count,
                        "rigging_tasks": rigging_tasks}, "capabilities": capabilities,
            "inference_checked": False, "models_loaded_by_check": False}

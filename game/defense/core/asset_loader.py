from __future__ import annotations
from ursina import Entity

def safe_entity(model=None, fallback_model="cube", **kwargs) -> Entity:
    """Create an Entity, retrying with a primitive when external assets fail."""
    try:
        if model:
            model = str(model)
        if "texture" in kwargs and kwargs["texture"]:
            kwargs["texture"] = str(kwargs["texture"])
        return Entity(model=model or fallback_model, **kwargs)
    except Exception:
        kwargs.pop("texture", None)
        return Entity(model=fallback_model, **kwargs)

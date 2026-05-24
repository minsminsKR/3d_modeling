from __future__ import annotations
from collections import defaultdict
from typing import Callable, DefaultDict


class EventBus:
    def __init__(self):
        self._listeners: DefaultDict[str, list[Callable]] = defaultdict(list)

    def on(self, event_name: str, callback: Callable):
        self._listeners[event_name].append(callback)

    def emit(self, event_name: str, **payload):
        for callback in list(self._listeners.get(event_name, [])):
            callback(**payload)

    def clear(self):
        self._listeners.clear()

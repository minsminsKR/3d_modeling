"""In-memory history indexes; callers hold the application's jobs lock.

Only immutable submission metadata belongs in this index. Status and rig edits
remain in the shared job store, so paged results always show their latest values.
"""
from __future__ import annotations

from bisect import insort


class JobHistory:
    def __init__(self, jobs):
        self._indexes = {}
        self._keys = {}
        for job in jobs.values():
            self._add(job, ordered=False)
        for entries in self._indexes.values():
            entries.sort()

    @staticmethod
    def _groups(job):
        source = job.get("source", "web")
        request_id = job.get("request_id")
        groups = ((None, None), (source, None)) + (
            ((None, request_id), (source, request_id)) if request_id is not None else ()
        )
        return tuple(dict.fromkeys(groups))

    def _add(self, job, *, ordered):
        job_id = job["id"]
        key = (job.get("created_at", 0), job_id)
        groups = self._groups(job)
        previous = self._keys.get(job_id)
        if previous == (key, groups):
            return
        if previous:
            old_key, old_groups = previous
            for group in old_groups:
                self._indexes[group].remove(old_key)
        self._keys[job_id] = (key, groups)
        for group in groups:
            entries = self._indexes.setdefault(group, [])
            if ordered:
                insort(entries, key)
            else:
                entries.append(key)

    def register(self, jobs):
        for job in jobs:
            self._add(job, ordered=True)

    def page(self, jobs, *, source=None, request_id=None, limit=50, offset=0):
        entries = self._indexes.get((source, request_id), ())
        total = len(entries)
        end = max(0, total - offset)
        start = max(0, end - limit)
        return [dict(jobs[job_id]) for _, job_id in reversed(entries[start:end])], total

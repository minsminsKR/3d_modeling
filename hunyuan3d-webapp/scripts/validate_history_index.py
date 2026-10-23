"""Verify indexed history equivalence without GPU inference."""
from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from modeling_history import JobHistory


def baseline(jobs, source=None, request_id=None, limit=50, offset=0):
    selected = [dict(job) for job in jobs.values()
                if (source is None or job.get("source", "web") == source)
                and (request_id is None or job.get("request_id") == request_id)]
    selected.sort(key=lambda job: (job.get("created_at", 0), job["id"]), reverse=True)
    return selected[offset:offset + limit], len(selected)


def fixture(count=501):
    jobs = {}
    for index in range(count):
        job = {"id": f"job-{index:06d}", "created_at": index // 3, "status": "queued"}
        if index % 3:
            job["source"] = "cli" if index % 3 == 1 else "api"
            job["request_id"] = f"request-{index // 7}"
        jobs[job["id"]] = job
    return jobs


class HistoryTests(unittest.TestCase):
    def test_all_filters_pages_ties_and_missing_requests(self):
        jobs = fixture()
        history = JobHistory(jobs)
        for source in (None, "web", "cli", "api"):
            for request_id in (None, "request-1", "request-30", "absent", ""):
                for offset in (0, 1, 49, 500, 999):
                    for limit in (1, 50, 200):
                        options = dict(source=source, request_id=request_id, offset=offset, limit=limit)
                        self.assertEqual(history.page(jobs, **options), baseline(jobs, **options))

    def test_batch_registration_and_replay_are_idempotent(self):
        jobs = fixture(10)
        history = JobHistory(jobs)
        created = list(fixture(25).values())[10:]
        jobs.update((job["id"], job) for job in created)
        history.register(created)
        history.register(created)
        self.assertEqual(history.page(jobs), baseline(jobs))

    def test_live_status_updates_and_snapshot_isolation(self):
        jobs = fixture(2)
        history = JobHistory(jobs)
        jobs["job-000001"]["status"] = "completed"
        page, _ = history.page(jobs)
        self.assertEqual(page[0]["status"], "completed")
        page[0]["status"] = "failed"
        self.assertEqual(jobs["job-000001"]["status"], "completed")

    def test_rebuild_and_metadata_replacement(self):
        jobs = fixture()
        history = JobHistory(jobs)
        changed = jobs["job-000001"]
        changed.update(source="web", request_id="replacement", created_at=999)
        history.register([changed])
        rebuilt = JobHistory(jobs)
        for source in (None, "web", "cli"):
            for request_id in (None, "request-0", "replacement"):
                options = dict(source=source, request_id=request_id)
                self.assertEqual(history.page(jobs, **options), baseline(jobs, **options))
                self.assertEqual(rebuilt.page(jobs, **options), baseline(jobs, **options))


if __name__ == "__main__":
    unittest.main()

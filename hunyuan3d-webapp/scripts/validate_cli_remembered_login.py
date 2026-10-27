#!/usr/bin/env python3
"""Offline tests for remembered CLI authentication; no real credentials."""
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import requests
import os
import uuid
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from modeling_cli import Client
from modeling_state import State

class RememberedLoginTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.client = Client(State(Path(self.tmp.name), 'https://example.invalid'), 3)

    def response(self, status, body):
        r = requests.Response()
        r.status_code = status
        r._content = body
        r.url = 'https://example.invalid/api/jobs'
        return r

    def test_expired_session_reauthenticates_once(self):
        denied = self.response(401, b'{"error":"authentication_required"}')
        ok = self.response(200, b'{"jobs":[]}')
        with patch.object(self.client, 'saved_password', return_value='fixture'), patch.object(self.client, 'login') as login, patch.object(self.client.session, 'request', side_effect=[denied, ok]) as request:
            response = self.client.request('GET', '/api/jobs')
        self.assertEqual(response.status_code, 200)
        self.assertEqual(request.call_count, 2)
        login.assert_called_once_with(password_value='fixture')

    def test_repeated_401_stops_without_loop(self):
        denied = self.response(401, b'{"error":"authentication_required"}')
        with patch.object(self.client, 'saved_password', return_value='fixture'), patch.object(self.client, 'login') as login, patch.object(self.client.session, 'request', side_effect=[denied, denied]) as request:
            response = self.client.request('GET', '/api/jobs')
        self.assertEqual(response.status_code, 401)
        self.assertEqual(request.call_count, 2)
        self.assertEqual(login.call_count, 1)

    def test_mutation_preflight_and_no_replay(self):
        denied = self.response(401, b'{"error":"authentication_required"}')
        with patch.object(self.client, 'saved_password', return_value='fixture'), patch.object(self.client, 'json') as preflight, patch.object(self.client.session, 'request', return_value=denied) as request:
            response = self.client.request('POST', '/api/jobs', data=b'fixture')
        preflight.assert_called_once_with('GET', '/api/jobs?limit=1')
        self.assertEqual(request.call_count, 1)
        self.assertEqual(response.status_code, 401)

    def test_credential_key_is_bound_to_origin(self):
        second = Client(State(Path(self.tmp.name), 'https://other.invalid'), 3)
        self.assertNotEqual(self.client.credential_target(), second.credential_target())

    @unittest.skipUnless(os.name == 'nt', 'Windows Credential Manager')
    def test_windows_credential_roundtrip(self):
        import win32cred
        self.client.server = 'https://' + uuid.uuid4().hex + '.invalid'
        self.assertIsNone(self.client.saved_password())
        self.client.remember_password('fixture-한글')
        try:
            self.assertEqual(self.client.saved_password(), 'fixture-한글')
        finally:
            win32cred.CredDelete(self.client.credential_target(), win32cred.CRED_TYPE_GENERIC, 0)

if __name__ == '__main__':
    unittest.main()

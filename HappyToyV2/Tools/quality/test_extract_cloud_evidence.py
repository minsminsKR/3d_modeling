import base64
import hashlib
import unittest
import tempfile
from unittest.mock import patch
from pathlib import Path
from extract_cloud_evidence import decode, read_input


def envelope(data=b'{"real":true}', name='sample.json'):
    payload = base64.b64encode(data).decode()
    chunks = [payload[i:i+4096] for i in range(0, len(payload), 4096)]
    return '\n'.join([
        f'HAPPYTOY_ARTIFACT_BEGIN {name} {len(data)} {hashlib.sha256(data).hexdigest()} {len(chunks)}',
        *[f'HAPPYTOY_ARTIFACT_CHUNK {name} {i} {chunk}' for i, chunk in enumerate(chunks)],
        f'HAPPYTOY_ARTIFACT_END {name}',
    ])


class CloudEvidenceTests(unittest.TestCase):
    def test_camera_audio_and_aim_batch_at_file_cap_round_trips(self):
        text = '\n'.join(envelope(name=f'frame-{i}.json') for i in range(39))
        self.assertEqual(len(decode(text)), 39)

    def test_camera_audio_and_aim_batch_over_file_cap_fails(self):
        text = '\n'.join(envelope(name=f'frame-{i}.json') for i in range(40))
        with self.assertRaisesRegex(ValueError, 'Too many artifacts'): decode(text)

    def test_aggregate_byte_cap_is_enforced(self):
        with patch('extract_cloud_evidence.MAX_TOTAL', 20):
            with self.assertRaisesRegex(ValueError, 'Total artifacts'):
                decode(envelope(name='one.json') + '\n' + envelope(name='two.json'))

    def test_reserved_manifest_fails(self):
        with self.assertRaises(ValueError): decode(envelope(name='transport-manifest.json'))

    def test_multiple_chunks_round_trip(self):
        data = b'"' + b'A' * 20000 + b'"'
        self.assertEqual(decode(envelope(data)), {'sample.json': data})

    def test_nunit_output_cdata(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'results.xml'
            path.write_text('<test-run><test-case><output><![CDATA[' + envelope() + ']]></output></test-case></test-run>')
            original, text = read_input(path)
            self.assertTrue(original.startswith(b'<test-run>'))
            self.assertEqual(decode(text), decode(envelope()))

    def test_complete_verified(self):
        self.assertEqual(decode(envelope()), {'sample.json': b'{"real":true}'})

    def test_identical_log_duplicates(self):
        self.assertEqual(decode(envelope() + '\n' + envelope()), decode(envelope()))

    def test_prefixed_log_lines(self):
        self.assertEqual(decode('\n'.join('[timestamp] ' + s for s in envelope().splitlines())), decode(envelope()))

    def test_no_artifacts_fails(self):
        with self.assertRaises(ValueError): decode('test passed')

    def test_path_escape_fails(self):
        with self.assertRaises(ValueError): decode(envelope(name='../sample.json'))

    def test_missing_end_fails(self):
        with self.assertRaises(ValueError): decode('\n'.join(envelope().splitlines()[:-1]))

    def test_missing_chunk_fails(self):
        with self.assertRaises(ValueError): decode('\n'.join(envelope().splitlines()[::2]))

    def test_conflicting_duplicate_fails(self):
        with self.assertRaises(ValueError): decode(envelope() + '\n' + envelope(b'{"real":false}'))

    def test_hash_corruption_fails(self):
        text = envelope().replace(base64.b64encode(b'{"real":true}').decode(), base64.b64encode(b'{"real":null}').decode())
        with self.assertRaises(ValueError): decode(text)

    def test_non_png_fails(self):
        with self.assertRaises(ValueError): decode(envelope(name='picture.png'))

    def test_non_wave_fails(self):
        with self.assertRaises(ValueError): decode(envelope(name='recording.wav'))

    def test_non_json_fails(self):
        with self.assertRaises(ValueError): decode(envelope(b'not json'))

    def test_extra_chunk_fails(self):
        with self.assertRaises(ValueError): decode(envelope().replace('CHUNK sample.json 0', 'CHUNK sample.json 1'))

    def test_oversized_header_fails(self):
        with self.assertRaises(ValueError): decode(envelope().replace('sample.json 13 ', 'sample.json 1500001 '))


if __name__ == '__main__': unittest.main()

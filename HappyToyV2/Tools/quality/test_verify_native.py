"""Synthetic checks of the evidence verifier; these are not native-run evidence."""
from pathlib import Path
import copy, tempfile, unittest, wave
from verify_native import sha256, verify_wav

class RetainedPcmVerifierTests(unittest.TestCase):
    def test_pcm_header_counts_checksum_and_duration(self):
        intended=Path(__file__).resolve().parent
        with tempfile.TemporaryDirectory(prefix='validator-selfcheck-',dir=intended) as directory:
            target=Path(directory).resolve()
            self.assertTrue(target.is_relative_to(intended)) # verify cleanup stays in the staging root
            wav=target/'synthetic.wav'
            with wave.open(str(wav),'wb') as stream:
                stream.setnchannels(2);stream.setsampwidth(2);stream.setframerate(48000)
                stream.writeframes(b'\xe8\x03\x18\xfc'*4800)
            entry={'sha256':sha256(wav),'samples':9600,'channels':2,'seconds':.1}
            self.assertEqual(verify_wav(wav,entry,48000,2),[])
            altered=copy.copy(entry);altered['sha256']='0'*64
            self.assertTrue(any('SHA256' in error for error in verify_wav(wav,altered,48000,2)))
            altered=copy.copy(entry);altered['samples']=9602
            self.assertTrue(any('sample/channel' in error for error in verify_wav(wav,altered,48000,2)))
            altered=copy.copy(entry);altered['seconds']=.2
            self.assertTrue(any('duration' in error for error in verify_wav(wav,altered,48000,2)))
            wav.write_bytes(wav.read_bytes()[:-4]);altered=copy.copy(entry);altered['sha256']=sha256(wav)
            self.assertTrue(any('payload is incomplete' in error for error in verify_wav(wav,altered,48000,2)))

if __name__=='__main__':unittest.main()

"""Optional independent offline ASR of the prepared Korean actor phrase.

CPython 3.11, faster-whisper==1.1.0, CPU/int8; no speaker or audio device output.
The ordinary build does not require the ASR model. Supply --download explicitly
to acquire the pinned publicly downloadable Whisper conversion on a new machine.
Recognition checks the words, not human/child voice quality or the game mix.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re

PROJECT = Path(__file__).resolve().parents[2]
MODEL = "Systran/faster-whisper-large-v2"
REVISION = "f0fe81560cb8b68660e564f55dd99207059c092e"


def check(download=False):
    from faster_whisper import WhisperModel
    from huggingface_hub import snapshot_download
    path = snapshot_download(MODEL, revision=REVISION, local_files_only=not download,
        allow_patterns=["config.json", "model.bin", "tokenizer.json", "vocabulary.json"])
    model = WhisperModel(path, device="cpu", compute_type="int8", cpu_threads=4)
    results = []
    for name in ["enemy-baby-mutter-0.wav", "enemy-baby-mutter-1.wav"]:
        cue = PROJECT / "Assets/Resources/Audio/External" / name
        segments, info = model.transcribe(str(cue), language="ko", beam_size=5, temperature=0,
            vad_filter=False, condition_on_previous_text=False)
        text = " ".join(segment.text for segment in segments)
        normalized = re.sub(r"[^가-힣]", "", text)
        results.append(dict(file=name, sha256=hashlib.sha256(cue.read_bytes()).hexdigest(),
            transcribed=text, normalized=normalized, phraseRecognized=normalized == "어디어디있어",
            language=info.language))
    return dict(status="PASS" if all(item["phraseRecognized"] for item in results) else "FAIL",
        model=MODEL, revision=REVISION, device="cpu/int8", speaker_output=False, results=results,
        scope="Independent unprompted offline ASR intelligibility; not human listening/child-voice quality, Unity import, runtime mix or lifecycle verification.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--download", action="store_true", help="Explicitly acquire the pinned public ASR model.")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    report = check(args.download)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    print(json.dumps(report, ensure_ascii=True))
    raise SystemExit(0 if report["status"] == "PASS" else 1)

"""Optional local neural Korean utterance authoring. Writes files; NEVER plays.

The normal cue rebuild uses the retained raw generated WAVs and does not require
the model, torch, network, a voice service or an account. This explicit optional
command regenerates the raw speech from pinned public MIT MeloTTS weights and
the project's own text. No real person's voice is cloned.
"""
from pathlib import Path
import argparse
import hashlib
import json
import random
import numpy as np

PROJECT = Path(__file__).resolve().parents[2]
ROOT = PROJECT / "ThirdParty/Audio/CorridorBabyMutter"
MODEL = "myshell-ai/MeloTTS-Korean"
MODEL_REVISION = "0207e5adfc90129a51b6b03d89be6d84360ed323"
TOOL_REVISION = "209145371cff8fc3bd60d7be902ea69cbdb7965a"
BERT = "kykim/bert-kor-base"
BERT_REVISION = "1779cc0982ada0216dd6de0dd4e86fb78201926d"
HASHES = {"config.json": "74543376976dfadde45ba34336fa79c7e95509f43a7c2e701b22c0f71fd7695c",
    "checkpoint.pth": "48e3ff3fd0b5348e095f0468e60ae727507564100f58142ef3a922ead6e0a4d0"}
PHRASE = "어디..? 어디있어??"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def generate(model_dir=None):
    import torch
    import soundfile as sf
    from huggingface_hub import hf_hub_download
    from transformers import AutoModelForMaskedLM, AutoTokenizer
    from melo.api import TTS
    from melo.text import korean, japanese_bert
    paths = {}
    for filename, digest in HASHES.items():
        paths[filename] = (model_dir / filename if model_dir else
            Path(hf_hub_download(MODEL, filename, revision=MODEL_REVISION)))
        assert sha(paths[filename]) == digest, "Changed model input: " + filename
    # Melo's supported Korean path uses this BERT context encoder. Pin it rather
    # than following a changing main branch during speech regeneration.
    korean.tokenizer = AutoTokenizer.from_pretrained(BERT, revision=BERT_REVISION)
    japanese_bert.tokenizers[BERT] = korean.tokenizer
    japanese_bert.models[BERT] = AutoModelForMaskedLM.from_pretrained(BERT, revision=BERT_REVISION).eval()
    # This actor has exactly two Korean words, with a reviewed pronunciation.
    # The upstream general G2pkk morphological backend auto-installs packages
    # and its Windows dictionary cannot handle some Unicode installation paths.
    # Supply the same standard pronunciation explicitly for these authored words
    # instead; reject any new word until its pronunciation is deliberately added.
    pronunciation = {"어디": "어디", "있어": "이써"}
    def character_pronunciation(text):
        if text not in pronunciation:
            raise ValueError("Unreviewed actor word: " + text)
        return pronunciation[text]
    korean.g2p_kr = character_pronunciation
    torch.set_num_threads(4)
    torch.manual_seed(211); np.random.seed(211); random.seed(211)
    model = TTS(language="KR", device="cpu", config_path=str(paths["config.json"]),
        ckpt_path=str(paths["checkpoint.pth"]))
    raw_dir = ROOT / "sources"; raw_dir.mkdir(parents=True, exist_ok=True)
    candidates = [dict(name="seeking-soft", seed=211, speed=.94, pause=.48),
        dict(name="seeking-hesitant", seed=431, speed=.88, pause=.63),
        dict(name="neutral-comparison", seed=971, speed=1.0, pause=.16)]
    sources = {}
    rate = model.hps.data.sampling_rate
    for candidate in candidates:
        torch.manual_seed(candidate["seed"]); np.random.seed(candidate["seed"]); random.seed(candidate["seed"])
        parts = []
        # Speak the two written questions as separate naturally intonated phrases
        # so the ellipsis is a hesitation, never a spoken punctuation name.
        for text in ["어디?", "어디 있어?"]:
            speech = model.tts_to_file(text, model.hps.data.spk2id["KR"], None,
                speed=candidate["speed"], sdp_ratio=.2, noise_scale=.48, noise_scale_w=.6, quiet=True)
            active = np.flatnonzero(np.abs(speech) > max(np.max(np.abs(speech)) * .015, .0002))
            assert len(active) > 0, "Generated a silent utterance"
            speech = speech[max(0, active[0] - round(.035 * rate)):min(len(speech), active[-1] + round(.07 * rate))]
            parts.append(speech)
        x = np.concatenate([parts[0], np.zeros(round(candidate["pause"] * rate)), parts[1]])
        path = raw_dir / (candidate["name"] + ".wav")
        sf.write(path, x, rate, subtype="PCM_16")
        sources[candidate["name"]] = dict(file=path.name, creator="HappyToyV2 generated speech / MyShell.ai MeloTTS",
            license="MIT", page="https://huggingface.co/" + MODEL,
            download="https://huggingface.co/" + MODEL + "/resolve/" + MODEL_REVISION + "/checkpoint.pth",
            source_file="Locally generated Korean project utterance; original input text: " + PHRASE,
            source_file_sha256=sha(path), generated=True, phrase=PHRASE,
            spoken_segments=["어디?", "어디 있어?"], model=MODEL, model_revision=MODEL_REVISION,
            tool_revision=TOOL_REVISION, auxiliary_model=BERT, auxiliary_revision=BERT_REVISION,
            authoring=dict(**candidate, sample_rate=rate, sdp_ratio=.2, noise_scale=.48, noise_scale_w=.6,
                script="SourceArt/Audio/generate_baby_mutter.py", scope="File-only local inference; no speaker output or live voice/person cloning."))
    (ROOT / "sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    provenance = dict(model=MODEL, model_revision=MODEL_REVISION, model_license="MIT",
        model_weight_hashes=HASHES, tool_revision=TOOL_REVISION, auxiliary_model=BERT,
        auxiliary_revision=BERT_REVISION, auxiliary_license="Apache-2.0 (LMkor)",
        authored_pronunciation=pronunciation,
        phrase=PHRASE, python="3.11", torch=torch.__version__, device="cpu", clones_person=False,
        hardware_playback=False, notes="Neural generated speech, then child-character formant/F0 authoring; not a recording of a real infant. Raw WAVs retained so the ordinary rebuild is fully offline.")
    (ROOT / "generation.json").write_text(json.dumps(provenance, ensure_ascii=False, indent=2) + "\n", encoding="utf8")
    print(json.dumps({"status": "PASS", "generated_candidates": len(sources), "speaker_output": False}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-dir", type=Path, help="Optional folder containing checked config.json/checkpoint.pth.")
    generate(parser.parse_args().model_dir)

# Village approach sound sources

These are original recordings published by their copyright holders under CC0
1.0, which permits commercial use and redistribution. Retrieved 2026-11-11.
No claim is made that either recording was made in Korea.

* `dog-bark-original.ogg`: Edo.pt2, "Ladrido perro", 2026-04-07, CC0 1.0.
  https://commons.wikimedia.org/wiki/File:Ladrido_perro.ogg
  Original Opus; source SHA-1 `0ac194a829fd26d68c8cc47c76754576a419c5c2`.
  SHA-256 `6646b8ab52930a967d01ec19158185599b4573d0b00507e7da21df89ebafad53`.
  Unity cannot be assumed to decode Opus in an Ogg container, so the entire
  3.7335 seconds is decoded to mono 48 kHz signed 16-bit WAV. No synthetic
  sound or editing is added. Unity output SHA-256
  `dfbed2ab842697e53e17985e10bd9495bd4a6790b707f0c90b7cf6b5f19d4a54`.
* `cicadas-original.ogg`: Ngguls, "蝉鸣", recorded Qingfeng Mountain,
  2018-07-07, CC0 1.0. https://commons.wikimedia.org/wiki/File:%E8%9D%89%E9%B8%A3.ogg
  Original Vorbis; source SHA-1 `6b93430720d5af137884f509addff63ce6145844`.
  SHA-256 `1dad0233d8ef729a489386857c938fabd7645f5ffc0e33e58cca7d940b1ff73f`.
  The 33.097-second original is copied unchanged for Unity. It is a field
  recording of cicadas, not a television or a verified Korean village sound.
* `wind-chime-original.ogg`: Membeth, "Windglockenspiel.Koshi", 2012-12-28,
  CC0 1.0. https://commons.wikimedia.org/wiki/File:Windglockenspiel.Koshi.ogg
  Original SHA-1 `1c41e7f03f9cbc886c15c60b80233964f91aaf99` and SHA-256
  `1f8b33ad604ef9ff5ba1ad901401b6d182f9db94e28f62a5ed930ff2f281b741`.
  The 58-second original Vorbis recording is decoded to mono 44.1 kHz signed
  16-bit WAV for spatial playback, with no other edits. Output SHA-256
  `ea2486dbe28568b210455e65f5a1a8fd6092ca45381940e472acc7881accb552`.
  This is a temporary cue; its instrument and location are not verified as Korean Buddhist.
  Audio listening, ambience fit and loop boundaries have not been reviewed.

License text: https://creativecommons.org/publicdomain/zero/1.0/legalcode
Run `python prepare_audio.py --ffmpeg <path-to-ffmpeg>` to reproduce. The WAV
was produced with FFmpeg 8.1; codec metadata may differ with other versions.
Imported resources are `Audio/VillageApproach/dog-bark`,
`Audio/VillageApproach/cicadas` and `Audio/VillageApproach/wind-chime`.
Audibility and looping have not been tested
by this source-preparation step.

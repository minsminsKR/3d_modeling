# Defense Web

Single-player WebGL version of the crowd shooter defense prototype.

## Run

```powershell
cd C:\Users\User\Desktop\민수\3d_modeling\game\defense_web
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
.\.venv\Scripts\python.exe app.py
```

Open:

```text
http://127.0.0.1:8020
```

For another device on the same Wi-Fi/LAN:

```powershell
.\.venv\Scripts\python.exe app.py --host 0.0.0.0 --port 8020
```

Then open:

```text
http://YOUR_PC_LAN_IP:8020
```

Windows Firewall may ask for permission the first time.

## Notes

- Flask only serves files.
- Three.js runs the actual game in the browser.
- Ranking and account APIs are intentionally deferred.
- Bulk allies, bullets, enemies, pickups, and particles use instanced rendering.
- FBX character assets are reused from `game/happy_toy/assets`.

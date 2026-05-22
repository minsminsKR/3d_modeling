import { GameScene } from "./scene.js";
import { InputController } from "./input.js";
import { NetworkClient } from "./network.js";

const canvas = document.querySelector("#game");
const stats = document.querySelector("#stats");
const leaderboard = document.querySelector("#leaderboard");
const death = document.querySelector("#death");
const join = document.querySelector("#join");
const nameInput = document.querySelector("#nameInput");
const playButton = document.querySelector("#playButton");

const scene = new GameScene(canvas);
const input = new InputController(canvas);
const net = new NetworkClient();
const params = new URLSearchParams(location.search);
const calibrationMode = params.has("calibrate");
const debugAutoplay = params.has("autoplay");
const debugFlashlight = params.has("flashlight");
const visualTestMode = params.has("visualtest");

let joined = false;
let latestSnapshot = null;
let pendingName = "";

if (calibrationMode) {
  join.style.display = "none";
  death.hidden = true;
  stats.innerHTML = "Visual calibration<br>All characters size 5<br>Green rings share the same gameplay radius";
  leaderboard.innerHTML = "<strong>Size Check</strong><br>Cyclopse · Hwacat · Uncat · Angry";
  scene.createCalibrationLineup(5);
} else if (visualTestMode) {
  joined = true;
  input.flashlightOn = debugFlashlight;
  join.style.display = "none";
  death.hidden = true;
  stats.innerHTML = "Visual flashlight test<br>Nearby and cone monsters should brighten";
  leaderboard.innerHTML = "<strong>Reveal Check</strong><br>F toggles flashlight in normal play";
  scene.setSelf("visual-player");
  latestSnapshot = {
    type: "snapshot",
    uptime: 0,
    players: [
      { id: "visual-player", name: "Tester", x: 0, z: 0, yaw: 0, size: 5, score: 0, stamina: 100, alive: true, protected: false, color: "#d88f6a" }
    ],
    enemies: [
      { id: "front-small", kind: "hwacat", x: 0, z: 46, yaw: 0, size: 3, state: "wandering", personality: "skittish" },
      { id: "front-large", kind: "uncat", x: 18, z: 70, yaw: 0, size: 30, state: "chasing", personality: "bold" },
      { id: "side-small", kind: "hwacat", x: -38, z: 8, yaw: 0, size: 2, state: "fleeing", personality: "erratic" },
      { id: "far-dark", kind: "angry", x: 70, z: 65, yaw: 0, size: 45, state: "wandering", personality: "lazy" }
    ]
  };
} else if (debugAutoplay) {
  joined = true;
  pendingName = "Test Cyclopse";
  input.flashlightOn = debugFlashlight;
  join.style.display = "none";
}

playButton.addEventListener("click", () => {
  joined = true;
  pendingName = nameInput.value;
  join.style.display = "none";
  canvas.requestPointerLock?.().catch?.(() => {});
  net.send("join", { name: pendingName });
});

nameInput.addEventListener("keydown", (event) => {
  if (event.key === "Enter") playButton.click();
});

net.on("welcome", (message) => {
  if (visualTestMode) return;
  scene.setSelf(message.id);
  if (joined && !calibrationMode) net.send("join", { name: pendingName });
});

net.on("snapshot", (snapshot) => {
  if (calibrationMode || visualTestMode) return;
  latestSnapshot = snapshot;
  const self = snapshot.players.find((p) => p.id === net.id);
  if (self) {
    const protection = self.protected ? "<br>Spawn shield active" : "";
    const godMode = self.godMode ? "<br><strong>무적 모드 ON</strong> (숫자패드 0)" : "";
    stats.innerHTML = `Size: ${self.size}<br>Score: ${Math.floor(self.score)}<br>Time: ${snapshot.uptime}s<br>Players: ${snapshot.playerCount ?? snapshot.players.length}${godMode}${protection}`;
    death.hidden = !joined || self.alive;
  }
  const top = snapshot.leaders ?? [...snapshot.players].sort((a, b) => b.score - a.score).slice(0, 6);
  leaderboard.innerHTML = `<strong>Leaderboard</strong><br>${top
    .map((p, index) => `${index + 1}. ${p.name} · ${Math.floor(p.score)}`)
    .join("<br>")}`;
});

net.on("death", () => {
  if (visualTestMode) return;
  if (input.godMode) return;
  death.hidden = false;
});

window.addEventListener("godmodechange", (event) => {
  if (!joined) return;
  net.send("godMode", { enabled: event.detail.enabled });
});

setInterval(() => {
  if (!joined) return;
  net.send("input", { input: input.snapshot() });
}, 33);

function frame() {
  scene.render(latestSnapshot, input.snapshot());
  requestAnimationFrame(frame);
}

frame();

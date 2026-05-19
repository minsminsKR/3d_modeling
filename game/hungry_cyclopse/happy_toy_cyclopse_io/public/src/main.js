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
const calibrationMode = new URLSearchParams(location.search).has("calibrate");

let joined = false;
let latestSnapshot = null;
let pendingName = "";

if (calibrationMode) {
  join.style.display = "none";
  death.hidden = true;
  stats.innerHTML = "Visual calibration<br>All characters size 5<br>Green rings share the same gameplay radius";
  leaderboard.innerHTML = "<strong>Size Check</strong><br>Cyclopse · Hwacat · Uncat · Angry";
  scene.createCalibrationLineup(5);
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
  scene.setSelf(message.id);
  if (joined && !calibrationMode) net.send("join", { name: pendingName });
});

net.on("snapshot", (snapshot) => {
  if (calibrationMode) return;
  latestSnapshot = snapshot;
  const self = snapshot.players.find((p) => p.id === net.id);
  if (self) {
    const protection = self.protected ? "<br>Spawn shield active" : "";
    const godMode = self.godMode ? "<br><strong>무적 모드 ON</strong> (숫자패드 0)" : "";
    stats.innerHTML = `Size: ${self.size}<br>Score: ${Math.floor(self.score)}<br>Time: ${snapshot.uptime}s<br>Players: ${snapshot.players.length}${godMode}${protection}`;
    death.hidden = !joined || self.alive;
  }
  const top = [...snapshot.players].sort((a, b) => b.score - a.score).slice(0, 6);
  leaderboard.innerHTML = `<strong>Leaderboard</strong><br>${top
    .map((p, index) => `${index + 1}. ${p.name} · ${Math.floor(p.score)}`)
    .join("<br>")}`;
});

net.on("death", () => {
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

import http from "node:http";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { GameWorld } from "./gameWorld.js";
import { acceptWebSocket } from "./ws.js";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const publicDir = path.resolve(__dirname, "../public");
const assetDir = path.resolve("E:/AI/3d_modeling/game/happy_toy/assets");
const port = Number(process.env.PORT || 8080);
const world = new GameWorld();

const contentTypes = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".fbx": "application/octet-stream",
  ".glb": "model/gltf-binary"
};

function serveFile(res, baseDir, routePath) {
  const safePath = path.normalize(decodeURIComponent(routePath)).replace(/^(\.\.[/\\])+/, "");
  const requested = safePath === "/" || safePath === "\\" ? "index.html" : safePath.replace(/^[/\\]/, "");
  const filePath = path.join(baseDir, requested);
  if (!filePath.startsWith(baseDir)) {
    res.writeHead(403);
    res.end("Forbidden");
    return;
  }
  fs.readFile(filePath, (error, data) => {
    if (error) {
      res.writeHead(404);
      res.end("Not found");
      return;
    }
    res.writeHead(200, { "Content-Type": contentTypes[path.extname(filePath)] || "application/octet-stream" });
    res.end(data);
  });
}

function serveStatic(req, res) {
  const url = new URL(req.url, `http://${req.headers.host}`);
  if (url.pathname.startsWith("/assets/")) {
    serveFile(res, assetDir, url.pathname.slice("/assets".length));
    return;
  }
  serveFile(res, publicDir, url.pathname);
}

const server = http.createServer(serveStatic);

server.on("upgrade", (req, socket) => {
  if (req.url !== "/ws") {
    socket.destroy();
    return;
  }
  const peer = acceptWebSocket(req, socket);
  if (!peer) return;
  const player = world.addPlayer(peer);
  peer.onMessage = (message) => world.handleMessage(player, message);
  peer.onClose = () => world.removePlayer(player.id);
});

let last = performance.now();
setInterval(() => {
  const now = performance.now();
  const dt = Math.min(0.08, (now - last) / 1000);
  last = now;
  world.tick(dt);
  if (world.shouldBroadcast(Date.now())) {
    const snapshot = world.snapshot();
    for (const player of world.players.values()) {
      player.peer.sendJson(snapshot);
    }
  }
}, 50);

server.listen(port, () => {
  console.log(`Hungry one eye Cyclopse .io server running at http://localhost:${port}`);
});

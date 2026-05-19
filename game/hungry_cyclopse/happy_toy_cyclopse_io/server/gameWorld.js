const ARENA_RADIUS = 520;
const TARGET_ENEMIES = 90;
const GIANT_START_SIZE = 60;
const PLAYER_BASE_SIZE = 5;
const WALK_SPEED = 44;
const RUN_SPEED = 70;
const GIANT_SPEED = RUN_SPEED - 2;
const STAMINA_MAX = 100;
const PLAYER_SPAWN_SAFE_RADIUS = 210;
const ENEMY_SPAWN_PLAYER_MIN_DISTANCE = 230;
const SPAWN_PROTECTION_SECONDS = 3.0;

function rand(min, max) {
  return min + Math.random() * (max - min);
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

function dist(a, b) {
  const dx = a.x - b.x;
  const dz = a.z - b.z;
  return Math.hypot(dx, dz);
}

function radiusFromSize(size) {
  return Math.max(3, (size / PLAYER_BASE_SIZE) * 3.4);
}

function randomPoint(minRadius = 0, maxRadius = ARENA_RADIUS) {
  const angle = Math.random() * Math.PI * 2;
  const radius = rand(minRadius, maxRadius);
  return { x: Math.cos(angle) * radius, z: Math.sin(angle) * radius };
}

function randomColor() {
  const colors = ["#d88f6a", "#88c0d0", "#b48ead", "#a3be8c", "#ebcb8b", "#bf616a"];
  return colors[Math.floor(Math.random() * colors.length)];
}

let nextId = 1;

export class GameWorld {
  constructor() {
    this.players = new Map();
    this.enemies = new Map();
    this.startedAt = Date.now();
    this.lastBroadcast = 0;
  }

  addPlayer(peer) {
    const id = `p${nextId++}`;
    const pos = this.#findPlayerSpawnPoint();
    const player = {
      id,
      peer,
      name: `Cyclopse ${id.slice(1)}`,
      x: pos.x,
      z: pos.z,
      yaw: 0,
      size: PLAYER_BASE_SIZE,
      score: 0,
      stamina: STAMINA_MAX,
      alive: false,
      joined: false,
      spawnProtection: SPAWN_PROTECTION_SECONDS,
      color: randomColor(),
      input: { up: false, down: false, left: false, right: false, sprint: false, yaw: Math.PI, pitch: 18 * Math.PI / 180 },
      respawnTimer: 0,
      godMode: false
    };
    this.players.set(id, player);
    peer.sendJson({ type: "welcome", id, arenaRadius: ARENA_RADIUS });
    return player;
  }

  removePlayer(id) {
    this.players.delete(id);
  }

  handleMessage(player, message) {
    if (message.type === "input" && message.input) {
      player.input = {
        up: Boolean(message.input.up),
        down: Boolean(message.input.down),
        left: Boolean(message.input.left),
        right: Boolean(message.input.right),
        sprint: Boolean(message.input.sprint),
        yaw: Number.isFinite(message.input.yaw) ? message.input.yaw : player.input.yaw,
        pitch: Number.isFinite(message.input.pitch) ? message.input.pitch : player.input.pitch
      };
      if (typeof message.input.godMode === "boolean") {
        player.godMode = message.input.godMode;
      }
    }
    if (message.type === "join") {
      if (typeof message.name === "string") player.name = message.name.slice(0, 18).trim() || player.name;
      player.joined = true;
      this.#respawn(player, false);
    }
    if (message.type === "rename" && typeof message.name === "string") {
      player.name = message.name.slice(0, 18).trim() || player.name;
    }
    if (message.type === "godMode") {
      player.godMode = Boolean(message.enabled);
      if (player.godMode && !player.alive) {
        this.#respawn(player, false);
      }
    }
  }

  tick(dt) {
    this.#maintainEnemies();
    for (const player of this.players.values()) this.#tickPlayer(player, dt);
    for (const enemy of this.enemies.values()) this.#tickEnemy(enemy, dt);
    this.#resolveContacts();
  }

  shouldBroadcast(now) {
    if (now - this.lastBroadcast < 50) return false;
    this.lastBroadcast = now;
    return true;
  }

  snapshot() {
    return {
      type: "snapshot",
      now: Date.now(),
      uptime: Math.floor((Date.now() - this.startedAt) / 1000),
      players: [...this.players.values()].filter((p) => p.joined).map((p) => ({
        id: p.id,
        name: p.name,
        x: Math.round(p.x * 10) / 10,
        z: Math.round(p.z * 10) / 10,
        yaw: Math.round(p.yaw * 100) / 100,
        size: p.size,
        score: p.score,
        stamina: Math.round(p.stamina),
        alive: p.alive,
        protected: p.spawnProtection > 0,
        godMode: p.godMode,
        color: p.color
      })),
      enemies: [...this.enemies.values()].map((e) => ({
        id: e.id,
        kind: e.kind,
        x: Math.round(e.x * 10) / 10,
        z: Math.round(e.z * 10) / 10,
        yaw: Math.round(e.yaw * 100) / 100,
        size: e.size,
        state: e.state
      }))
    };
  }

  #tickPlayer(player, dt) {
    if (!player.joined) return;
    if (!player.alive) {
      player.respawnTimer -= dt;
      if (player.respawnTimer <= 0) this.#respawn(player);
      return;
    }
    player.spawnProtection = Math.max(0, player.spawnProtection - dt);

    const input = player.input;
    let forward = 0;
    let strafe = 0;
    if (input.up) forward += 1;
    if (input.down) forward -= 1;
    if (input.right) strafe += 1;
    if (input.left) strafe -= 1;

    const length = Math.hypot(forward, strafe);
    if (length > 0) {
      forward /= length;
      strafe /= length;
      const sprinting = input.sprint && player.stamina > 2;
      const speed = sprinting ? RUN_SPEED : WALK_SPEED;
      if (sprinting) player.stamina = Math.max(0, player.stamina - 28 * dt);
      else player.stamina = Math.min(STAMINA_MAX, player.stamina + 20 * dt);

      const sin = Math.sin(input.yaw);
      const cos = Math.cos(input.yaw);
      const moveX = -Math.sin(input.yaw) * forward + cos * strafe;
      const moveZ = -Math.cos(input.yaw) * forward - sin * strafe;
      player.x += moveX * speed * dt;
      player.z += moveZ * speed * dt;
      player.yaw = Math.atan2(moveX, moveZ);
    } else {
      player.stamina = Math.min(STAMINA_MAX, player.stamina + 20 * dt);
    }

    const arenaDistance = Math.hypot(player.x, player.z);
    if (arenaDistance > ARENA_RADIUS) {
      const scale = ARENA_RADIUS / arenaDistance;
      player.x *= scale;
      player.z *= scale;
    }
    player.score += dt;
  }

  #tickEnemy(enemy, dt) {
    if (enemy.kind === "giant") {
      const target = this.#nearestAlivePlayer(enemy, enemy.state === "chasing" ? 140 : 95);
      if (target) {
        enemy.state = "chasing";
        this.#moveToward(enemy, target, GIANT_SPEED, dt);
      } else {
        enemy.state = "wandering";
        this.#wander(enemy, dt);
      }
      return;
    }

    const target = this.#nearestAlivePlayer(enemy, 90);
    if (target && Math.random() < 0.5) {
      if (enemy.size > target.size) this.#moveToward(enemy, target, enemy.speed, dt);
      else this.#moveAway(enemy, target, enemy.speed, dt);
    } else {
      this.#wander(enemy, dt);
    }
  }

  #maintainEnemies() {
    const activePlayers = [...this.players.values()].filter((p) => p.joined);
    const largest = Math.max(PLAYER_BASE_SIZE, ...activePlayers.map((p) => p.size));
    const giantPhase = largest >= GIANT_START_SIZE;
    const target = giantPhase ? clamp(2 + Math.floor((Date.now() - this.startedAt) / 45000), 2, 16) : TARGET_ENEMIES;

    for (const enemy of [...this.enemies.values()]) {
      if (giantPhase && enemy.kind !== "giant") this.enemies.delete(enemy.id);
    }

    while (this.enemies.size < target) {
      const enemy = giantPhase ? this.#makeEnemy("giant", 80) : this.#makeRegularEnemy(largest);
      this.enemies.set(enemy.id, enemy);
    }
  }

  #makeRegularEnemy(largestPlayerSize) {
    let kind = "hwacat";
    if (largestPlayerSize >= 35) {
      const roll = Math.random();
      kind = roll < 0.5 ? "hwacat" : roll < 0.8 ? "uncat" : "angry";
    } else if (largestPlayerSize >= 15) {
      kind = Math.random() < 0.65 ? "hwacat" : "uncat";
    }
    const ranges = {
      hwacat: [2, 20],
      uncat: [25, 40],
      angry: [40, 50]
    };
    const [min, max] = ranges[kind];
    return this.#makeEnemy(kind, Math.floor(rand(min, max + 1)));
  }

  #makeEnemy(kind, size) {
    const pos = this.#findEnemySpawnPoint();
    return {
      id: `e${nextId++}`,
      kind,
      x: pos.x,
      z: pos.z,
      yaw: rand(-Math.PI, Math.PI),
      size,
      speed: kind === "angry" ? 42 : kind === "uncat" ? 36 : 28,
      state: "wandering",
      turnTimer: rand(0.4, 2.4)
    };
  }

  #resolveContacts() {
    for (const player of this.players.values()) {
      if (!player.joined || !player.alive) continue;
      for (const enemy of [...this.enemies.values()]) {
        if (dist(player, enemy) >= radiusFromSize(player.size) + radiusFromSize(enemy.size)) continue;
        if (player.spawnProtection > 0) {
          this.#pushApart(player, enemy, 28);
          continue;
        }
        if (player.size > enemy.size) {
          player.size += 1;
          player.score += 10;
          this.enemies.delete(enemy.id);
          player.peer.sendJson({ type: "event", event: "eat" });
        } else if (player.size < enemy.size) {
          if (player.godMode) this.#pushApart(player, enemy, 16);
          else this.#kill(player);
        } else {
          this.#pushApart(player, enemy, 16);
        }
      }
    }

    const players = [...this.players.values()].filter((p) => p.joined && p.alive);
    for (let i = 0; i < players.length; i += 1) {
      for (let j = i + 1; j < players.length; j += 1) {
        const a = players[i];
        const b = players[j];
        if (dist(a, b) >= radiusFromSize(a.size) + radiusFromSize(b.size)) continue;
        if (a.spawnProtection > 0 || b.spawnProtection > 0) {
          this.#pushApart(a, b, 34);
          continue;
        }
        if (a.size > b.size + 1) {
          a.size += Math.max(1, Math.floor(b.size / 5));
          a.score += 25;
          if (b.godMode) this.#pushApart(a, b, 34);
          else this.#kill(b);
        } else if (b.size > a.size + 1) {
          b.size += Math.max(1, Math.floor(a.size / 5));
          b.score += 25;
          if (a.godMode) this.#pushApart(a, b, 34);
          else this.#kill(a);
        } else {
          this.#pushApart(a, b, 22);
        }
      }
    }
  }

  #kill(player) {
    if (player.godMode) return;
    player.alive = false;
    player.respawnTimer = 3.0;
    player.peer.sendJson({ type: "event", event: "death" });
  }

  #respawn(player, resetScore = true) {
    const pos = this.#findPlayerSpawnPoint();
    player.x = pos.x;
    player.z = pos.z;
    player.size = PLAYER_BASE_SIZE;
    if (resetScore) player.score = 0;
    player.stamina = STAMINA_MAX;
    player.spawnProtection = SPAWN_PROTECTION_SECONDS;
    player.alive = true;
  }

  #findPlayerSpawnPoint() {
    for (let attempt = 0; attempt < 80; attempt += 1) {
      const point = randomPoint(0, 140);
      const safeFromEnemies = [...this.enemies.values()].every((enemy) => dist(point, enemy) > PLAYER_SPAWN_SAFE_RADIUS);
      const safeFromPlayers = [...this.players.values()].every((player) => !player.joined || !player.alive || dist(point, player) > 80);
      if (safeFromEnemies && safeFromPlayers) return point;
    }
    return randomPoint(0, 60);
  }

  #findEnemySpawnPoint() {
    for (let attempt = 0; attempt < 80; attempt += 1) {
      const point = randomPoint(240, ARENA_RADIUS - 20);
      const safeFromPlayers = [...this.players.values()].every(
        (player) => !player.joined || !player.alive || dist(point, player) > ENEMY_SPAWN_PLAYER_MIN_DISTANCE
      );
      if (safeFromPlayers) return point;
    }
    return randomPoint(320, ARENA_RADIUS - 20);
  }

  #pushApart(a, b, force) {
    const dx = a.x - b.x;
    const dz = a.z - b.z;
    const len = Math.hypot(dx, dz) || 1;
    a.x += (dx / len) * force;
    a.z += (dz / len) * force;
    b.x -= (dx / len) * force;
    b.z -= (dz / len) * force;
  }

  #nearestAlivePlayer(entity, range) {
    let best = null;
    let bestDistance = range;
    for (const player of this.players.values()) {
      if (!player.alive) continue;
      const d = dist(entity, player);
      if (d < bestDistance) {
        bestDistance = d;
        best = player;
      }
    }
    return best;
  }

  #wander(entity, dt) {
    entity.turnTimer -= dt;
    if (entity.turnTimer <= 0) {
      entity.yaw += rand(-1.8, 1.8);
      entity.turnTimer = rand(0.6, 2.6);
    }
    entity.x += Math.sin(entity.yaw) * entity.speed * dt;
    entity.z += Math.cos(entity.yaw) * entity.speed * dt;
    this.#contain(entity);
  }

  #moveToward(entity, target, speed, dt) {
    const angle = Math.atan2(target.x - entity.x, target.z - entity.z);
    entity.yaw = angle;
    entity.x += Math.sin(angle) * speed * dt;
    entity.z += Math.cos(angle) * speed * dt;
    this.#contain(entity);
  }

  #moveAway(entity, target, speed, dt) {
    const angle = Math.atan2(entity.x - target.x, entity.z - target.z);
    entity.yaw = angle;
    entity.x += Math.sin(angle) * speed * dt;
    entity.z += Math.cos(angle) * speed * dt;
    this.#contain(entity);
  }

  #contain(entity) {
    const d = Math.hypot(entity.x, entity.z);
    if (d < ARENA_RADIUS) return;
    const scale = (ARENA_RADIUS - 8) / d;
    entity.x *= scale;
    entity.z *= scale;
    entity.yaw += Math.PI * 0.7;
  }
}

// 맵의 벽, 층별 walkable area, 계단/낙하 전환 구역을 관리하는 모듈입니다.
// 층 이동은 같은 X/Z에서 Y만 내리는 방식이 아니라, 명시된 transition/drop zone을 통해서만 허용합니다.

import * as THREE from "three";
import { clamp, makeAabbFromCenter } from "../utils/math.js";

const FLOOR_EPSILON = 0.18;

export class CollisionWorld {
  constructor() {
    this.blockers = [];
    this.floorAreas = [];
    this.roomAreas = [];
    this.blockedAreas = [];
    this.voidAreas = [];
    this.landingAreas = [];
    this.dropZones = [];
    this.ramps = [];
    this.transitionWaypoints = [];
    this.lastDropAttempt = null;
    this.warningCache = new Set();
  }

  addFloorArea(area, chunkId = null) {
    this.floorAreas.push({
      type: "walkable",
      ...area,
      floor: area.floor ?? inferFloorFromY(area.y),
      chunkId: chunkId ?? area.chunkId,
    });
  }

  addLandingArea(area, chunkId = null) {
    this.landingAreas.push({
      ...area,
      floor: area.floor ?? inferFloorFromY(area.position?.[1] ?? 0),
      chunkId: chunkId ?? area.chunkId,
    });
  }

  addRoomArea(area, chunkId = null) {
    this.roomAreas.push({
      ...area,
      floor: area.floor ?? 1,
      type: area.type || "room",
      chunkId: chunkId ?? area.chunkId,
    });
  }

  addBlockedArea(area, chunkId = null) {
    this.blockedAreas.push({
      ...area,
      floor: area.floor ?? 1,
      type: area.type || "blocked",
      chunkId: chunkId ?? area.chunkId,
    });
  }

  addVoidArea(area, chunkId = null) {
    this.voidAreas.push({
      ...area,
      floor: area.floor ?? 1,
      type: area.type || "void/out-of-bounds",
      chunkId: chunkId ?? area.chunkId,
    });
  }

  addDropZone(zone, chunkId = null) {
    this.dropZones.push({
      type: "dropZone",
      ...zone,
      floor: zone.floor ?? 1,
      chunkId: chunkId ?? zone.chunkId,
    });
  }

  addRamp(ramp, chunkId = null) {
    this.ramps.push({
      type: "transitionZone",
      ...ramp,
      startFloor: ramp.startFloor ?? inferFloorFromY(ramp.startY),
      endFloor: ramp.endFloor ?? inferFloorFromY(ramp.endY),
      chunkId: chunkId ?? ramp.chunkId,
    });
  }

  addTransitionWaypoint(waypoint, chunkId = null) {
    this.transitionWaypoints.push({
      ...waypoint,
      floor: waypoint.floor ?? inferFloorFromY(waypoint.position?.[1] ?? 0),
      links: waypoint.links || [],
      chunkId: chunkId ?? waypoint.chunkId,
    });
  }

  addStaticBox(id, position, size, chunkId = null) {
    this.blockers.push({
      id,
      type: "static",
      aabb: makeAabbFromCenter(position, size),
      active: () => true,
      chunkId: chunkId ?? id.split("_")[0],
    });
  }

  addDoor(door, chunkId = null) {
    this.blockers.push({
      id: door.id,
      type: door.isLocked || door.isBlocked ? "lockedDoor" : "door",
      aabb: () => door.getAabb(),
      active: () => door.isBlocking(),
      chunkId: chunkId ?? door.chunkId,
    });
  }

  clearChunkData(chunkId) {
    this.blockers = this.blockers.filter((b) => b.chunkId !== chunkId);
    this.floorAreas = this.floorAreas.filter((a) => a.chunkId !== chunkId);
    this.roomAreas = this.roomAreas.filter((a) => a.chunkId !== chunkId);
    this.blockedAreas = this.blockedAreas.filter((a) => a.chunkId !== chunkId);
    this.voidAreas = this.voidAreas.filter((a) => a.chunkId !== chunkId);
    this.landingAreas = this.landingAreas.filter((l) => l.chunkId !== chunkId);
    this.dropZones = this.dropZones.filter((z) => z.chunkId !== chunkId);
    this.ramps = this.ramps.filter((r) => r.chunkId !== chunkId);
    this.transitionWaypoints = this.transitionWaypoints.filter((w) => w.chunkId !== chunkId);
  }

  resolveCameraPosition(playerPosition, cameraPosition, wallPadding = 0.25) {
    let target = cameraPosition.clone();
    const dir = new THREE.Vector3().subVectors(cameraPosition, playerPosition);
    const dist = dir.length();
    if (dist < 0.01) {
      return target;
    }
    dir.normalize();
    const steps = 10;
    for (let i = 1; i <= steps; i++) {
      const testPoint = playerPosition.clone().addScaledVector(dir, dist * (i / steps));
      if (this.isCircleBlocked(testPoint, wallPadding)) {
        const safeDist = dist * ((i - 1) / steps);
        return playerPosition.clone().addScaledVector(dir, Math.max(0, safeDist - 0.08));
      }
    }
    return target;
  }

  getActiveBlockers(options = {}) {
    const includeDoors = options.includeDoors ?? true;
    const position = options.position || null;
    return this.blockers.filter((blocker) => {
      if (!blocker.active() || (!includeDoors && blocker.type === "door")) {
        return false;
      }

      if (!position) {
        return true;
      }

      const aabb = typeof blocker.aabb === "function" ? blocker.aabb() : blocker.aabb;
      return verticalRangeOverlaps(position.y, aabb);
    });
  }

  resolveCircle(position, radius) {
    for (const blocker of this.getActiveBlockers({ position })) {
      const aabb = typeof blocker.aabb === "function" ? blocker.aabb() : blocker.aabb;
      const collision = getCircleAabbCollision(position, radius, aabb);

      if (!collision.collides) {
        continue;
      }

      if (collision.distanceSquared > 0.00001) {
        const distance = Math.sqrt(collision.distanceSquared);
        const push = radius - distance;
        position.x += (collision.dx / distance) * push;
        position.z += (collision.dz / distance) * push;
      } else {
        const left = Math.abs(position.x - aabb.minX);
        const right = Math.abs(aabb.maxX - position.x);
        const top = Math.abs(position.z - aabb.minZ);
        const bottom = Math.abs(aabb.maxZ - position.z);
        const min = Math.min(left, right, top, bottom);
        if (min === left) position.x = aabb.minX - radius;
        else if (min === right) position.x = aabb.maxX + radius;
        else if (min === top) position.z = aabb.minZ - radius;
        else position.z = aabb.maxZ + radius;
      }
    }
    return position;
  }

  isCircleBlocked(position, radius, options = {}) {
    for (const blocker of this.getActiveBlockers({ ...options, position })) {
      const aabb = typeof blocker.aabb === "function" ? blocker.aabb() : blocker.aabb;
      if (getCircleAabbCollision(position, radius, aabb).collides) {
        return true;
      }
    }
    return false;
  }

  hasLineOfSight(start, end) {
    for (const blocker of this.getActiveBlockers({ position: start })) {
      const aabb = typeof blocker.aabb === "function" ? blocker.aabb() : blocker.aabb;
      if (!verticalRangeOverlaps(end.y ?? start.y, aabb)) {
        continue;
      }
      if (segmentIntersectsAabb2D(start.x, start.z, end.x, end.z, aabb)) {
        return false;
      }
    }
    return true;
  }

  findPath(start, goal, radius, options = {}) {
    const startSurface = this.getSurfaceAt(start, { allowAnyFloor: true });
    const goalSurface = this.getSurfaceAt(goal, { allowAnyFloor: true });
    const startFloor = options.startFloor ?? startSurface.floor ?? this.getFloorForY(start.y ?? 0);
    const goalFloor = options.goalFloor ?? goalSurface.floor ?? this.getFloorForY(goal.y ?? 0);

    if (!startFloor || !goalFloor) {
      this.warnOnce(
        `path-missing-floor-${round2(start.x)}-${round2(start.z)}-${round2(goal.x)}-${round2(goal.z)}`,
        `[CollisionWorld] Pathfinding failed because start or goal floor is unknown. start=${startSurface.type}, goal=${goalSurface.type}.`,
      );
      return [];
    }

    if (startSurface.type === "stair/transition") {
      const rampPath = this.findPathFromRamp(start, goal, radius, options, startSurface, goalFloor);
      if (rampPath.length > 0) {
        return rampPath;
      }
    }

    if (startFloor !== goalFloor) {
      if (options.allowInterFloor === false) {
        this.warnOnce(
          `path-interfloor-disabled-${startFloor}-${goalFloor}-${round2(start.x)}-${round2(start.z)}-${round2(goal.x)}-${round2(goal.z)}`,
          `[CollisionWorld] Pathfinding skipped floor transition from ${startFloor} to ${goalFloor} because allowInterFloor=false.`,
        );
        return [];
      }
      return this.findInterFloorPath(start, goal, radius, options, startFloor, goalFloor);
    }

    return this.findFloorPath(start, goal, radius, { ...options, floor: startFloor });
  }

  findFloorPath(start, goal, radius, options = {}) {
    const cellSize = options.cellSize ?? 0.85;
    const startSurface = this.getSurfaceAt(start, { preferredFloor: options.floor, allowAnyFloor: true });
    const floor = options.floor ?? startSurface.floor ?? this.getFloorForY(start.y ?? 0);
    const bounds = this.getNavigationBounds(start, goal, radius + cellSize * 2, cellSize, floor);
    const startCell = worldToCell(start, bounds, cellSize);
    const goalCell = worldToCell(goal, bounds, cellSize);
    const open = [createPathNode(startCell.x, startCell.z, 0, heuristic(startCell, goalCell), null)];
    const openByKey = new Map([[cellKey(startCell.x, startCell.z), open[0]]]);
    const closed = new Set();
    const nodes = new Map(openByKey);
    const directions = [
      [1, 0],
      [-1, 0],
      [0, 1],
      [0, -1],
    ];
    const maxIterations = options.maxIterations ?? 5200;
    let iterations = 0;

    while (open.length > 0 && iterations < maxIterations) {
      iterations += 1;
      open.sort((a, b) => a.f - b.f);
      const current = open.shift();
      const currentKey = cellKey(current.x, current.z);
      openByKey.delete(currentKey);

      if (current.x === goalCell.x && current.z === goalCell.z) {
        return rebuildPath(current, bounds, cellSize);
      }

      closed.add(currentKey);

      for (const [dx, dz] of directions) {
        const nextX = current.x + dx;
        const nextZ = current.z + dz;
        if (nextX < 0 || nextZ < 0 || nextX > bounds.cellsX || nextZ > bounds.cellsZ) {
          continue;
        }

        const nextKey = cellKey(nextX, nextZ);
        if (closed.has(nextKey)) {
          continue;
        }

        const world = cellToWorld(nextX, nextZ, bounds, cellSize);
        if (
          !(nextX === goalCell.x && nextZ === goalCell.z)
          && (
            this.isCircleBlocked(world, radius, { includeDoors: false })
            || !this.getSurfaceAt(world, { preferredFloor: floor }).walkable
          )
        ) {
          continue;
        }

        const nextCost = current.g + 1;
        const existing = nodes.get(nextKey);
        if (existing && nextCost >= existing.g) {
          continue;
        }

        const nextNode = createPathNode(
          nextX,
          nextZ,
          nextCost,
          nextCost + heuristic({ x: nextX, z: nextZ }, goalCell),
          current,
        );
        nodes.set(nextKey, nextNode);
        if (!openByKey.has(nextKey)) {
          open.push(nextNode);
          openByKey.set(nextKey, nextNode);
        }
      }
    }

    this.warnOnce(
      `path-fail-floor-${floor}-${round2(start.x)}-${round2(start.z)}-${round2(goal.x)}-${round2(goal.z)}`,
      `[CollisionWorld] Pathfinding failed on floor ${floor}: start x=${start.x.toFixed(2)}, z=${start.z.toFixed(2)} -> goal x=${goal.x.toFixed(2)}, z=${goal.z.toFixed(2)}.`,
    );
    return [];
  }

  findInterFloorPath(start, goal, radius, options, startFloor, goalFloor) {
    const route = this.findTransitionRoute(start, goal, startFloor, goalFloor);
    if (route.length === 0) {
      this.warnOnce(
        `path-no-transition-${startFloor}-${goalFloor}`,
        `[CollisionWorld] Pathfinding failed: no transition route from floor ${startFloor} to floor ${goalFloor}.`,
      );
      return [];
    }

    const fullPath = [];
    let current = start;
    let currentFloor = startFloor;

    for (const waypoint of route) {
      const waypointPoint = pointFromArray(waypoint.position);
      if (waypoint.floor === currentFloor) {
        const segment = this.findFloorPath(current, waypointPoint, radius, { ...options, floor: currentFloor });
        if (segment.length === 0) {
          this.warnOnce(
            `path-fail-transition-entry-${waypoint.id}-${round2(current.x)}-${round2(current.z)}`,
            `[CollisionWorld] Pathfinding failed before transition waypoint ${waypoint.id}.`,
          );
          return [];
        }
        appendPathSegment(fullPath, segment);
      } else {
        appendPathSegment(fullPath, [waypointPoint]);
      }
      current = waypointPoint;
      currentFloor = waypoint.floor;
    }

    const finalSegment = this.findFloorPath(current, goal, radius, { ...options, floor: goalFloor });
    if (finalSegment.length === 0) {
      this.warnOnce(
        `path-fail-transition-exit-${goalFloor}-${round2(goal.x)}-${round2(goal.z)}`,
        `[CollisionWorld] Pathfinding failed after transition route on floor ${goalFloor}.`,
      );
      return [];
    }

    appendPathSegment(fullPath, finalSegment);
    return fullPath;
  }

  findPathFromRamp(start, goal, radius, options, startSurface, goalFloor) {
    const ramp = this.ramps.find((entry) => entry.id === startSurface.id);
    if (!ramp) {
      return [];
    }

    const exitFloor = goalFloor === ramp.startFloor || goalFloor === ramp.endFloor
      ? goalFloor
      : startSurface.floor;
    const exitWaypoint = this.findNearestTransitionWaypoint(start, { floor: exitFloor });
    if (!exitWaypoint) {
      return [];
    }

    const exitPoint = pointFromArray(exitWaypoint.position);
    if (exitFloor !== goalFloor) {
      return [clonePoint(start), exitPoint];
    }

    const finalSegment = this.findFloorPath(exitPoint, goal, radius, { ...options, floor: goalFloor });
    if (finalSegment.length === 0) {
      this.warnOnce(
        `path-fail-ramp-exit-${exitWaypoint.id}-${round2(goal.x)}-${round2(goal.z)}`,
        `[CollisionWorld] Pathfinding failed after ramp exit ${exitWaypoint.id}.`,
      );
      return [clonePoint(start), exitPoint];
    }

    const path = [clonePoint(start), exitPoint];
    appendPathSegment(path, finalSegment);
    return path;
  }

  findTransitionRoute(start, goal, startFloor, goalFloor) {
    const startCandidates = this.transitionWaypoints
      .filter((waypoint) => waypoint.floor === startFloor)
      .sort((a, b) => distance2DSquared(start, pointFromArray(a.position)) - distance2DSquared(start, pointFromArray(b.position)));
    const goalCandidates = this.transitionWaypoints.filter((waypoint) => waypoint.floor === goalFloor);
    const goalIds = new Set(goalCandidates.map((waypoint) => waypoint.id));
    let bestRoute = [];
    let bestScore = Infinity;

    for (const startWaypoint of startCandidates) {
      const route = this.searchWaypointRoute(startWaypoint.id, goalIds);
      if (route.length === 0) {
        continue;
      }
      const firstPoint = pointFromArray(route[0].position);
      const lastPoint = pointFromArray(route[route.length - 1].position);
      const score = distance2DSquared(start, firstPoint) + route.length * 4 + distance2DSquared(goal, lastPoint);
      if (score < bestScore) {
        bestRoute = route;
        bestScore = score;
      }
    }

    return bestRoute;
  }

  searchWaypointRoute(startId, goalIds) {
    const waypointById = new Map(this.transitionWaypoints.map((waypoint) => [waypoint.id, waypoint]));
    const queue = [{ id: startId, path: [startId] }];
    const visited = new Set([startId]);

    while (queue.length > 0) {
      const current = queue.shift();
      if (goalIds.has(current.id)) {
        return current.path.map((id) => waypointById.get(id)).filter(Boolean);
      }

      const waypoint = waypointById.get(current.id);
      for (const nextId of waypoint?.links || []) {
        if (visited.has(nextId) || !waypointById.has(nextId)) {
          continue;
        }
        visited.add(nextId);
        queue.push({ id: nextId, path: [...current.path, nextId] });
      }
    }

    return [];
  }

  canNavigateBetween(start, goal) {
    const startSurface = this.getSurfaceAt(start, { allowAnyFloor: true });
    const goalSurface = this.getSurfaceAt(goal, { allowAnyFloor: true });
    if (!startSurface.walkable || !goalSurface.walkable) {
      return false;
    }
    if (startSurface.floor === goalSurface.floor) {
      return true;
    }
    return this.findTransitionRoute(start, goal, startSurface.floor, goalSurface.floor).length > 0;
  }

  findNearestTransitionWaypoint(position, options = {}) {
    const floor = options.floor ?? this.getSurfaceAt(position, { allowAnyFloor: true }).floor;
    let bestWaypoint = null;
    let bestDistance = Infinity;
    for (const waypoint of this.transitionWaypoints) {
      if (floor && waypoint.floor !== floor) {
        continue;
      }
      const distance = distance2DSquared(position, pointFromArray(waypoint.position));
      if (distance < bestDistance) {
        bestWaypoint = waypoint;
        bestDistance = distance;
      }
    }
    return bestWaypoint;
  }

  getNavigationBounds(start, goal, padding, cellSize, floor = null) {
    let minX = Math.min(start.x, goal.x);
    let maxX = Math.max(start.x, goal.x);
    let minZ = Math.min(start.z, goal.z);
    let maxZ = Math.max(start.z, goal.z);

    for (const blocker of this.blockers) {
      const aabb = typeof blocker.aabb === "function" ? blocker.aabb() : blocker.aabb;
      minX = Math.min(minX, aabb.minX);
      maxX = Math.max(maxX, aabb.maxX);
      minZ = Math.min(minZ, aabb.minZ);
      maxZ = Math.max(maxZ, aabb.maxZ);
    }

    minX -= padding;
    maxX += padding;
    minZ -= padding;
    maxZ += padding;

    return {
      minX,
      minZ,
      y: this.getFloorY(floor) ?? start.y ?? 0,
      floor,
      cellsX: Math.ceil((maxX - minX) / cellSize),
      cellsZ: Math.ceil((maxZ - minZ) / cellSize),
    };
  }

  resolveActorPosition(previousPosition, candidatePosition, radius, options = {}) {
    const previous = clonePoint(previousPosition);
    const candidate = clonePoint(candidatePosition);
    const actorId = options.actorId || "actor";
    const previousSurface = this.getSurfaceAt(previous);
    const preferredFloor = this.getPreferredFloorAfterMove(previousSurface, previous, candidate);
    const candidateSurface = this.getSurfaceAt(candidate, { preferredFloor });

    if (candidateSurface.walkable) {
      candidatePosition.x = candidate.x;
      candidatePosition.y = candidateSurface.y;
      candidatePosition.z = candidate.z;
      return {
        allowed: true,
        surface: candidateSurface,
        position: candidatePosition,
      };
    }

    const fromFloor = previousSurface.floor ?? preferredFloor;
    const dropZone = this.findDropZoneAt(candidate, fromFloor);
    if (dropZone) {
      const dropResult = this.resolveDropZone(dropZone, actorId);
      if (dropResult?.surface?.walkable) {
        candidatePosition.x = dropResult.position.x;
        candidatePosition.y = dropResult.position.y;
        candidatePosition.z = dropResult.position.z;
        return {
          allowed: true,
          surface: dropResult.surface,
          position: candidatePosition,
          drop: dropResult.drop,
        };
      }

      this.cancelInvalidTransition(actorId, previous, candidate, `invalid drop landing for ${dropZone.id}`);
      candidatePosition.x = previous.x;
      candidatePosition.y = previous.y;
      candidatePosition.z = previous.z;
      return {
        allowed: false,
        surface: previousSurface,
        position: candidatePosition,
      };
    }

    this.cancelInvalidTransition(actorId, previous, candidate, `blocked ${candidateSurface.type} on floor ${fromFloor ?? "unknown"}`);
    candidatePosition.x = previous.x;
    candidatePosition.y = previous.y;
    candidatePosition.z = previous.z;
    return {
      allowed: false,
      surface: previousSurface,
      position: candidatePosition,
    };
  }

  snapToValidSurface(position, options = {}) {
    const preferredFloor = options.floor ?? this.getFloorForY(position.y ?? 0);
    const surface = this.getSurfaceAt(position, { preferredFloor, allowAnyFloor: true });
    if (!surface.walkable) {
      this.warnOnce(
        `snap-${options.actorId || "actor"}-${round2(position.x)}-${round2(position.z)}`,
        `[CollisionWorld] Invalid spawn/snap position for ${options.actorId || "actor"} at x=${position.x.toFixed(2)}, z=${position.z.toFixed(2)}.`,
      );
      return {
        position,
        surface,
      };
    }

    position.y = surface.y;
    return {
      position,
      surface,
    };
  }

  getGroundY(position, options = {}) {
    const preferredFloor = options.floor ?? this.getFloorForY(position.y ?? 0);
    const surface = this.getSurfaceAt(position, { preferredFloor, allowAnyFloor: Boolean(options.allowAnyFloor) });
    return surface.walkable ? surface.y : position.y ?? 0;
  }

  getSurfaceAt(position, options = {}) {
    const preferredFloor = options.preferredFloor ?? this.getFloorForY(position.y ?? 0);
    const ramp = this.findRampAt(position);
    if (ramp) {
      const y = sampleRampY(ramp, position);
      return {
        id: ramp.id,
        type: "stair/transition",
        floor: nearestFloorForRamp(ramp, y),
        startFloor: ramp.startFloor,
        endFloor: ramp.endFloor,
        y,
        walkable: true,
      };
    }

    const blockedArea = this.findTypedAreaAt(this.blockedAreas, position, preferredFloor);
    if (blockedArea) {
      return {
        id: blockedArea.id,
        type: blockedArea.type,
        floor: blockedArea.floor,
        y: position.y ?? this.getFloorY(blockedArea.floor) ?? 0,
        walkable: false,
      };
    }

    const voidArea = this.findTypedAreaAt(this.voidAreas, position, preferredFloor);
    if (voidArea) {
      return {
        id: voidArea.id,
        type: voidArea.type,
        floor: voidArea.floor,
        y: position.y ?? this.getFloorY(voidArea.floor) ?? 0,
        walkable: false,
      };
    }

    const preferredArea = this.findWalkableAreaAt(position, preferredFloor);
    if (preferredArea) {
      return surfaceFromArea(preferredArea);
    }

    if (options.allowAnyFloor) {
      const nearestArea = this.findNearestWalkableAreaAt(position);
      if (nearestArea) {
        return surfaceFromArea(nearestArea);
      }
    }

    const dropZone = this.findDropZoneAt(position, preferredFloor);
    if (dropZone) {
      return {
        id: dropZone.id,
        type: "dropZone",
        floor: dropZone.floor,
        y: position.y ?? this.getFloorY(dropZone.floor) ?? 0,
        walkable: false,
        targetFloor: dropZone.targetFloor,
        targetLandingId: dropZone.targetLandingId,
        targetLandingPosition: dropZone.targetLandingPosition,
      };
    }

    if (this.isCircleBlocked(position, 0.05)) {
      return {
        id: "wall",
        type: "wall",
        floor: preferredFloor,
        y: position.y ?? this.getFloorY(preferredFloor) ?? 0,
        walkable: false,
      };
    }

    return {
      id: "void",
      type: "void/out-of-bounds",
      floor: preferredFloor,
      y: position.y ?? this.getFloorY(preferredFloor) ?? 0,
      walkable: false,
    };
  }

  getDebugState(position) {
    const surface = this.getSurfaceAt(position);
    const below = this.getBelowLandingInfo(position, surface.floor);
    return {
      floor: surface.floor ?? "?",
      x: position.x,
      z: position.z,
      tileType: surface.type,
      tileId: surface.id,
      belowValidLanding: below.valid,
      belowFloor: below.floor,
      belowTileType: below.surface?.type || "none",
      lastDropAttempt: this.lastDropAttempt,
      areaCounts: this.getAreaDebugCounts(),
      transitionWaypoints: this.getTransitionDebug(),
    };
  }

  clearDropAttempt() {
    this.lastDropAttempt = null;
  }

  getBelowLandingInfo(position, floor = null) {
    const currentFloor = floor ?? this.getFloorForY(position.y ?? 0);
    const belowFloor = this.getFloorBelow(currentFloor);
    if (!belowFloor) {
      return { valid: false, floor: null, surface: null };
    }

    const probe = {
      x: position.x,
      y: this.getFloorY(belowFloor) ?? 0,
      z: position.z,
    };
    const surface = this.getSurfaceAt(probe, { preferredFloor: belowFloor });
    return {
      valid: surface.walkable,
      floor: belowFloor,
      surface,
    };
  }

  getPreferredFloorAfterMove(previousSurface, previous, candidate) {
    if (previousSurface.type === "stair/transition") {
      const ramp = this.ramps.find((entry) => entry.id === previousSurface.id);
      if (ramp) {
        const exitFloor = getRampExitFloor(ramp, previous, candidate);
        if (exitFloor) {
          return exitFloor;
        }
      }
    }

    return previousSurface.floor ?? this.getFloorForY(previous.y ?? 0);
  }

  resolveDropZone(dropZone, actorId) {
    const target = this.resolveDropTarget(dropZone);
    const targetInfo = {
      id: dropZone.id,
      floor: dropZone.floor,
      targetFloor: dropZone.targetFloor,
      targetLandingId: dropZone.targetLandingId || null,
      targetLandingPosition: dropZone.targetLandingPosition || null,
    };

    if (!target) {
      this.lastDropAttempt = {
        status: "blocked",
        reason: "missing target landing",
        ...targetInfo,
      };
      this.warnOnce(
        `drop-missing-${dropZone.id}`,
        `[CollisionWorld] Drop zone ${dropZone.id} has no valid target landing.`,
      );
      return null;
    }

    const surface = this.getSurfaceAt(target, { preferredFloor: dropZone.targetFloor });
    if (!surface.walkable) {
      this.lastDropAttempt = {
        status: "blocked",
        reason: "target landing is not walkable",
        targetTileType: surface.type,
        ...targetInfo,
      };
      this.warnOnce(
        `drop-invalid-${dropZone.id}-${round2(target.x)}-${round2(target.z)}`,
        `[CollisionWorld] Drop zone ${dropZone.id} tried to land ${actorId} on invalid floor ${dropZone.targetFloor} at x=${target.x.toFixed(2)}, z=${target.z.toFixed(2)}.`,
      );
      return null;
    }

    this.lastDropAttempt = {
      status: "landed",
      reason: "validated drop zone",
      targetTileType: surface.type,
      ...targetInfo,
    };
    return {
      position: target,
      surface,
      drop: this.lastDropAttempt,
    };
  }

  resolveDropTarget(dropZone) {
    if (dropZone.targetLandingId) {
      const landing = this.landingAreas.find((entry) => entry.id === dropZone.targetLandingId);
      if (!landing) {
        return null;
      }
      return pointFromArray(landing.position);
    }

    if (dropZone.targetLandingPosition) {
      return pointFromArray(dropZone.targetLandingPosition);
    }

    return null;
  }

  cancelInvalidTransition(actorId, previous, candidate, reason) {
    this.lastDropAttempt = {
      status: "blocked",
      reason,
      from: { x: previous.x, y: previous.y, z: previous.z },
      attempted: { x: candidate.x, y: candidate.y, z: candidate.z },
      targetFloor: null,
      targetLandingId: null,
    };
    this.warnOnce(
      `transition-${actorId}-${reason}-${round2(candidate.x)}-${round2(candidate.z)}`,
      `[CollisionWorld] Cancelled invalid floor transition for ${actorId}: ${reason}. Attempted x=${candidate.x.toFixed(2)}, y=${candidate.y.toFixed(2)}, z=${candidate.z.toFixed(2)}.`,
    );
  }

  findRampAt(position) {
    return this.ramps.find((ramp) => (
      position.x >= ramp.minX
      && position.x <= ramp.maxX
      && position.z >= ramp.minZ
      && position.z <= ramp.maxZ
    )) || null;
  }

  findWalkableAreaAt(position, floor) {
    return this.floorAreas.find((area) => (
      area.floor === floor
      && contains2D(area, position)
    )) || null;
  }

  findNearestWalkableAreaAt(position) {
    let bestArea = null;
    let bestDistance = Infinity;
    for (const area of this.floorAreas) {
      if (!contains2D(area, position)) {
        continue;
      }

      const distance = Math.abs((position.y ?? 0) - area.y);
      if (distance < bestDistance) {
        bestArea = area;
        bestDistance = distance;
      }
    }
    return bestArea;
  }

  findDropZoneAt(position, floor) {
    return this.dropZones.find((zone) => (
      zone.floor === floor
      && contains2D(zone, position)
    )) || null;
  }

  findTypedAreaAt(areas, position, floor) {
    return areas.find((area) => (
      area.floor === floor
      && contains2D(area, position)
    )) || null;
  }

  getTransitionDebug() {
    return this.transitionWaypoints.map((waypoint) => ({
      id: waypoint.id,
      floor: waypoint.floor,
      type: waypoint.type,
      position: pointFromArray(waypoint.position),
      links: [...(waypoint.links || [])],
    }));
  }

  getAreaDebugCounts() {
    return {
      walkable: this.floorAreas.length,
      room: this.roomAreas.length,
      blocked: this.blockedAreas.length,
      void: this.voidAreas.length,
      stair: this.ramps.length,
      door: this.blockers.filter((blocker) => blocker.type === "door" || blocker.type === "lockedDoor").length,
    };
  }

  getFloorForY(y) {
    let bestFloor = null;
    let bestDistance = Infinity;
    for (const area of this.floorAreas) {
      const distance = Math.abs((y ?? 0) - area.y);
      if (distance < bestDistance) {
        bestFloor = area.floor;
        bestDistance = distance;
      }
    }
    return bestFloor;
  }

  getFloorY(floor) {
    const area = this.floorAreas.find((entry) => entry.floor === floor);
    return area?.y ?? null;
  }

  getFloorBelow(floor) {
    const floors = [...new Set(this.floorAreas.map((area) => area.floor))].sort((a, b) => a - b);
    const index = floors.indexOf(floor);
    return index > 0 ? floors[index - 1] : null;
  }

  warnOnce(key, message) {
    if (this.warningCache.has(key)) {
      return;
    }
    this.warningCache.add(key);
    console.warn(message);
  }
}

function surfaceFromArea(area) {
  return {
    id: area.id,
    type: area.type || "walkable",
    floor: area.floor,
    y: area.y,
    walkable: true,
  };
}

function inferFloorFromY(y) {
  return (y ?? 0) > 1.5 ? 2 : 1;
}

function sampleRampY(ramp, position) {
  const axisStart = ramp.axis === "x" ? ramp.startX : ramp.startZ;
  const axisEnd = ramp.axis === "x" ? ramp.endX : ramp.endZ;
  const axisValue = ramp.axis === "x" ? position.x : position.z;
  const axisLength = axisEnd - axisStart;
  const progress = Math.abs(axisLength) > 0.0001
    ? clamp((axisValue - axisStart) / axisLength, 0, 1)
    : 0;
  return ramp.startY + (ramp.endY - ramp.startY) * progress;
}

function nearestFloorForRamp(ramp, y) {
  return Math.abs(y - ramp.startY) <= Math.abs(y - ramp.endY)
    ? ramp.startFloor
    : ramp.endFloor;
}

function getRampExitFloor(ramp, previous, candidate) {
  if (ramp.axis === "z") {
    const towardNegative = ramp.endZ < ramp.startZ;
    if (towardNegative) {
      if (candidate.z <= ramp.minZ + FLOOR_EPSILON) return ramp.endFloor;
      if (candidate.z >= ramp.maxZ - FLOOR_EPSILON) return ramp.startFloor;
    } else {
      if (candidate.z >= ramp.maxZ - FLOOR_EPSILON) return ramp.endFloor;
      if (candidate.z <= ramp.minZ + FLOOR_EPSILON) return ramp.startFloor;
    }
  }

  if (ramp.axis === "x") {
    const towardNegative = ramp.endX < ramp.startX;
    if (towardNegative) {
      if (candidate.x <= ramp.minX + FLOOR_EPSILON) return ramp.endFloor;
      if (candidate.x >= ramp.maxX - FLOOR_EPSILON) return ramp.startFloor;
    } else {
      if (candidate.x >= ramp.maxX - FLOOR_EPSILON) return ramp.endFloor;
      if (candidate.x <= ramp.minX + FLOOR_EPSILON) return ramp.startFloor;
    }
  }

  return nearestFloorForRamp(ramp, previous.y ?? ramp.startY);
}

function contains2D(area, position) {
  return (
    position.x >= area.minX
    && position.x <= area.maxX
    && position.z >= area.minZ
    && position.z <= area.maxZ
  );
}

function pointFromArray(value) {
  return {
    x: value[0],
    y: value[1],
    z: value[2],
  };
}

function clonePoint(value) {
  return {
    x: value.x,
    y: value.y ?? 0,
    z: value.z,
  };
}

function round2(value) {
  return Math.round(value * 100) / 100;
}

function getCircleAabbCollision(position, radius, aabb) {
  const closestX = clamp(position.x, aabb.minX, aabb.maxX);
  const closestZ = clamp(position.z, aabb.minZ, aabb.maxZ);
  const dx = position.x - closestX;
  const dz = position.z - closestZ;
  const distanceSquared = dx * dx + dz * dz;

  return {
    collides: distanceSquared < radius * radius,
    distanceSquared,
    dx,
    dz,
  };
}

function createPathNode(x, z, g, f, parent) {
  return { x, z, g, f, parent };
}

function heuristic(a, b) {
  return Math.abs(a.x - b.x) + Math.abs(a.z - b.z);
}

function cellKey(x, z) {
  return `${x},${z}`;
}

function worldToCell(point, bounds, cellSize) {
  return {
    x: clamp(Math.round((point.x - bounds.minX) / cellSize), 0, bounds.cellsX),
    z: clamp(Math.round((point.z - bounds.minZ) / cellSize), 0, bounds.cellsZ),
  };
}

function cellToWorld(x, z, bounds, cellSize) {
  return {
    x: bounds.minX + x * cellSize,
    y: bounds.y,
    z: bounds.minZ + z * cellSize,
  };
}

function rebuildPath(node, bounds, cellSize) {
  const path = [];
  let current = node;
  while (current) {
    path.push(cellToWorld(current.x, current.z, bounds, cellSize));
    current = current.parent;
  }
  return path.reverse();
}

function appendPathSegment(target, segment) {
  for (const point of segment) {
    const previous = target[target.length - 1];
    if (
      previous
      && Math.abs(previous.x - point.x) < 0.001
      && Math.abs(previous.y - point.y) < 0.001
      && Math.abs(previous.z - point.z) < 0.001
    ) {
      continue;
    }
    target.push(point);
  }
}

function distance2DSquared(a, b) {
  const dx = a.x - b.x;
  const dz = a.z - b.z;
  return dx * dx + dz * dz;
}

function verticalRangeOverlaps(y, aabb) {
  if (!Number.isFinite(aabb.minY) || !Number.isFinite(aabb.maxY)) {
    return true;
  }

  return y >= aabb.minY - 0.35 && y <= aabb.maxY + 0.35;
}

function segmentIntersectsAabb2D(x1, z1, x2, z2, aabb) {
  let tMin = 0;
  let tMax = 1;
  const dx = x2 - x1;
  const dz = z2 - z1;

  const checks = [
    [-dx, x1 - aabb.minX],
    [dx, aabb.maxX - x1],
    [-dz, z1 - aabb.minZ],
    [dz, aabb.maxZ - z1],
  ];

  for (const [p, q] of checks) {
    if (Math.abs(p) < 0.00001) {
      if (q < 0) return false;
      continue;
    }
    const ratio = q / p;
    if (p < 0) tMin = Math.max(tMin, ratio);
    else tMax = Math.min(tMax, ratio);
    if (tMin > tMax) return false;
  }

  return true;
}

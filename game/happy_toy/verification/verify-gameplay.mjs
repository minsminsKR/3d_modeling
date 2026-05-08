import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8013/";

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
const browserErrors = [];
page.on("console", (message) => {
  if (message.type() === "error") {
    browserErrors.push(message.text());
  }
});
page.on("pageerror", (error) => browserErrors.push(error.message));

try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 60000 });
  await page.waitForFunction(
    () => window.__happyToy?.enemyManager?.enemies?.length === 2,
    null,
    { timeout: 90000 },
  );

  const initialState = await page.evaluate(() => {
    const game = window.__happyToy;
    return {
      keys: game.keys.length,
      cabinets: game.cabinets.length,
      doors: game.doors.length,
      hasFinalExit: Boolean(game.finalExit),
      floorAreas: game.collisionWorld.floorAreas.length,
      landingAreas: game.collisionWorld.landingAreas.length,
      dropZones: game.collisionWorld.dropZones.length,
      ramps: game.collisionWorld.ramps.length,
      roomAreas: game.collisionWorld.roomAreas.length,
      blockedAreas: game.collisionWorld.blockedAreas.length,
      voidAreas: game.collisionWorld.voidAreas.length,
      transitionWaypoints: game.collisionWorld.transitionWaypoints.length,
      lockedDoors: game.doors.filter((door) => door.isLocked || door.isBlocked).length,
      connectedSecondFloorDoors: game.doors.filter((door) => door.position.y > 3 && door.connectedRoomId).length,
      hasRuntimeAudio: Boolean(game.audioManager),
      hasHorrorEventManager: Boolean(game.horrorEventManager),
      horrorLights: game.horrorLights.length,
      cicadaProps: [
        "cicada-window-1f-north",
        "cicada-window-2f-north",
        "corridor-wire-1f",
        "cicada-shells-2f",
        "silent-mannequin-2f",
      ].filter((name) => Boolean(game.scene.getObjectByName(name))).length,
      playerZ: game.player.position.z,
      enemyStates: game.enemyManager.enemies.map((enemy) => enemy.state),
      canvasWidth: game.renderer.domElement.width,
      canvasHeight: game.renderer.domElement.height,
    };
  });

  assert(initialState.keys === 3, `expected 3 keys, got ${initialState.keys}`);
  assert(initialState.cabinets >= 4, `expected at least 4 cabinets, got ${initialState.cabinets}`);
  assert(initialState.doors >= 7, `expected two-floor map doors, got ${initialState.doors}`);
  assert(initialState.floorAreas >= 5, `expected registered floor areas, got ${initialState.floorAreas}`);
  assert(initialState.landingAreas >= 3, `expected registered landing areas, got ${initialState.landingAreas}`);
  assert(initialState.dropZones >= 1, `expected explicit drop zones, got ${initialState.dropZones}`);
  assert(initialState.ramps >= 1, `expected registered stair ramp, got ${initialState.ramps}`);
  assert(initialState.roomAreas >= 3, `expected second-floor room/event areas, got ${initialState.roomAreas}`);
  assert(initialState.blockedAreas >= 3, `expected explicit blocked areas, got ${initialState.blockedAreas}`);
  assert(initialState.voidAreas >= 2, `expected explicit void debug areas, got ${initialState.voidAreas}`);
  assert(initialState.transitionWaypoints >= 2, `expected stair transition waypoints, got ${initialState.transitionWaypoints}`);
  assert(initialState.lockedDoors >= 1, `expected at least one locked/blocked 2F door, got ${initialState.lockedDoors}`);
  assert(initialState.connectedSecondFloorDoors >= 2, `expected 2F opening doors to have connected rooms, got ${initialState.connectedSecondFloorDoors}`);
  assert(!initialState.hasRuntimeAudio, "expected runtime audio to be disabled until real sound assets are provided");
  assert(initialState.hasHorrorEventManager, "expected HorrorEventManager for cicada corridor events");
  assert(initialState.horrorLights >= 8, `expected flicker-controlled horror lights, got ${initialState.horrorLights}`);
  assert(initialState.cicadaProps >= 5, `expected cicada corridor props, got ${initialState.cicadaProps}`);
  assert(initialState.hasFinalExit, "expected final exit object");
  assert(initialState.playerZ > 20, "expected player to start in expanded south corridor");
  assert(initialState.enemyStates.every((state) => state === "patrol"), "expected enemies to start on patrol");
  assert(initialState.canvasWidth > 0 && initialState.canvasHeight > 0, "expected non-empty WebGL canvas");

  const giveUpState = await page.evaluate(() => {
    const game = window.__happyToy;
    const enemy = game.enemyManager.enemies[0];
    enemy.state = "chase";
    enemy.memoryTimer = enemy.config.memorySeconds;
    enemy.lastKnownPlayerPosition = game.player.position.clone();
    enemy.group.position.set(0, 0, 0);
    game.player.position.set(enemy.config.giveUpRange + 4, 0, 0);
    game.enemyManager.update(0.016, { position: game.player.position, isHidden: false });
    return enemy.state;
  });
  assert(giveUpState === "patrol", `expected chase give-up to return patrol, got ${giveUpState}`);

  const cyclopseGroundState = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    const cyclopse = game.enemyManager.enemies.find((enemy) => enemy.config.id === "cyclopse");
    cyclopse.state = "chase";
    cyclopse.memoryTimer = cyclopse.config.memorySeconds;
    cyclopse.group.position.set(7.3, 0, -7);
    game.player.position.set(5.8, 0, -7);
    cyclopse.lastKnownPlayerPosition = game.player.position.clone();
    for (let i = 0; i < 40; i += 1) {
      cyclopse.update(1 / 60, { position: game.player.position, isHidden: false });
    }
    return {
      state: cyclopse.state,
      action: cyclopse.currentActionName,
      groundOffset: cyclopse.getModelGroundOffset(),
      visualGroundSink: cyclopse.config.visualGroundSink || 0,
    };
  });
  assert(cyclopseGroundState.action === "chase", `expected Cyclopse chase action, got ${cyclopseGroundState.action}`);
  const expectedCyclopseOffset = -(cyclopseGroundState.visualGroundSink || 0);
  assert(
    Math.abs(cyclopseGroundState.groundOffset - expectedCyclopseOffset) <= 0.035,
    `expected Cyclopse visual ground sink ${expectedCyclopseOffset}, got offset ${cyclopseGroundState.groundOffset}`,
  );

  const floorState = await page.evaluate(() => {
    const game = window.__happyToy;
    const stairRamp = game.collisionWorld.ramps.find((ramp) => ramp.id === "west-stair-ramp");
    const bottomY = game.collisionWorld.getGroundY({ x: -7, y: 0, z: 20 });
    const midY = game.collisionWorld.getGroundY({ x: -7, y: 1.7, z: 14.9 });
    const topY = game.collisionWorld.getGroundY({ x: -7, y: 3.4, z: 9.45 });
    const upperY = game.collisionWorld.getGroundY({ x: 0, y: 3.4, z: -8 });
    const upperStairGuard = game.collisionWorld.blockers.find((blocker) => blocker.id?.startsWith("upper-stair-guard"));
    const secondLandingPanel = game.scene.getObjectByName("second-landing-panel");
    const secondStairTopPanel = game.scene.getObjectByName("second-stair-top-panel");
    const stairDoor = game.doors.find((door) => door.id === "door-stairwell");
    const stairDoorFrame = game.scene.getObjectByName("door-stairwell-frame");
    const landing = game.scene.getObjectByName("west-stair-bottom-landing");
    const landingWidth = landing?.geometry?.parameters?.width ?? 0;
    const stairPosts = game.scene.children.filter((object) => object.name?.startsWith("west-stair-post-")).length;
    const wallHandrails = game.scene.children.filter((object) => object.name?.startsWith("west-stair-wall-handrail-")).length;
    const ceilingPieces = game.scene.children.filter((object) => object.name?.startsWith("first-floor-ceiling-")).length;
    const lowerInnerGuard = game.collisionWorld.blockers.find((blocker) => blocker.id === "stair-open-inner-guard");
    const openCorridorEntryClear = !game.collisionWorld.isCircleBlocked({ x: -2.85, y: 0, z: 22 }, 0.3);
    const lowerStairSideClear = !game.collisionWorld.isCircleBlocked({ x: -4.55, y: 0, z: 17.2 }, 0.3);
    const stairTopClear = !game.collisionWorld.isCircleBlocked({ x: -7.0, y: 3.4, z: 9.8 }, 0.34);
    const turnIntoUpperCorridorClear = !game.collisionWorld.isCircleBlocked({ x: -4.4, y: 3.4, z: 9.8 }, 0.34);
    game.player.setPosition({ x: 0, y: 3.4, z: -8 });
    return {
      bottomY,
      midY,
      topY,
      upperY,
      playerY: game.player.position.y,
      cameraY: game.camera.position.y,
      firstFloorCeilingExists: Boolean(game.scene.getObjectByName("first-floor-panel-ceiling")),
      upperStairGuardExists: Boolean(upperStairGuard),
      secondLandingPanelType: secondLandingPanel?.geometry?.type || "",
      secondStairTopPanelType: secondStairTopPanel?.geometry?.type || "",
      stairWidth: stairRamp.maxX - stairRamp.minX,
      westClearance: stairRamp.minX - (-11.0),
      eastClearance: -2.6 - stairRamp.maxX,
      stairDoorExists: Boolean(stairDoor),
      hasStairDoorFrame: Boolean(stairDoorFrame),
      hasBottomLanding: Boolean(landing),
      landingMaxX: landing ? landing.position.x + landingWidth / 2 : null,
      stairPosts,
      wallHandrails,
      ceilingPieces,
      hasLowerOpenGuard: Boolean(lowerInnerGuard),
      openCorridorEntryClear,
      lowerStairSideClear,
      stairTopClear,
      turnIntoUpperCorridorClear,
    };
  });
  assert(Math.abs(floorState.bottomY) <= 0.05, `expected stair bottom near 0, got ${floorState.bottomY}`);
  assert(floorState.midY > 1.2 && floorState.midY < 2.4, `expected stair midpoint height, got ${floorState.midY}`);
  assert(Math.abs(floorState.topY - 3.4) <= 0.08, `expected stair top near 3.4, got ${floorState.topY}`);
  assert(Math.abs(floorState.upperY - 3.4) <= 0.03, `expected upper floor 3.4, got ${floorState.upperY}`);
  assert(Math.abs(floorState.playerY - 3.4) <= 0.03, `expected player set on second floor, got ${floorState.playerY}`);
  assert(Math.abs(floorState.cameraY - 5.08) <= 0.05, `expected second floor camera height, got ${floorState.cameraY}`);
  assert(!floorState.firstFloorCeilingExists, "expected first floor to use segmented ceiling slabs rather than one unbroken plane");
  assert(floorState.ceilingPieces >= 4, `expected segmented first-floor ceiling pieces around stair opening, got ${floorState.ceilingPieces}`);
  assert(!floorState.upperStairGuardExists, "expected upper stair transition to be free of small guard walls");
  assert(floorState.secondLandingPanelType === "BoxGeometry", `expected second-floor landing slab to be thick box geometry, got ${floorState.secondLandingPanelType}`);
  assert(floorState.secondStairTopPanelType === "BoxGeometry", `expected stair top to have a thick transition slab, got ${floorState.secondStairTopPanelType}`);
  assert(!floorState.stairDoorExists, "expected stair to be open from corridor without a stairwell door");
  assert(floorState.stairWidth >= 4.2, `expected natural stair width, got ${floorState.stairWidth}`);
  assert(floorState.westClearance >= 1.0, `expected stair clear of west wall, got ${floorState.westClearance}`);
  assert(floorState.eastClearance >= 1.0, `expected stair clear of east wall, got ${floorState.eastClearance}`);
  assert(!floorState.hasStairDoorFrame, "expected no stairwell door frame in corridor stair layout");
  assert(floorState.hasBottomLanding, "expected bottom landing between corridor and stairs");
  assert(floorState.landingMaxX >= -2.4, `expected bottom landing to meet the corridor opening, got maxX ${floorState.landingMaxX}`);
  assert(floorState.openCorridorEntryClear, "expected corridor entry into stair landing to be clear");
  assert(!floorState.hasLowerOpenGuard, "expected lower stair side to be free of small guard walls");
  assert(floorState.lowerStairSideClear, "expected stair side opening to stay clear instead of blocked by a short wall");
  assert(floorState.stairTopClear, "expected top stair landing to be clear");
  assert(floorState.turnIntoUpperCorridorClear, "expected turn from stair top into upper corridor to be clear");
  assert(floorState.stairPosts >= 6, `expected open stair railing posts instead of blocky guard walls, got ${floorState.stairPosts}`);
  assert(floorState.wallHandrails >= 2, `expected wall-mounted handrails, got ${floorState.wallHandrails}`);

  const floorValidationState = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();

    const sameXzStart = { x: 0, y: 3.4, z: -28 };
    const sameXzCandidate = { x: 0, y: 3.4, z: -30 };
    const sameXzBelow = game.collisionWorld.getBelowLandingInfo(sameXzStart, 2);
    const sameXzResult = game.collisionWorld.resolveActorPosition(
      sameXzStart,
      sameXzCandidate,
      0.34,
      { actorId: "validation-same-xz" },
    );

    const voidStart = { x: -8.4, y: 3.4, z: -4 };
    const voidCandidate = { x: -8.4, y: 3.4, z: -9 };
    const voidBelow = game.collisionWorld.getBelowLandingInfo(voidStart, 2);
    const voidResult = game.collisionWorld.resolveActorPosition(
      voidStart,
      voidCandidate,
      0.34,
      { actorId: "validation-void" },
    );

    const dropStart = { x: -10.3, y: 3.4, z: 14.2 };
    const dropCandidate = { x: -10.7, y: 3.4, z: 14.2 };
    const dropResult = game.collisionWorld.resolveActorPosition(
      dropStart,
      dropCandidate,
      0.34,
      { actorId: "validation-drop" },
    );
    const debug = game.collisionWorld.getDebugState({ x: 0, y: 3.4, z: -8 });

    return {
      sameXzBelowValid: sameXzBelow.valid,
      sameXzAllowed: sameXzResult.allowed,
      sameXzPosition: sameXzCandidate,
      voidBelowValid: voidBelow.valid,
      voidAllowed: voidResult.allowed,
      voidPosition: voidCandidate,
      dropAllowed: dropResult.allowed,
      dropPosition: dropCandidate,
      dropAttempt: game.collisionWorld.lastDropAttempt,
      debug,
    };
  });
  assert(floorValidationState.sameXzBelowValid, "expected same X/Z below right room to be walkable on 1F for validation");
  assert(!floorValidationState.sameXzAllowed, "expected non-drop-zone floor transition to be cancelled even when below X/Z is walkable");
  assert(Math.abs(floorValidationState.sameXzPosition.y - 3.4) <= 0.01, `expected cancelled same-XZ transition to stay on 2F, got y ${floorValidationState.sameXzPosition.y}`);
  assert(!floorValidationState.voidBelowValid, "expected left 2F room below to be void/non-walkable on 1F");
  assert(!floorValidationState.voidAllowed, "expected invalid void landing transition to be cancelled");
  assert(Math.abs(floorValidationState.voidPosition.y - 3.4) <= 0.01, `expected cancelled void transition to stay on 2F, got y ${floorValidationState.voidPosition.y}`);
  assert(floorValidationState.dropAllowed, "expected explicit drop zone to allow validated landing");
  assert(Math.abs(floorValidationState.dropPosition.y) <= 0.01, `expected explicit drop to land on 1F, got y ${floorValidationState.dropPosition.y}`);
  assert(floorValidationState.dropAttempt?.targetLandingId === "stair-landing_1f", "expected drop debug to record target landing id");
  assert(floorValidationState.debug.tileType === "walkable", `expected debug tile type walkable, got ${floorValidationState.debug.tileType}`);
  assert(typeof floorValidationState.debug.belowValidLanding === "boolean", "expected debug to expose below valid landing flag");

  const secondFloorStructureState = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    const nurseryDoor = game.doors.find((door) => door.id === "door-upper-nursery");
    const mirrorDoor = game.doors.find((door) => door.id === "door-upper-mirror");
    const lockedDoor = game.doors.find((door) => door.id === "door-upper-locked-records");
    nurseryDoor.isOpen = true;
    nurseryDoor.openAmount = 1;
    mirrorDoor.isOpen = true;
    mirrorDoor.openAmount = 1;
    lockedDoor.interact(game.createInteractionContext());

    const nurseryPath = game.collisionWorld.findPath(
      { x: 0, y: 3.4, z: -4 },
      { x: -8.2, y: 3.4, z: -4.2 },
      0.34,
    );
    const mirrorPath = game.collisionWorld.findPath(
      { x: 0, y: 3.4, z: -16 },
      { x: 8.2, y: 3.4, z: -16.2 },
      0.34,
    );
    const stairPath = game.collisionWorld.findPath(
      { x: -7.1, y: 0, z: 20.15 },
      { x: 5.4, y: 3.4, z: 7 },
      0.34,
    );
    const debug = game.collisionWorld.getDebugState({ x: 5.4, y: 3.4, z: 7 });
    const roomConnections = game.doors
      .filter((door) => door.position.y > 3)
      .map((door) => door.getDebugInfo());

    return {
      nurseryConnected: nurseryDoor.connectedRoomId,
      mirrorConnected: mirrorDoor.connectedRoomId,
      lockedOpen: lockedDoor.isOpen,
      lockedBlocking: lockedDoor.isBlocking(),
      lockedConnectedRoom: lockedDoor.connectedRoomId,
      nurseryPathLength: nurseryPath.length,
      mirrorPathLength: mirrorPath.length,
      stairPathLength: stairPath.length,
      stairPathCrossesFloor: stairPath.some((point) => point.y > 3),
      nurseryDoorwayBlocked: game.collisionWorld.isCircleBlocked({ x: -2.42, y: 3.4, z: -4 }, 0.28, { includeDoors: false }),
      mirrorDoorwayBlocked: game.collisionWorld.isCircleBlocked({ x: 2.42, y: 3.4, z: -16 }, 0.28, { includeDoors: false }),
      alcoveSurface: game.collisionWorld.getSurfaceAt({ x: 5.4, y: 3.4, z: 7 }, { preferredFloor: 2 }),
      barricadeExists: Boolean(game.scene.getObjectByName("upper-corridor-barricade")),
      brokenDeskExists: Boolean(game.scene.getObjectByName("upper-records-overturned-desk")),
      debugAreaCounts: debug.areaCounts,
      debugWaypointCount: debug.transitionWaypoints.length,
      roomConnections,
    };
  });
  assert(secondFloorStructureState.nurseryConnected === "upper-nursery-room", "expected nursery door connected to upper nursery room");
  assert(secondFloorStructureState.mirrorConnected === "upper-mirror-room", "expected mirror door connected to upper mirror room");
  assert(!secondFloorStructureState.lockedOpen, "expected locked 2F door not to open");
  assert(secondFloorStructureState.lockedBlocking, "expected locked 2F door to remain blocking");
  assert(secondFloorStructureState.lockedConnectedRoom === null, "expected locked door without room to be explicitly non-connected");
  assert(secondFloorStructureState.nurseryPathLength > 2, `expected path into nursery room, got ${secondFloorStructureState.nurseryPathLength}`);
  assert(secondFloorStructureState.mirrorPathLength > 2, `expected path into mirror room, got ${secondFloorStructureState.mirrorPathLength}`);
  assert(secondFloorStructureState.stairPathLength > 3, `expected stair path to second floor alcove, got ${secondFloorStructureState.stairPathLength}`);
  assert(secondFloorStructureState.stairPathCrossesFloor, "expected stair path to include second-floor y values");
  assert(!secondFloorStructureState.nurseryDoorwayBlocked, "expected nursery doorway static wall opening to be clear");
  assert(!secondFloorStructureState.mirrorDoorwayBlocked, "expected mirror doorway static wall opening to be clear");
  assert(secondFloorStructureState.alcoveSurface.walkable, `expected 2F records alcove walkable, got ${secondFloorStructureState.alcoveSurface.type}`);
  assert(secondFloorStructureState.barricadeExists, "expected visible 2F barricade object");
  assert(secondFloorStructureState.brokenDeskExists, "expected visible 2F horror object in records alcove");
  assert(secondFloorStructureState.debugAreaCounts?.blocked >= 3, "expected debug HUD data to count blocked areas");
  assert(secondFloorStructureState.debugWaypointCount >= 2, "expected debug HUD data to include stair waypoints");

  const stairClimbState = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    const player = game.player;
    player.setPosition({ x: -7.1, y: 0, z: 19.95 });
    player.yaw = 0;
    player.pitch = 0;

    const cameraDeltas = [];
    let previousCameraY = game.camera.position.y;
    game.input.keys.add("w");
    for (let i = 0; i < 195; i += 1) {
      player.update(1 / 60);
      cameraDeltas.push(Math.abs(game.camera.position.y - previousCameraY));
      previousCameraY = game.camera.position.y;
    }
    game.input.keys.delete("w");
    game.input.keys.add("d");
    for (let i = 0; i < 150; i += 1) {
      player.update(1 / 60);
      cameraDeltas.push(Math.abs(game.camera.position.y - previousCameraY));
      previousCameraY = game.camera.position.y;
    }
    game.input.keys.delete("d");

    return {
      x: player.position.x,
      y: player.position.y,
      z: player.position.z,
      cameraY: game.camera.position.y,
      maxCameraDelta: Math.max(...cameraDeltas),
      blockedAtEnd: game.collisionWorld.isCircleBlocked(player.position, player.constructor.PLAYER_RADIUS ?? 0.34),
      topTurnClear: !game.collisionWorld.isCircleBlocked({ x: -4.4, y: 3.4, z: player.position.z }, player.constructor.PLAYER_RADIUS ?? 0.34),
    };
  });
  assert(stairClimbState.y > 3.2, `expected stair climb to reach second floor, got y ${stairClimbState.y}`);
  assert(stairClimbState.x > -2.2, `expected player to turn naturally into second-floor corridor, got x ${stairClimbState.x}`);
  assert(stairClimbState.z >= 8.8 && stairClimbState.z <= 13, `expected stair top to feed into upper landing, got z ${stairClimbState.z}`);
  assert(stairClimbState.maxCameraDelta < 0.065, `expected smoothed stair camera, max frame delta ${stairClimbState.maxCameraDelta}`);
  assert(stairClimbState.topTurnClear, "expected stair top turn into corridor to remain clear");

  const stealthAndFloorState = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");

    uncat.group.position.set(0, 0, 0);
    uncat.group.rotation.y = 0;
    game.player.position.set(0, 0, -2);
    uncat.state = "patrol";
    uncat.memoryTimer = 0;
    uncat.lastKnownPlayerPosition = null;
    uncat.update(1 / 60, {
      position: game.player.position,
      isHidden: false,
      isMoving: true,
      isSprinting: false,
    });
    const walkingBehind = {
      state: uncat.state,
      caught: uncat.caughtPlayer,
    };

    uncat.group.position.set(0, 0, 0);
    uncat.group.rotation.y = 0;
    uncat.state = "patrol";
    uncat.memoryTimer = 0;
    uncat.lastKnownPlayerPosition = null;
    game.player.position.set(0, 0, -2);
    uncat.update(1 / 60, {
      position: game.player.position,
      isHidden: false,
      isMoving: true,
      isSprinting: true,
    });
    const sprintingBehind = {
      state: uncat.state,
      caught: uncat.caughtPlayer,
    };

    uncat.group.position.set(0, 0, -8);
    uncat.group.rotation.y = 0;
    uncat.state = "patrol";
    uncat.memoryTimer = 0;
    uncat.lastKnownPlayerPosition = null;
    game.player.setPosition({ x: 0, y: 3.4, z: -8 });
    uncat.update(1 / 60, {
      position: game.player.position,
      isHidden: false,
      isMoving: true,
      isSprinting: true,
    });
    const verticalOverlap = {
      enemyY: uncat.group.position.y,
      playerY: game.player.position.y,
      state: uncat.state,
      caught: uncat.caughtPlayer,
      threat: uncat.getThreatAmount(game.player.position),
    };

    return { walkingBehind, sprintingBehind, verticalOverlap };
  });
  assert(stealthAndFloorState.walkingBehind.state === "patrol", `expected walking behind monster to stay hidden, got ${stealthAndFloorState.walkingBehind.state}`);
  assert(!stealthAndFloorState.walkingBehind.caught, "expected walking behind monster not to be caught");
  assert(stealthAndFloorState.sprintingBehind.state === "chase", `expected sprinting behind monster to trigger chase, got ${stealthAndFloorState.sprintingBehind.state}`);
  assert(!stealthAndFloorState.verticalOverlap.caught, "expected different floors not to trigger catch even on same XZ");
  assert(stealthAndFloorState.verticalOverlap.state === "patrol", `expected different floors not to trigger chase, got ${stealthAndFloorState.verticalOverlap.state}`);
  assert(stealthAndFloorState.verticalOverlap.threat === 0, `expected different floors to have zero threat, got ${stealthAndFloorState.verticalOverlap.threat}`);

  const interFloorChaseState = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
    const cyclopse = game.enemyManager.enemies.find((enemy) => enemy.config.id === "cyclopse");
    game.player.setPosition({ x: 5.4, y: 3.4, z: 7 });
    uncat.group.position.set(-7.1, 0, 20.15);
    uncat.state = "chase";
    uncat.memoryTimer = uncat.config.memorySeconds;
    uncat.lastKnownPlayerPosition = game.player.position.clone();
    const initialPath = game.collisionWorld.findPath(uncat.group.position, game.player.position, uncat.config.radius);
    for (let i = 0; i < 520; i += 1) {
      uncat.update(1 / 60, {
        position: game.player.position,
        isHidden: false,
        isMoving: true,
        isSprinting: true,
      });
    }

    cyclopse.group.position.set(-7.1, 0, 20.15);
    cyclopse.state = "chase";
    cyclopse.memoryTimer = cyclopse.config.memorySeconds;
    cyclopse.lastKnownPlayerPosition = game.player.position.clone();
    for (let i = 0; i < 520; i += 1) {
      cyclopse.update(1 / 60, {
        position: game.player.position,
        isHidden: false,
        isMoving: true,
        isSprinting: true,
      });
    }

    return {
      initialPathLength: initialPath.length,
      initialPathHasStairY: initialPath.some((point) => point.y > 1 && point.y < 3.5),
      uncatState: uncat.state,
      uncatY: uncat.group.position.y,
      uncatFloor: game.collisionWorld.getSurfaceAt(uncat.group.position, { allowAnyFloor: true }).floor,
      uncatDistance: Math.hypot(uncat.group.position.x - game.player.position.x, uncat.group.position.z - game.player.position.z),
      uncatStuckTimer: uncat.stuckTimer,
      cyclopseState: cyclopse.state,
      cyclopseY: cyclopse.group.position.y,
      cyclopseFloor: game.collisionWorld.getSurfaceAt(cyclopse.group.position, { allowAnyFloor: true }).floor,
      cyclopseStuckTimer: cyclopse.stuckTimer,
      debug: uncat.getDebugState(),
    };
  });
  assert(interFloorChaseState.initialPathLength > 3, `expected cross-floor path, got ${interFloorChaseState.initialPathLength}`);
  assert(interFloorChaseState.initialPathHasStairY, "expected cross-floor path to include stair/ramp heights");
  assert(interFloorChaseState.uncatState === "chase", `expected Uncat to keep chasing across floors, got ${interFloorChaseState.uncatState}`);
  assert(interFloorChaseState.uncatY > 3.0, `expected Uncat to climb to 2F, got y ${interFloorChaseState.uncatY}`);
  assert(interFloorChaseState.uncatFloor === 2, `expected Uncat floor 2 after stair chase, got ${interFloorChaseState.uncatFloor}`);
  assert(interFloorChaseState.uncatDistance < 5.0, `expected Uncat to close distance on 2F, got ${interFloorChaseState.uncatDistance}`);
  assert(interFloorChaseState.uncatStuckTimer < 0.5, `expected Uncat not stuck after stair chase, got ${interFloorChaseState.uncatStuckTimer}`);
  assert(interFloorChaseState.cyclopseState === "chase", `expected Cyclopse to keep chasing across floors, got ${interFloorChaseState.cyclopseState}`);
  assert(interFloorChaseState.cyclopseY > 3.0, `expected Cyclopse to climb to 2F, got y ${interFloorChaseState.cyclopseY}`);
  assert(interFloorChaseState.cyclopseFloor === 2, `expected Cyclopse floor 2 after stair chase, got ${interFloorChaseState.cyclopseFloor}`);
  assert(interFloorChaseState.cyclopseStuckTimer < 0.5, `expected Cyclopse not stuck after stair chase, got ${interFloorChaseState.cyclopseStuckTimer}`);
  assert(interFloorChaseState.debug.pathTarget, "expected enemy debug state to expose path target");

  const cabinetExitState = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    const cabinet = game.cabinets[0];
    game.player.position.copy(cabinet.getGuardPosition());
    game.enterCabinet(cabinet);
    game.exitCabinet();
    const forward = cabinet.getForwardDirection();
    const cameraForward = { x: -Math.sin(game.player.yaw), z: -Math.cos(game.player.yaw) };
    const toCabinetRaw = {
      x: cabinet.position.x - game.player.position.x,
      z: cabinet.position.z - game.player.position.z,
    };
    const toCabinetLength = Math.hypot(toCabinetRaw.x, toCabinetRaw.z) || 1;
    const toCabinet = {
      x: toCabinetRaw.x / toCabinetLength,
      z: toCabinetRaw.z / toCabinetLength,
    };
    return {
      hidden: game.player.isHidden,
      dotForward: cameraForward.x * forward.x + cameraForward.z * forward.z,
      dotToCabinet: cameraForward.x * toCabinet.x + cameraForward.z * toCabinet.z,
      pitch: game.player.pitch,
    };
  });
  assert(!cabinetExitState.hidden, "expected player outside cabinet after exit");
  assert(cabinetExitState.dotForward > 0.96, `expected exit camera to face away from cabinet, dot ${cabinetExitState.dotForward}`);
  assert(cabinetExitState.dotToCabinet < -0.75, `expected exit camera not to look back at cabinet, dot ${cabinetExitState.dotToCabinet}`);
  assert(Math.abs(cabinetExitState.pitch) <= 0.001, `expected level pitch after cabinet exit, got ${cabinetExitState.pitch}`);

  const patrolState = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
    const guardBlocked = game.cabinets.some((cabinet) => (
      game.collisionWorld.isCircleBlocked(cabinet.getGuardPosition(), uncat.config.radius)
    ));
    uncat.group.position.set(-13.0, 0, -13.7);
    uncat.endCabinetInvestigation();
    for (let i = 0; i < 260; i += 1) {
      uncat.update(1 / 60, { position: game.player.position, isHidden: true });
    }
    const longPatrolStart = uncat.group.position.clone();
    for (let i = 0; i < 700; i += 1) {
      uncat.update(1 / 60, { position: game.player.position, isHidden: true });
    }
    const surfaceAfterLongPatrol = game.collisionWorld.getSurfaceAt(uncat.group.position, { allowAnyFloor: true });
    return {
      guardBlocked,
      x: uncat.group.position.x,
      z: uncat.group.position.z,
      state: uncat.state,
      blocked: game.collisionWorld.isCircleBlocked(uncat.group.position, uncat.config.radius),
      waypointXMax: Math.max(...uncat.config.waypoints.map((point) => Math.abs(point[0]))),
      waypointFloors: [...new Set(uncat.config.waypoints.map((point) => point[1] > 1.5 ? 2 : 1))],
      floorPatrols: Object.keys(uncat.config.patrolWaypointsByFloor || {}),
      activePatrolFloor: surfaceAfterLongPatrol.floor,
      teleportedToStair: Math.abs(uncat.group.position.x + 7.1) < 0.8 && Math.abs(uncat.group.position.z - 20.15) < 1.2,
      movedDuringLongPatrol: Math.hypot(uncat.group.position.x - longPatrolStart.x, uncat.group.position.z - longPatrolStart.z),
    };
  });
  assert(!patrolState.guardBlocked, "expected cabinet guard positions to be clear");
  assert(patrolState.state === "patrol", `expected patrol after cabinet recovery, got ${patrolState.state}`);
  assert(!patrolState.blocked, "expected recovering enemy not to be inside a wall");
  assert(patrolState.waypointXMax <= 1.2, `expected base Uncat patrol waypoints to stay 1F corridor-focused, got max x ${patrolState.waypointXMax}`);
  assert(patrolState.waypointFloors.length === 1 && patrolState.waypointFloors[0] === 1, "expected base Uncat waypoints not to include stair/2F transitions");
  assert(patrolState.floorPatrols.includes("1") && patrolState.floorPatrols.includes("2"), "expected floor-local patrol sets for 1F and 2F");
  assert(patrolState.activePatrolFloor === 1, `expected normal 1F patrol not to climb stairs by itself, got floor ${patrolState.activePatrolFloor}`);
  assert(!patrolState.teleportedToStair, "expected normal patrol not to pop to stair entry");
  assert(patrolState.movedDuringLongPatrol > 1, "expected normal patrol to keep walking corridors");

  const doorChaseState = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    game.isStarted = true;
    const uncat = game.enemyManager.enemies.find((enemy) => enemy.config.id === "uncat");
    const door = game.doors.find((entry) => entry.id === "door-left-workshop");
    door.isOpen = false;
    door.openAmount = 0;
    uncat.group.position.set(-10.8, 0, -16);
    game.player.position.set(0.2, 0, -16);
    uncat.state = "chase";
    uncat.memoryTimer = uncat.config.memorySeconds;
    uncat.lastKnownPlayerPosition = game.player.position.clone();

    for (let i = 0; i < 360; i += 1) {
      game.update(1 / 60);
    }

    return {
      doorOpen: door.isOpen,
      uncatX: uncat.group.position.x,
      distance: Math.hypot(uncat.group.position.x - game.player.position.x, uncat.group.position.z - game.player.position.z),
      pathLength: uncat.chasePath.length,
    };
  });
  assert(doorChaseState.doorOpen, "expected Uncat to open the workshop door while chasing");
  assert(doorChaseState.uncatX > -3.2, `expected Uncat to move through the door instead of sticking to wall, got x ${doorChaseState.uncatX}`);
  assert(doorChaseState.distance < 4.5, `expected Uncat to close distance through door, got ${doorChaseState.distance}`);

  const pauseState = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", code: "Escape" }));
    game.update(1 / 60);
    const paused = {
      isPaused: game.isPaused,
      pauseVisible: !document.querySelector("#pause-screen")?.classList.contains("hidden"),
    };

    const sensitivityInput = document.querySelector("#mouse-sensitivity");
    sensitivityInput.value = "1.5";
    sensitivityInput.dispatchEvent(new Event("input", { bubbles: true }));
    const sensitivityScale = game.player.mouseSensitivity / 0.0022;

    window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", code: "Escape" }));
    game.update(1 / 60);
    const resumed = {
      isPaused: game.isPaused,
      pauseVisible: !document.querySelector("#pause-screen")?.classList.contains("hidden"),
    };

    game.pause();
    document.querySelector("#quit-button").click();
    const quit = {
      isStarted: game.isStarted,
      isPaused: game.isPaused,
      startVisible: !document.querySelector("#start-screen")?.classList.contains("hidden"),
      pauseVisible: !document.querySelector("#pause-screen")?.classList.contains("hidden"),
    };

    return { paused, sensitivityScale, resumed, quit };
  });
  assert(pauseState.paused.isPaused, "expected Escape to pause the game");
  assert(pauseState.paused.pauseVisible, "expected pause menu to be visible");
  assert(Math.abs(pauseState.sensitivityScale - 1.5) <= 0.01, `expected sensitivity scale 1.5, got ${pauseState.sensitivityScale}`);
  assert(!pauseState.resumed.isPaused, "expected second Escape to resume the game");
  assert(!pauseState.resumed.pauseVisible, "expected pause menu to hide after resume");
  assert(!pauseState.quit.isStarted, "expected quit button to return to title");
  assert(!pauseState.quit.isPaused, "expected quit button to leave pause state");
  assert(pauseState.quit.startVisible, "expected title screen visible after quit");
  assert(!pauseState.quit.pauseVisible, "expected pause screen hidden after quit");

  const keyAndClearState = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    game.tryClearFinal();
    const blockedText = document.querySelector("#status-line")?.textContent || "";
    for (const key of game.keys) {
      game.collectKey(key);
    }
    game.tryClearFinal();
    return {
      blockedText,
      keyCount: game.keyCount,
      cleared: game.gameCleared,
      clearVisible: !document.querySelector("#clear-screen")?.classList.contains("hidden"),
    };
  });
  assert(keyAndClearState.blockedText.includes("부족"), "expected final exit to block without 3 keys");
  assert(keyAndClearState.keyCount === 3, `expected 3 collected keys, got ${keyAndClearState.keyCount}`);
  assert(keyAndClearState.cleared, "expected gameCleared after handing in 3 keys");
  assert(keyAndClearState.clearVisible, "expected clear overlay to be visible");

  const cabinetSafeState = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    const cabinet = game.cabinets[0];
    const enemy = game.enemyManager.enemies[0];
    const guard = cabinet.getGuardPosition();
    enemy.state = "chase";
    enemy.group.position.copy(guard);
    game.enterCabinet(cabinet, { forceOutcome: "safe" });
    for (let i = 0; i < 90; i += 1) {
      game.updateCabinetEvent(1 / 60);
      enemy.update(1 / 60, { position: game.player.position, isHidden: true });
    }
    const waitingState = {
      enemyAction: enemy.currentActionName,
      enemyIdle: enemy.isIdlePose,
      enemyStateWhileWaiting: enemy.state,
    };
    for (let i = 0; i < 330; i += 1) {
      game.updateCabinetEvent(1 / 60);
    }
    const stateBeforeExit = {
      hidden: game.player.isHidden,
      eventCleared: !game.cabinetEvent,
      enemyState: enemy.state,
      cabinetOccupied: cabinet.occupied,
      ...waitingState,
    };
    game.exitCabinet();
    return {
      ...stateBeforeExit,
      hiddenAfterExit: game.player.isHidden,
      cabinetOccupiedAfterExit: cabinet.occupied,
    };
  });
  assert(cabinetSafeState.hidden, "expected player hidden during safe cabinet event");
  assert(cabinetSafeState.eventCleared, "expected safe cabinet event to clear after 5 seconds");
  assert(cabinetSafeState.enemyState === "patrol", `expected enemy patrol after safe cabinet event, got ${cabinetSafeState.enemyState}`);
  assert(cabinetSafeState.cabinetOccupied, "expected cabinet occupied while hidden");
  assert(cabinetSafeState.enemyAction === "idlePose", `expected enemy to hold idle pose at cabinet, got ${cabinetSafeState.enemyAction}`);
  assert(cabinetSafeState.enemyIdle, "expected enemy idle pose flag while waiting at cabinet");
  assert(
    cabinetSafeState.enemyStateWhileWaiting === "investigateCabinet",
    `expected enemy investigating cabinet while waiting, got ${cabinetSafeState.enemyStateWhileWaiting}`,
  );
  assert(!cabinetSafeState.hiddenAfterExit, "expected player to exit cabinet after safe event");
  assert(!cabinetSafeState.cabinetOccupiedAfterExit, "expected cabinet to free after exit");

  const cabinetCaughtState = await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    const cabinet = game.cabinets[1];
    const enemy = game.enemyManager.enemies[0];
    enemy.state = "chase";
    enemy.group.position.copy(cabinet.getGuardPosition());
    game.enterCabinet(cabinet, { forceOutcome: "caught" });
    for (let i = 0; i < 90; i += 1) {
      game.updateCabinetEvent(1 / 60);
    }
    return {
      gameOver: game.gameOver,
      caughtVisible: !document.querySelector("#caught-screen")?.classList.contains("hidden"),
      caughtTitle: document.querySelector("#caught-screen h2")?.textContent || "",
    };
  });
  assert(cabinetCaughtState.gameOver, "expected forced caught cabinet outcome to end game");
  assert(cabinetCaughtState.caughtVisible, "expected caught overlay to be visible");
  assert(cabinetCaughtState.caughtTitle.includes("캐비넷"), "expected cabinet death message");

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    game.hud.hideStart();
    game.hud.hidePause();
    game.hud.hideCaught();
    game.hud.hideClear();
    const cyclopse = game.enemyManager.enemies.find((enemy) => enemy.config.id === "cyclopse");
    cyclopse.group.position.set(7.3, 0, -10.2);
    game.player.position.set(7.3, 0, -3.2);
    game.player.yaw = 0;
    game.player.pitch = 0.12;
    cyclopse.state = "chase";
    cyclopse.memoryTimer = cyclopse.config.memorySeconds;
    cyclopse.lastKnownPlayerPosition = game.player.position.clone();
    for (let i = 0; i < 90; i += 1) {
      cyclopse.update(1 / 60, { position: game.player.position, isHidden: false });
    }
    cyclopse.group.position.set(7.3, 0, -8.2);
    cyclopse.snapModelToGround(false);
    game.isStarted = false;
    game.gameOver = false;
    game.gameCleared = false;
    game.hud.hideCaught();
    game.hud.hideStart();
    game.player.update(0);
    game.renderer.render(game.scene, game.camera);
  });
  await page.screenshot({ path: "E:/AI/3d_modeling/game/happy_toy/verification/cyclopse-grounded-check.png", fullPage: false });

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    game.hud.hideStart();
    game.hud.hidePause();
    game.hud.hideCaught();
    game.hud.hideClear();
    game.flashlightController.enabled = true;
    game.flashlight.visible = true;
    game.hud.setFlashlightEnabled(true);
    game.player.setPosition({ x: -6.1, y: 3.4, z: -5.8 });
    game.player.yaw = Math.PI / 2;
    game.player.pitch = -0.04;
    game.player.update(0);
    game.renderer.render(game.scene, game.camera);
  });
  await page.screenshot({ path: "E:/AI/3d_modeling/game/happy_toy/verification/second-floor-check.png", fullPage: false });

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.restart();
    game.hud.hideStart();
    game.hud.hidePause();
    game.hud.hideCaught();
    game.hud.hideClear();
    game.flashlightController.enabled = true;
    game.flashlight.visible = true;
    game.hud.setFlashlightEnabled(true);
    game.player.setPosition({ x: -1.2, y: 0, z: 21.9 });
    game.player.yaw = Math.PI * 0.32;
    game.player.pitch = -0.06;
    game.player.update(0);
    game.renderer.render(game.scene, game.camera);
  });
  await page.screenshot({ path: "E:/AI/3d_modeling/game/happy_toy/verification/stairwell-check.png", fullPage: false });

  await page.evaluate(() => {
    const game = window.__happyToy;
    game.player.setPosition({ x: -5.6, y: 3.4, z: 12.3 });
    game.player.yaw = Math.PI;
    game.player.pitch = -0.45;
    game.player.update(0);
    game.renderer.render(game.scene, game.camera);
  });
  await page.screenshot({ path: "E:/AI/3d_modeling/game/happy_toy/verification/happy-toy-expanded-map.png", fullPage: false });

  if (browserErrors.length > 0) {
    throw new Error(`browser console errors: ${browserErrors.join(" | ")}`);
  }

  console.log(JSON.stringify({
    ok: true,
    initialState,
    giveUpState,
    cyclopseGroundState,
    floorState,
    floorValidationState,
    secondFloorStructureState,
    stairClimbState,
    stealthAndFloorState,
    interFloorChaseState,
    cabinetExitState,
    patrolState,
    doorChaseState,
    pauseState,
    keyAndClearState,
    cabinetSafeState,
    cabinetCaughtState,
  }, null, 2));
} finally {
  await browser.close();
}

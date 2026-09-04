import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = process.argv[2] || "http://127.0.0.1:8010/";

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

console.log("Starting verification for Backrooms procedural game on url:", url);

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
  await page.goto(url, { waitUntil: "networkidle", timeout: 5000 });
  
  // Wait for game initialization
  await page.waitForFunction(
    () => window.__happyToy?.assetsReady === true,
    null,
    { timeout: 5000 },
  );

  console.log("Game loaded successfully. Testing state...");

  const state = await page.evaluate(() => {
    const game = window.__happyToy;
    const map = game.mapBuilder;
    
    return {
      loadedChunksCount: map.loadedChunks.size,
      hasStartChunk: map.loadedChunks.has("0,0"),
      hasFinalExit: Boolean(game.finalExit),
      playerStartPos: [game.player.position.x, game.player.position.y, game.player.position.z],
      doorCount: game.doors.length,
      keyCount: game.keys.length,
      cabinetCount: game.cabinets.length,
      safeLightCount: game.safeLights.length,
      safeLightPrompt: game.safeLights[0]?.getPrompt?.() ?? "",
      safeLightPoolSize: game._safeLightPool?.length ?? 0,
      activeSafeLightPoolCount: game._safeLightPool?.filter((light) => light.intensity > 0).length ?? 0,
      safeLightVariantCount: new Set(game.safeLights.map((light) => light.variant)).size,
      safeLightBasicMaterialCount: (() => {
        let count = 0;
        for (const safeLight of game.safeLights) {
          safeLight.group.traverse((child) => {
            if (!child.isMesh) return;
            const materials = Array.isArray(child.material) ? child.material : [child.material];
            count += materials.filter((material) => material?.isMeshBasicMaterial).length;
          });
        }
        return count;
      })(),
      ambientIntensity: game.scene.children.find(c => c.isAmbientLight)?.intensity ?? 0,
      fogFar: game.scene.fog?.far ?? 0,
      shadowsEnabled: game.renderer.shadowMap.enabled,
      flashlightCastsShadow: game.flashlight.castShadow,
      flashlightShadowSize: game.flashlight.shadow.mapSize.width,
      toneMappingExposure: game.renderer.toneMappingExposure,
      basicMaterialCount: (() => {
        let count = 0;
        game.scene.traverse((child) => {
          if (!child.isMesh) return;
          const materials = Array.isArray(child.material) ? child.material : [child.material];
          count += materials.filter((material) => material?.isMeshBasicMaterial).length;
        });
        return count;
      })(),
      enemyMeshStats: game.enemyManager.enemies.map((enemy) => {
        const stats = {
          id: enemy.config.id,
          meshCount: 0,
          standardMaterialCount: 0,
          missingNormalCount: 0,
          shadowCasterCount: 0,
          shadowReceiverCount: 0,
          emissiveMaterialCount: 0,
        };
        enemy.group.traverse((child) => {
          if (!child.isMesh && !child.isSkinnedMesh) return;
          stats.meshCount += 1;
          const geometry = child.geometry;
          if (!geometry?.attributes?.normal) {
            stats.missingNormalCount += 1;
          }
          if (child.castShadow) {
            stats.shadowCasterCount += 1;
          }
          if (child.receiveShadow) {
            stats.shadowReceiverCount += 1;
          }
          const materials = Array.isArray(child.material) ? child.material : [child.material];
          if (materials.every((material) => material?.isMeshStandardMaterial)) {
            stats.standardMaterialCount += 1;
          }
          if (materials.some((material) => material?.emissive?.getHex?.() > 0)) {
            stats.emissiveMaterialCount += 1;
          }
        });
        return stats;
      }),
      horrorPropStats: (() => {
        const kinds = new Set();
        let anchorCount = 0;
        let loadedCount = 0;
        let meshCount = 0;
        let basicMaterialCount = 0;
        game.scene.traverse((child) => {
          if (child.userData?.horrorProp) {
            anchorCount += 1;
            if (child.userData.horrorPropLoaded) {
              loadedCount += 1;
            }
            if (child.userData.propKind) {
              kinds.add(child.userData.propKind);
            }
          }
          if (!child.isMesh && !child.isSkinnedMesh) return;
          let belongsToHorrorProp = false;
          let parent = child.parent;
          while (parent) {
            if (parent.userData?.horrorProp) {
              belongsToHorrorProp = true;
              break;
            }
            parent = parent.parent;
          }
          if (!belongsToHorrorProp) return;
          meshCount += 1;
          const materials = Array.isArray(child.material) ? child.material : [child.material];
          basicMaterialCount += materials.filter((material) => material?.isMeshBasicMaterial).length;
        });
        return {
          anchorCount,
          loadedCount,
          meshCount,
          basicMaterialCount,
          kindCount: kinds.size,
          kinds: [...kinds].sort(),
        };
      })(),
      enemyCount: game.enemyManager.enemies.length,
    };
  });

  // 1. Initial State Assertions
  assert(state.loadedChunksCount === 49, `Expected 49 loaded chunks in 7x7 grid, got ${state.loadedChunksCount}`);
  assert(state.hasStartChunk, "Expected start chunk (0,0) to be loaded");
  assert(state.hasFinalExit, "Expected final exit toy box to spawn in start chunk");
  assert(state.playerStartPos[0] === 0 && state.playerStartPos[2] === 0, `Expected player spawn at (0, 0), got ${state.playerStartPos}`);
  assert(state.doorCount >= 8, `Expected at least 8 start room, quadrant, and stair doors, got ${state.doorCount}`);
  assert(state.safeLightCount >= 12, `Expected safe light objects across Backrooms chunks, got ${state.safeLightCount}`);
  assert(
    state.safeLightPrompt.includes("Press E to turn on light"),
    `Expected safe light prompt to advertise E interaction, got "${state.safeLightPrompt}"`,
  );
  assert(state.safeLightPoolSize === 8, `Expected fixed safe-light dynamic pool of 8, got ${state.safeLightPoolSize}`);
  assert(state.activeSafeLightPoolCount === 0, `Expected no safe-light PointLights active before interaction, got ${state.activeSafeLightPoolCount}`);
  assert(state.safeLightVariantCount >= 3, `Expected at least 3 safe-light variants, got ${state.safeLightVariantCount}`);
  assert(state.safeLightBasicMaterialCount === 0, `Expected safe lights to use lit materials, got ${state.safeLightBasicMaterialCount} basic materials`);
  assert(state.ambientIntensity >= 0.12 && state.ambientIntensity <= 0.22, `Expected flashlight-dependent ambient light in Backrooms, got ${state.ambientIntensity}`);
  assert(state.fogFar === 50, `Expected denser Backrooms fog far distance 50, got ${state.fogFar}`);
  assert(state.shadowsEnabled, "Expected renderer shadow map to be enabled");
  assert(state.flashlightCastsShadow, "Expected flashlight to cast shadows");
  assert(state.flashlightShadowSize === 512, `Expected 512px flashlight shadow map, got ${state.flashlightShadowSize}`);
  assert(state.toneMappingExposure === 0.8, `Expected ACES exposure 0.8, got ${state.toneMappingExposure}`);
  assert(state.basicMaterialCount === 0, `Expected no MeshBasicMaterial in lit scene, got ${state.basicMaterialCount}`);
  assert(state.enemyCount >= 2, `Expected at least 2 monsters initialized, got ${state.enemyCount}`);

  for (const enemyStats of state.enemyMeshStats) {
    assert(enemyStats.meshCount > 0, `Expected ${enemyStats.id} to have at least one render mesh`);
    assert(
      enemyStats.standardMaterialCount === enemyStats.meshCount,
      `Expected ${enemyStats.id} meshes to use MeshStandardMaterial, got ${enemyStats.standardMaterialCount}/${enemyStats.meshCount}`,
    );
    assert(
      enemyStats.missingNormalCount === 0,
      `Expected ${enemyStats.id} meshes to include normals for PBR lighting, got ${enemyStats.missingNormalCount} missing`,
    );
    assert(
      enemyStats.shadowCasterCount === enemyStats.meshCount,
      `Expected ${enemyStats.id} meshes to cast shadows, got ${enemyStats.shadowCasterCount}/${enemyStats.meshCount}`,
    );
    assert(
      enemyStats.shadowReceiverCount === enemyStats.meshCount,
      `Expected ${enemyStats.id} meshes to receive shadows, got ${enemyStats.shadowReceiverCount}/${enemyStats.meshCount}`,
    );
    assert(
      enemyStats.emissiveMaterialCount === 0,
      `Expected ${enemyStats.id} meshes to avoid emissive material, got ${enemyStats.emissiveMaterialCount}`,
    );
  }
  assert(state.horrorPropStats.anchorCount >= 20, `Expected many horror prop anchors in loaded chunks, got ${state.horrorPropStats.anchorCount}`);
  assert(
    state.horrorPropStats.loadedCount >= 20,
    `Expected horror prop assets to finish loading, got ${state.horrorPropStats.loadedCount}/${state.horrorPropStats.anchorCount}`,
  );
  assert(state.horrorPropStats.meshCount > 0, "Expected loaded horror props to contain render meshes");
  assert(
    state.horrorPropStats.basicMaterialCount === 0,
    `Expected horror props to use lit materials, got ${state.horrorPropStats.basicMaterialCount} basic materials`,
  );
  assert(state.horrorPropStats.kindCount >= 5, `Expected varied horror prop kinds, got ${state.horrorPropStats.kinds.join(", ")}`);

  const safeLightState = await page.evaluate(() => {
    const game = window.__happyToy;
    const safeLight = game.safeLights.find((light) => light.variant === "wall-switch")
      || game.safeLights.find(Boolean);
    if (!safeLight) {
      return { missing: true };
    }

    const facingX = -Math.sin(safeLight.yaw);
    const facingZ = -Math.cos(safeLight.yaw);
    const approach = {
      x: safeLight.position.x + facingX * 1.15,
      y: 0,
      z: safeLight.position.z + facingZ * 1.15,
    };
    game.player.setPosition(approach);
    game.player.setLookAt({
      x: safeLight.position.x,
      y: Math.max(1.0, safeLight.position.y),
      z: safeLight.position.z,
    });
    game.refreshInteractables();
    game.player.updateInteraction();

    const promptBefore = game.hud.promptElement?.textContent ?? "";
    const focusedKey = game.player.currentInteractable?.stateKey ?? null;
    game.input.pressedThisFrame.add("e");
    game.player.updateInteraction();
    game.updateSafeLightPool(game.player.position);

    const stateKey = safeLight.stateKey;
    const [sourceCx, sourceCz] = stateKey.split(":")[0].split(",").map(Number);
    const sourceChunkKey = `${sourceCx},${sourceCz}`;
    const poolActiveAfterOn = game._safeLightPool.filter((light) => light.intensity > 0).length;
    const poolLightRange = game._safeLightPool.find((light) => light.intensity > 0)?.distance ?? 0;

    const pump = (frames) => {
      for (let i = 0; i < frames; i += 1) {
        game.updateBackrooms(0.016);
      }
    };

    game.player.setPosition({ x: (sourceCx + 7) * 16, y: 0, z: (sourceCz + 7) * 16 });
    pump(80);
    const unloaded = !game.mapBuilder.loadedChunks.has(sourceChunkKey);

    game.player.setPosition({ x: sourceCx * 16, y: 0, z: sourceCz * 16 });
    pump(80);
    const restored = game.safeLights.find((light) => light.stateKey === stateKey);
    game.updateSafeLightPool(game.player.position);

    return {
      missing: false,
      stateKey,
      promptBefore,
      focusedKey,
      isOnAfterE: safeLight.isOn,
      activatedSetHas: game.activatedSafeLights.has(stateKey),
      poolActiveAfterOn,
      poolSize: game._safeLightPool.length,
      poolLightRange,
      unloaded,
      restoredOn: restored?.isOn ?? false,
      activeAfterReload: game._safeLightPool.filter((light) => light.intensity > 0).length,
    };
  });

  assert(!safeLightState.missing, "Expected at least one safe light to test interaction");
  assert(
    safeLightState.promptBefore.includes("Press E to turn on light"),
    `Expected safe-light prompt before activation, got "${safeLightState.promptBefore}"`,
  );
  assert(safeLightState.focusedKey === safeLightState.stateKey, "Expected player focus to target the nearby safe light");
  assert(safeLightState.isOnAfterE, "Expected pressing E near safe light to switch it on");
  assert(safeLightState.activatedSetHas, "Expected activatedSafeLights Set to remember switched-on light");
  assert(
    safeLightState.poolActiveAfterOn > 0 && safeLightState.poolActiveAfterOn <= safeLightState.poolSize,
    `Expected bounded active safe lights after activation, got ${safeLightState.poolActiveAfterOn}/${safeLightState.poolSize}`,
  );
  assert(safeLightState.poolSize === 8, `Expected safe-light dynamic pool to stay at 8, got ${safeLightState.poolSize}`);
  assert(safeLightState.poolLightRange <= 30, `Expected local safe light range <= 30, got ${safeLightState.poolLightRange}`);

  assert(safeLightState.unloaded, "Expected activated safe light chunk to unload when player moves far away");
  assert(safeLightState.restoredOn, "Expected safe light ON state to restore after chunk reload");
  assert(safeLightState.activeAfterReload <= 8, `Expected active dynamic safe lights to remain bounded, got ${safeLightState.activeAfterReload}`);
  
  console.log("Initial state passed! Testing chunk generation & key placement...");

  // 2. Teleport far away to test culling, then check Workshop (2, 2)
  const workshopState = await page.evaluate(() => {
    const game = window.__happyToy;
    // Teleport player to (96, 0, 96) which is chunk (6, 6)
    game.player.setPosition({ x: 96, y: 0, z: 96 });
    // Trigger update loop to load new chunks and cull old ones
    game.updateBackrooms(0.016);
    const hasStartChunkRemoved = !game.mapBuilder.loadedChunks.has("0,0");

    // Teleport back to Workshop (32, 0, 32) which is chunk (2, 2)
    game.player.setPosition({ x: 32, y: 0, z: 32 });
    game.updateBackrooms(0.016);
    
    const key = game.keys.find(k => k.id === "key-workshop");
    const cabinet = game.cabinets.find(c => c.id === "cabinet-workshop");
    
    return {
      loadedChunksCount: game.mapBuilder.loadedChunks.size,
      hasWorkshopChunk: game.mapBuilder.loadedChunks.has("2,2"),
      hasKey: Boolean(key),
      hasCabinet: Boolean(cabinet),
      hasStartChunkRemoved,
    };
  });

  assert(workshopState.hasWorkshopChunk, "Expected chunk (2,2) to be loaded after teleporting");
  assert(workshopState.hasKey, "Expected key-workshop to spawn in chunk (2,2)");
  assert(workshopState.hasCabinet, "Expected cabinet-workshop to spawn in chunk (2,2)");
  assert(workshopState.hasStartChunkRemoved, "Expected start chunk (0,0) to cull when player is far away");

  console.log("Procedural generation and culling passed! Testing Hwacat event triggers...");

  // 3. Teleport to Event Room (-2, -2) and test Mirror Hwacat Event
  const eventState = await page.evaluate(() => {
    const game = window.__happyToy;
    game.player.setPosition({ x: -32, y: 0, z: -32 });
    game.updateBackrooms(0.016);

    const eventObj = game.mirrorEvents[0];
    const painting = game.scene.getObjectByName("upper-hwa-painting");

    return {
      hasEventChunk: game.mapBuilder.loadedChunks.has("-2,-2"),
      hasPainting: Boolean(painting),
      eventState: eventObj.state,
      eventTriggered: eventObj.hasTriggered,
    };
  });

  assert(eventState.hasEventChunk, "Expected event room chunk (-2,-2) to load");
  assert(state.enemyCount >= 2, `Expected at least 2 monsters initialized, got ${state.enemyCount}`);

  assert(eventState.hasPainting, "Expected upper-hwa-painting mesh to exist in event room");
  assert(eventState.eventState === "idle", `Expected event state to start as idle, got ${eventState.eventState}`);

  console.log("All Backrooms verification checks passed successfully!");

} catch (error) {
  console.error("Verification failed!");
  console.error(error);
  console.error("Browser errors recorded during test:", browserErrors);
  process.exit(1);
} finally {
  await browser.close();
}

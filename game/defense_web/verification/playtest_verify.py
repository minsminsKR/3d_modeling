import asyncio
import json
import time
from pathlib import Path

from playwright.async_api import async_playwright


BASE_URL = "http://127.0.0.1:8020"
OUT_DIR = Path(__file__).resolve().parent
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"


async def snapshot(page):
    return await page.evaluate(
        """() => {
            const g = window.defenseGame;
            const text = document.body.innerText;
            const fps = document.getElementById("fps")?.textContent || "";
            const startPanel = document.getElementById("start-panel");
            const upgradePanel = document.getElementById("upgrade-panel");
            return {
                bodyText: text,
                fps,
                startPanelClass: startPanel?.className || "",
                upgradePanelClass: upgradePanel?.className || "",
                upgradeVisible: upgradePanel ? !upgradePanel.classList.contains("hidden") : false,
                game: g ? {
                    running: g.running,
                    pausedForUpgrade: g.pausedForUpgrade,
                    elapsed: g.elapsed,
                    kills: g.kills,
                    coins: g.coins,
                    level: g.level,
                    exp: g.exp,
                    expNeed: g.expNeed,
                    allyCount: g.allyCount,
                    weapon: g.currentWeapon ? g.currentWeapon().name : null,
                    enemies: g.enemies?.filter(e => e.active).length ?? null,
                    bullets: g.bullets?.filter(b => b.active).length ?? null,
                    pickups: g.pickups?.filter(p => p.active).length ?? null,
                    particles: g.particles?.filter(p => p.active).length ?? null,
                    gates: g.gates?.filter(gate => gate.active).length ?? null,
                    wave: g.wave,
                    modelReady: g.models?.ready ?? null,
                    modelLoadError: g.models?.loadError ? String(g.models.loadError) : null,
                    modelSlots: g.models?.slots?.map(slot => ({
                        type: slot.type,
                        visible: slot.root.visible
                    })) ?? null,
                    activeEnemyTypes: g.enemies?.filter(e => e.active).map(e => ({
                        type: e.type,
                        modelType: e.modelType,
                        points: e.points,
                        z: Math.round(e.z * 10) / 10
                    })).slice(0, 20) ?? []
                } : null
            };
        }"""
    )


async def run_viewport(playwright, viewport, label, duration=0):
    console = []
    failed = []
    responses = []
    browser = await playwright.chromium.launch(
        executable_path=EDGE,
        headless=True,
        args=["--disable-gpu", "--disable-dev-shm-usage"],
    )
    context = await browser.new_context(
        viewport=viewport,
        device_scale_factor=1,
        bypass_csp=True,
        ignore_https_errors=True,
    )
    page = await context.new_page()
    page.on("console", lambda msg: console.append({"type": msg.type, "text": msg.text, "location": msg.location}))
    page.on("pageerror", lambda exc: console.append({"type": "pageerror", "text": str(exc), "location": {}}))
    page.on("requestfailed", lambda req: failed.append({"url": req.url, "failure": req.failure}))
    page.on("response", lambda res: responses.append({"url": res.url, "status": res.status}))

    await page.goto(f"{BASE_URL}/?playwright={label}-{int(time.time())}", wait_until="networkidle")
    await page.screenshot(path=OUT_DIR / f"{label}_initial.png")
    before = await snapshot(page)

    await page.get_by_role("button", name="Start").click()
    await page.wait_for_timeout(2500)
    await page.screenshot(path=OUT_DIR / f"{label}_started.png")
    started = await snapshot(page)

    samples = []
    if duration:
        mouse_y = int(viewport["height"] * 0.72)
        await page.mouse.move(int(viewport["width"] * 0.5), mouse_y)
        await page.mouse.down()
        start = time.monotonic()
        next_shots = {10, 60, 120, 180}
        while time.monotonic() - start < duration:
            elapsed = int(time.monotonic() - start)
            x = int(viewport["width"] * (0.30 + 0.40 * ((elapsed % 12) / 11)))
            if (elapsed // 12) % 2:
                x = viewport["width"] - x
            await page.mouse.move(x, mouse_y)

            state = await snapshot(page)
            samples.append({"t": elapsed, **state})

            if state["upgradeVisible"]:
                buttons = page.locator("#upgrade-options button")
                if await buttons.count():
                    await buttons.first.click()

            if elapsed in next_shots:
                await page.screenshot(path=OUT_DIR / f"{label}_{elapsed}s.png")
                next_shots.remove(elapsed)
            await page.wait_for_timeout(1000)
        await page.mouse.up()
        await page.screenshot(path=OUT_DIR / f"{label}_final.png")

    final = await snapshot(page)
    await browser.close()

    interesting_responses = [
        r for r in responses
        if "/assets/" in r["url"] or "/vendor/" in r["url"] or "/static/src/" in r["url"] or r["url"].endswith("/health")
    ]
    return {
        "label": label,
        "viewport": viewport,
        "before": before,
        "started": started,
        "final": final,
        "samples": samples,
        "console": console,
        "failedRequests": failed,
        "interestingResponses": interesting_responses,
    }


async def main():
    async with async_playwright() as p:
        desktop = await run_viewport(p, {"width": 1366, "height": 768}, "desktop_1366x768", duration=180)
        mobile = await run_viewport(p, {"width": 390, "height": 844}, "mobile_390x844", duration=15)
    report = {"desktop": desktop, "mobile": mobile}
    (OUT_DIR / "playtest_results.json").write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    asyncio.run(main())

import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const { chromium } = require("playwright");

const url = "http://127.0.0.1:8010/";

console.log("Checking initialization errors on:", url);

const browser = await chromium.launch({
  executablePath: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  headless: true,
});

const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });

page.on("console", (msg) => {
  console.log(`[CONSOLE ${msg.type().toUpperCase()}]:`, msg.text());
});
page.on("pageerror", (err) => {
  console.error("[PAGE ERROR]:", err.message, err.stack);
});

try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 10000 });
  await new Promise(resolve => setTimeout(resolve, 3000));

  const info = await page.evaluate(() => {
    const startButton = document.querySelector("#start-button");
    const statusLine = document.querySelector("#status-line");
    const startTitle = document.querySelector("#start-title");
    const startDesc = document.querySelector("#start-description");
    return {
      startButtonText: startButton ? startButton.textContent : "null",
      startButtonDisabled: startButton ? startButton.disabled : null,
      statusLineText: statusLine ? statusLine.textContent : "null",
      startTitleText: startTitle ? startTitle.textContent : "null",
      startDescText: startDesc ? startDesc.textContent : "null",
      happyToyExists: typeof window.__happyToy !== "undefined",
      assetsReady: window.__happyToy?.assetsReady,
    };
  });
  console.log("Page State Info:", info);

} catch (error) {
  console.error("Failed to load page:", error);
} finally {
  await browser.close();
}

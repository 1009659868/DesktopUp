import { mkdir } from "node:fs/promises";
import { createRequire } from "node:module";
import { dirname, format, parse, resolve } from "node:path";

const playwrightModule = process.env.DESKTOP_UP_PLAYWRIGHT_MODULE;
const screenshotPath = process.env.DESKTOP_UP_SCREENSHOT_PATH;
const demoUrl = process.env.DESKTOP_UP_DEMO_URL ?? "http://127.0.0.1:4173";

if (!playwrightModule || !screenshotPath) {
  throw new Error("DESKTOP_UP_PLAYWRIGHT_MODULE and DESKTOP_UP_SCREENSHOT_PATH are required");
}

const require = createRequire(import.meta.url);
const { chromium } = require(playwrightModule);
const browser = await chromium.launch({ channel: "msedge", headless: true });
const context = await browser.newContext({ viewport: { width: 1600, height: 1000 }, deviceScaleFactor: 1 });
const page = await context.newPage();
const browserErrors = [];
const parsedScreenshotPath = parse(resolve(screenshotPath));
const posterScreenshotPath = format({
  ...parsedScreenshotPath,
  base: undefined,
  name: `${parsedScreenshotPath.name}-poster`
});

page.on("console", (message) => {
  if (message.type() === "error") browserErrors.push(message.text());
});
page.on("pageerror", (error) => browserErrors.push(error.message));

try {
  await page.goto(demoUrl, { waitUntil: "networkidle" });
  await page.locator(".poster").first().waitFor({ state: "visible" });
  await mkdir(dirname(resolve(screenshotPath)), { recursive: true });
  await page.screenshot({ path: resolve(screenshotPath), fullPage: false });

  const initialItems = await page.locator(".poster.is-selected .game-tile").count();
  await page.locator("#add-game").click();
  const addedItems = await page.locator(".poster.is-selected .game-tile").count();
  if (addedItems !== initialItems + 1) throw new Error("Auto-layout add-item interaction failed");

  await page.locator('[data-theme="ember"]').click();
  if (!(await page.locator(".poster.is-selected").evaluate((node) => node.classList.contains("theme-ember")))) {
    throw new Error("Theme switching failed");
  }

  await page.locator("#edit-toggle").click();
  if ((await page.locator("#edit-toggle").getAttribute("aria-pressed")) !== "true") {
    throw new Error("Edit mode toggle failed");
  }

  const selectedPoster = page.locator(".poster.is-selected");
  const initialX = await selectedPoster.evaluate((node) => getComputedStyle(node).getPropertyValue("--poster-x"));
  const header = selectedPoster.locator(".poster-header");
  const headerBox = await header.boundingBox();
  await page.mouse.move(headerBox.x + 80, headerBox.y + 18);
  await page.mouse.down();
  await page.mouse.move(headerBox.x + 140, headerBox.y + 48);
  await page.mouse.up();
  const lockedX = await selectedPoster.evaluate((node) => getComputedStyle(node).getPropertyValue("--poster-x"));
  if (lockedX !== initialX) throw new Error("Locked poster unexpectedly moved");

  await selectedPoster.locator('[data-action="lock"]').click();
  const unlockedHeaderBox = await selectedPoster.locator(".poster-header").boundingBox();
  await page.mouse.move(unlockedHeaderBox.x + 80, unlockedHeaderBox.y + 18);
  await page.mouse.down();
  await page.mouse.move(unlockedHeaderBox.x + 140, unlockedHeaderBox.y + 48);
  await page.mouse.up();
  const movedX = await selectedPoster.evaluate((node) => getComputedStyle(node).getPropertyValue("--poster-x"));
  if (movedX === initialX) throw new Error("Unlocked poster drag failed");

  await page.keyboard.press("Space");
  await page.locator("#runtime-chip.is-paused").waitFor({ state: "visible" });

  const posterPage = await context.newPage();
  await posterPage.setViewportSize({ width: 490, height: 392 });
  posterPage.on("console", (message) => {
    if (message.type() === "error") browserErrors.push(`poster: ${message.text()}`);
  });
  posterPage.on("pageerror", (error) => browserErrors.push(`poster: ${error.message}`));
  await posterPage.goto(new URL("poster.html", demoUrl).href, { waitUntil: "networkidle" });
  await posterPage.locator(".poster").waitFor({ state: "visible" });
  if (!(await posterPage.locator(".poster").evaluate((node) => node.classList.contains("theme-ember")))) {
    throw new Error("Poster surface did not receive shared theme state");
  }
  await posterPage.screenshot({ path: posterScreenshotPath, omitBackground: true });
  await posterPage.locator('[data-action="lock"]').click();
  if (!(await posterPage.locator(".poster").evaluate((node) => node.classList.contains("is-locked")))) {
    throw new Error("Poster surface lock toggle failed");
  }
  await posterPage.locator('[data-action="collapse"]').click();
  if (!(await posterPage.locator(".poster").evaluate((node) => node.classList.contains("is-collapsed")))) {
    throw new Error("Poster surface collapse toggle failed");
  }
  await posterPage.close();

  if (browserErrors.length) throw new Error(`Browser errors:\n${browserErrors.join("\n")}`);
  process.stdout.write(`Visual demo verified and captured at ${resolve(screenshotPath)} and ${posterScreenshotPath}\n`);
} finally {
  await browser.close();
}

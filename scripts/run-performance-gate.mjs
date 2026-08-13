import { spawn } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { join, resolve } from "node:path";

const releaseExecutable = resolve("src-tauri/target/release/DesktopUp.exe");
const appDataDirectory = process.env.APPDATA;
if (!existsSync(releaseExecutable)) {
  throw new Error(`Release executable was not found: ${releaseExecutable}`);
}
if (!appDataDirectory) {
  throw new Error("APPDATA is not available");
}

const baselinePath = join(
  appDataDirectory,
  "com.allmight.desktopup",
  "performance-baseline-v4.json"
);
const startedAtUnixSeconds = Math.floor(Date.now() / 1000);
const child = spawn(releaseExecutable, ["--cdrp-performance-gate"], {
  windowsHide: true,
  stdio: "inherit"
});
const [exitCode, signal] = await new Promise((resolveExit, reject) => {
  child.once("error", reject);
  child.once("exit", (code, exitSignal) => resolveExit([code, exitSignal]));
});

if (!existsSync(baselinePath)) {
  throw new Error(`Performance baseline was not written (exit=${exitCode}, signal=${signal})`);
}
const baseline = JSON.parse(readFileSync(baselinePath, "utf8"));
if (Number(baseline.savedAtUnixSeconds ?? 0) < startedAtUnixSeconds) {
  throw new Error("Performance baseline is stale; another Desktop-Up instance may be running");
}

process.stdout.write(`${JSON.stringify(baseline.gate, null, 2)}\n`);
if (!baseline.gate?.passed || exitCode !== 0) process.exitCode = 1;

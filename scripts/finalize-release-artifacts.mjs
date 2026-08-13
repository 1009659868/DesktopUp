import { createHash } from "node:crypto";
import {
  copyFileSync,
  existsSync,
  readFileSync,
  readdirSync,
  statSync,
} from "node:fs";
import { basename, join, resolve } from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const FINAL_INSTALLER_NAME = "DesktopUpSetup.exe";

function sha256(path) {
  return createHash("sha256").update(readFileSync(path)).digest("hex").toUpperCase();
}

export function finalizeReleaseArtifacts({ repoRoot }) {
  const root = resolve(repoRoot);
  const config = JSON.parse(
    readFileSync(join(root, "src-tauri", "tauri.conf.json"), "utf8"),
  );
  const nsisDirectory = join(
    root,
    "src-tauri",
    "target",
    "release",
    "bundle",
    "nsis",
  );
  const finalPath = join(nsisDirectory, FINAL_INSTALLER_NAME);
  const candidates = existsSync(nsisDirectory)
    ? readdirSync(nsisDirectory)
        .filter(
          (name) =>
            name.toLowerCase().endsWith("-setup.exe") &&
            name.toLowerCase() !== FINAL_INSTALLER_NAME.toLowerCase(),
        )
        .map((name) => join(nsisDirectory, name))
        .filter((path) => statSync(path).isFile())
        .sort((left, right) => statSync(right).mtimeMs - statSync(left).mtimeMs)
    : [];
  const sourcePath = candidates[0] ?? "";
  if (!sourcePath) {
    return {
      status: "blocked",
      productName: config.productName ?? "",
      mainBinaryName: config.mainBinaryName ?? "",
      sourcePath: "",
      finalPath,
      gap: "versionedNsisInstallerMissing",
    };
  }
  copyFileSync(sourcePath, finalPath);
  const sourceHash = sha256(sourcePath);
  const finalHash = sha256(finalPath);
  return {
    status: "ready",
    productName: config.productName ?? "",
    mainBinaryName: config.mainBinaryName ?? "",
    sourcePath,
    sourceName: basename(sourcePath),
    sourceSha256: sourceHash,
    finalPath,
    finalName: basename(finalPath),
    finalSha256: finalHash,
    byteIdentical: sourceHash === finalHash,
    sideEffects: {
      writesReleaseAliasOnly: true,
      installsOrUninstalls: false,
      writesRegistry: false,
      requestsElevation: false,
    },
  };
}

function main() {
  const report = finalizeReleaseArtifacts({ repoRoot: process.cwd() });
  process.stdout.write(JSON.stringify(report, null, 2) + "\n");
  if (process.argv.includes("--require-ready") && report.status !== "ready") {
    process.exitCode = 2;
  }
}

const invokedPath = process.argv[1] ? resolve(process.argv[1]) : "";
if (invokedPath === resolve(fileURLToPath(import.meta.url))) {
  main();
}

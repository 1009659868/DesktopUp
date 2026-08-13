import assert from "node:assert/strict";
import {
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { finalizeReleaseArtifacts } from "../scripts/finalize-release-artifacts.mjs";

function write(path, content) {
  mkdirSync(join(path, ".."), { recursive: true });
  writeFileSync(path, content);
}

test("release finalizer creates the exact contract installer alias", () => {
  const root = mkdtempSync(join(tmpdir(), "desktop-up-finalizer-"));
  try {
    write(
      join(root, "src-tauri", "tauri.conf.json"),
      JSON.stringify({ productName: "DesktopUp", mainBinaryName: "DesktopUp" }),
    );
    const source = join(
      root,
      "src-tauri",
      "target",
      "release",
      "bundle",
      "nsis",
      "DesktopUp_0.0.1_x64-setup.exe",
    );
    write(source, "fixture installer");
    const report = finalizeReleaseArtifacts({ repoRoot: root });
    assert.equal(report.status, "ready");
    assert.equal(report.finalName, "DesktopUpSetup.exe");
    assert.equal(report.byteIdentical, true);
    assert.deepEqual(readFileSync(report.finalPath), readFileSync(source));
    assert.deepEqual(report.sideEffects, {
      writesReleaseAliasOnly: true,
      installsOrUninstalls: false,
      writesRegistry: false,
      requestsElevation: false,
    });
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("release finalizer fails closed when the versioned NSIS artifact is absent", () => {
  const root = mkdtempSync(join(tmpdir(), "desktop-up-finalizer-missing-"));
  try {
    write(
      join(root, "src-tauri", "tauri.conf.json"),
      JSON.stringify({ productName: "DesktopUp", mainBinaryName: "DesktopUp" }),
    );
    const report = finalizeReleaseArtifacts({ repoRoot: root });
    assert.equal(report.status, "blocked");
    assert.equal(report.gap, "versionedNsisInstallerMissing");
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

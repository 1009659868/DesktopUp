import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import {
  mkdirSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  rmSync,
  statSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { probeInstallArtifacts } from "../scripts/probe-install-artifacts.mjs";

function write(path, content) {
  mkdirSync(join(path, ".."), { recursive: true });
  writeFileSync(path, content);
}

function createFixture() {
  const root = mkdtempSync(join(tmpdir(), "desktop-up-install-probe-"));
  const tauriRoot = join(root, "src-tauri");
  write(
    join(tauriRoot, "tauri.conf.json"),
    JSON.stringify({
      productName: "Desktop-Up",
      bundle: {
        windows: {
          nsis: { installMode: "currentUser" },
        },
      },
    }),
  );
  write(
    join(tauriRoot, "Cargo.toml"),
    '[package]\nname = "desktop-up"\nversion = "0.0.1"\n',
  );
  write(
    join(tauriRoot, "src", "install_baseline.rs"),
    [
      'pub const INSTALL_ROOT: &str = r"%LOCALAPPDATA%\\Programs\\DesktopUp";',
      "required_install_entries: vec![",
      '  "DesktopUp.exe",',
      '  "uninstall.exe",',
      '  "resources/",',
      "],",
    ].join("\n"),
  );
  write(
    join(root, "Docs", "Rules", "RELEASE_AND_UNINSTALL_POLICY.md"),
    "Final entry: DesktopUpSetup.exe\n",
  );
  write(
    join(tauriRoot, "target", "release", "desktop-up.exe"),
    "release fixture",
  );
  write(
    join(
      tauriRoot,
      "target",
      "release",
      "bundle",
      "nsis",
      "Desktop-Up_0.0.1_x64-setup.exe",
    ),
    "installer fixture",
  );
  const templatePath = join(root, "local-installer.nsi");
  write(
    templatePath,
    [
      "RequestExecutionLevel user",
      'StrCpy $INSTDIR "$LOCALAPPDATA\\$' + '{PRODUCTNAME}"',
      'WriteUninstaller "$INSTDIR\\uninstall.exe"',
    ].join("\n"),
  );
  return { root, templatePath };
}

function snapshotTree(root) {
  const result = {};
  function visit(directory, relativeDirectory) {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const path = join(directory, entry.name);
      const relative = join(relativeDirectory, entry.name);
      if (entry.isDirectory()) {
        result[relative] = { kind: "directory" };
        visit(path, relative);
      } else {
        const stats = statSync(path);
        result[relative] = {
          kind: "file",
          size: stats.size,
          hash: createHash("sha256").update(readFileSync(path)).digest("hex"),
        };
      }
    }
  }
  visit(root, "");
  return result;
}

test("artifact probe reports the four fixture packaging gaps", () => {
  const fixture = createFixture();
  try {
    const report = probeInstallArtifacts({
      repoRoot: fixture.root,
      nsisTemplatePath: fixture.templatePath,
    });
    assert.equal(report.status, "gapsDetected");
    assert.equal(report.releaseReadiness, "notReleaseReady");
    assert.equal(report.plannedInstallerBehavior.installMode, "currentUser");
    assert.equal(
      report.plannedInstallerBehavior.requestExecutionLevelUser,
      true,
    );
    assert.equal(
      report.plannedInstallerBehavior.uninstallerGenerationPlanned,
      true,
    );
    assert.deepEqual(
      report.gaps.map((gap) => gap.code),
      [
        "installRootMismatch",
        "mainExecutableNameMismatch",
        "resourcesDirectoryNotBundled",
        "finalInstallerNameMismatch",
      ],
    );
  } finally {
    rmSync(fixture.root, { recursive: true, force: true });
  }
});

test("artifact probe is read-only and records its inspection limitation", () => {
  const fixture = createFixture();
  try {
    const before = snapshotTree(fixture.root);
    const report = probeInstallArtifacts({
      repoRoot: fixture.root,
      nsisTemplatePath: fixture.templatePath,
    });
    const after = snapshotTree(fixture.root);
    assert.deepEqual(after, before);
    assert.equal(report.inspection.compressedPayloadExtracted, false);
    assert.equal(
      report.inspection.compressedPayloadContentsIndependentlyVerified,
      false,
    );
    assert.deepEqual(report.sideEffects, {
      writesInstallRoot: false,
      createsDirectories: false,
      installsOrUninstalls: false,
      requestsElevation: false,
    });
  } finally {
    rmSync(fixture.root, { recursive: true, force: true });
  }
});

test("runtime production probe only performs metadata reads", () => {
  const source = readFileSync(
    new URL("../src-tauri/src/install_probe.rs", import.meta.url),
    "utf8",
  );
  const productionSource = source.split("#[cfg(test)]", 1)[0];
  assert.match(productionSource, /fs::symlink_metadata/);
  assert.doesNotMatch(
    productionSource,
    /fs::(?:create|write|copy|rename|remove)|Command::|create_dir/,
  );
});

test("runtime install content probe is exposed through the Tauri command", () => {
  const source = readFileSync(
    new URL("../src-tauri/src/lib.rs", import.meta.url),
    "utf8",
  );
  assert.match(source, /fn install_content_probe\(/);
  assert.match(
    source,
    /generate_handler!\[[\s\S]*?install_content_probe,/,
  );
});

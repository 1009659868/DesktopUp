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

import {
  probeRuntimeDependencies,
  readPeMetadata,
} from "../scripts/probe-runtime-dependencies.mjs";

function write(path, content) {
  mkdirSync(join(path, ".."), { recursive: true });
  writeFileSync(path, content);
}

function createPeFixture() {
  const buffer = Buffer.alloc(0x800);
  buffer.write("MZ", 0, "ascii");
  buffer.writeUInt32LE(0x80, 0x3c);
  buffer.write("PE\0\0", 0x80, "ascii");
  buffer.writeUInt16LE(0x8664, 0x84);
  buffer.writeUInt16LE(1, 0x86);
  buffer.writeUInt16LE(0xf0, 0x94);
  const optional = 0x98;
  buffer.writeUInt16LE(0x20b, optional);
  buffer.writeUInt16LE(2, optional + 68);
  buffer.writeUInt32LE(16, optional + 108);
  buffer.writeUInt32LE(0x1100, optional + 120);
  buffer.writeUInt32LE(60, optional + 124);
  const section = optional + 0xf0;
  buffer.write(".rdata", section, "ascii");
  buffer.writeUInt32LE(0x500, section + 8);
  buffer.writeUInt32LE(0x1000, section + 12);
  buffer.writeUInt32LE(0x500, section + 16);
  buffer.writeUInt32LE(0x200, section + 20);
  const imports = 0x300;
  buffer.writeUInt32LE(0x1200, imports + 12);
  buffer.writeUInt32LE(0x1230, imports + 32);
  buffer.write("KERNEL32.dll\0", 0x400, "ascii");
  buffer.write("api-ms-win-crt-runtime-l1-1-0.dll\0", 0x430, "ascii");
  return buffer;
}

function createFixture({ assignedGaps = false } = {}) {
  const root = mkdtempSync(join(tmpdir(), "desktop-up-runtime-gate-"));
  const tauriRoot = join(root, "src-tauri");
  const releaseRoot = join(tauriRoot, "target", "release");
  write(join(root, ".cargo", "config.toml"), 'STATIC_VCRUNTIME = "true"\n');
  write(
    join(tauriRoot, "Cargo.toml"),
    [
      "[package]",
      'name = "desktop-up"',
      'version = "0.0.1"',
      "",
      "[dependencies]",
      'rusqlite = { version = "0.40.2", features = ["bundled"] }',
    ].join("\n"),
  );
  write(
    join(tauriRoot, "Cargo.lock"),
    [
      ["rusqlite", "0.40.2"],
      ["libsqlite3-sys", "0.38.2"],
      ["tauri", "2.11.5"],
      ["tauri-build", "2.6.3"],
      ["tauri-plugin-single-instance", "2.4.3"],
      ["webview2-com", "0.38.2"],
      ["windows", "0.61.3"],
    ]
      .map(([name, version]) =>
        ['[[package]]', 'name = "' + name + '"', 'version = "' + version + '"'].join("\n"),
      )
      .join("\n\n"),
  );
  write(
    join(tauriRoot, "tauri.conf.json"),
    JSON.stringify({
      productName: "DesktopUp",
      mainBinaryName: "DesktopUp",
      bundle: {
        resources: ["resources/*"],
        windows: {
          nsis: { installMode: "currentUser" },
          webviewInstallMode: { type: "downloadBootstrapper" },
        },
      },
    }),
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
    join(tauriRoot, "src", "autostart_contract.rs"),
    'pub const AUTOSTART_REGISTRY_VALUE_NAME: &str = "DesktopUp";\n',
  );
  write(
    join(root, "Docs", "Rules", "RELEASE_AND_UNINSTALL_POLICY.md"),
    "Final entry: DesktopUpSetup.exe\n",
  );
  write(join(releaseRoot, "DesktopUp.exe"), createPeFixture());
  write(
    join(releaseRoot, "bundle", "nsis", "DesktopUpSetup.exe"),
    "installer fixture",
  );
  const nsisTemplatePath = join(root, "installer-template.nsi");
  write(
    nsisTemplatePath,
    [
      "RequestExecutionLevel user",
      'StrCpy $INSTDIR "$LOCALAPPDATA\\Programs\\$' + '{PRODUCTNAME}"',
      'WriteUninstaller "$INSTDIR\\uninstall.exe"',
    ].join("\n"),
  );
  const generatedNsisPath = join(releaseRoot, "nsis", "x64", "installer.nsi");
  write(
    generatedNsisPath,
    [
      '!define ARCH "x64"',
      '!define INSTALLMODE "currentUser"',
      '!define INSTALLWEBVIEW2MODE "downloadBootstrapper"',
      '!define LICENSE ""',
      '!define PRODUCTNAME "DesktopUp"',
      'NSISdl::download "https://go.microsoft.com/fwlink/p/?LinkId=2124703"',
      'Abort "$(webview2AbortError)"',
      'WriteUninstaller "$INSTDIR\\uninstall.exe"',
      'StrCpy $INSTDIR "$LOCALAPPDATA\\Programs\\${PRODUCTNAME}"',
      'File "resources\\INSTALLATION_SCOPE.txt"',
      'DeleteRegValue HKCU "Software\\Microsoft\\Windows\\CurrentVersion\\Run" "' +
        (assignedGaps ? "Desktop-Up" : "${PRODUCTNAME}") +
        '"',
    ].join("\n"),
  );
  write(join(tauriRoot, "resources", "poster.json"), "{}\n");
  return { root, nsisTemplatePath, generatedNsisPath };
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

test("PE evidence parser identifies x64 GUI imports", () => {
  const root = mkdtempSync(join(tmpdir(), "desktop-up-pe-evidence-"));
  try {
    const path = join(root, "desktop-up.exe");
    write(path, createPeFixture());
    assert.deepEqual(readPeMetadata(path), {
      machine: "x64",
      subsystem: "windowsGui",
      imports: ["api-ms-win-crt-runtime-l1-1-0.dll", "kernel32.dll"],
    });
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("runtime dependency gate can prove a release-ready fixture", () => {
  const fixture = createFixture();
  try {
    const report = probeRuntimeDependencies({
      repoRoot: fixture.root,
      nsisTemplatePath: fixture.nsisTemplatePath,
      generatedNsisPath: fixture.generatedNsisPath,
    });
    assert.deepEqual(report.gaps, []);
    assert.equal(report.status, "contractPackagingReady");
    assert.equal(report.dependencyEvidenceStatus, "pass");
    assert.equal(report.releaseReadiness, "ready");
    assert.deepEqual(report.gaps, []);
    assert.equal(report.releasePe.machine, "x64");
    assert.equal(report.releasePe.subsystem, "windowsGui");
    assert.deepEqual(report.releasePe.dynamicVcRuntimeImports, []);
    assert.deepEqual(report.releasePe.externalSqliteImports, []);
    assert.equal(report.dependencies.webView2.downloadFailureAbortsInstall, true);
    assert.equal(report.generatedInstaller.installRoot, "%LOCALAPPDATA%\\Programs\\DesktopUp");
    assert.equal(report.generatedInstaller.resourcePayloadPresent, true);
    assert.equal(report.releaseCertification, "notCertifiedUntilC-RCGMatricesPass");
  } finally {
    rmSync(fixture.root, { recursive: true, force: true });
  }
});

test("an assigned autostart gap permits handoff but blocks release readiness", () => {
  const fixture = createFixture({ assignedGaps: true });
  try {
    const report = probeRuntimeDependencies({
      repoRoot: fixture.root,
      nsisTemplatePath: fixture.nsisTemplatePath,
      generatedNsisPath: fixture.generatedNsisPath,
    });
    assert.equal(report.status, "handoffReadyWithAssignedGaps");
    assert.equal(report.dependencyEvidenceStatus, "pass");
    assert.equal(report.releaseReadiness, "notReleaseReady");
    assert.deepEqual(
      report.gaps.map(({ code, owner }) => ({ code, owner })),
      [{ code: "autostartCleanupValueNameMismatch", owner: "C-URV" }],
    );
  } finally {
    rmSync(fixture.root, { recursive: true, force: true });
  }
});

test("runtime dependency gate is read-only and freezes downstream owners", () => {
  const fixture = createFixture();
  try {
    const before = snapshotTree(fixture.root);
    const report = probeRuntimeDependencies({
      repoRoot: fixture.root,
      nsisTemplatePath: fixture.nsisTemplatePath,
      generatedNsisPath: fixture.generatedNsisPath,
    });
    assert.deepEqual(snapshotTree(fixture.root), before);
    assert.deepEqual(report.sideEffects, {
      writesFiles: false,
      writesRegistry: false,
      installsOrUninstalls: false,
      launchesProcesses: false,
      requestsElevation: false,
    });
    assert.deepEqual(
      report.handoff.map(({ owner }) => owner),
      ["C-RLC", "C-UGM", "C-URV", "C-RCG"],
    );
  } finally {
    rmSync(fixture.root, { recursive: true, force: true });
  }
});

test("runtime dependency probe source contains no mutation or process-launch primitive", () => {
  const source = readFileSync(
    new URL("../scripts/probe-runtime-dependencies.mjs", import.meta.url),
    "utf8",
  );
  assert.doesNotMatch(
    source,
    /writeFileSync|appendFileSync|rmSync|renameSync|copyFileSync|spawnSync|execSync|child_process/,
  );
});

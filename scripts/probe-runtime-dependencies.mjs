import { createHash } from "node:crypto";
import {
  existsSync,
  readFileSync,
  readdirSync,
  statSync,
} from "node:fs";
import { join, resolve } from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

import { probeInstallArtifacts } from "./probe-install-artifacts.mjs";

const PROBE_NAME = "runtime-dependency-handoff-gate-v1";
const REQUIRED_PACKAGES = [
  "rusqlite",
  "libsqlite3-sys",
  "tauri",
  "tauri-build",
  "tauri-plugin-single-instance",
  "webview2-com",
  "windows",
];

function argumentValue(name) {
  const index = process.argv.indexOf(name);
  return index >= 0 ? process.argv[index + 1] : undefined;
}

function readText(path) {
  return readFileSync(path, "utf8");
}

function sha256(path) {
  return createHash("sha256").update(readFileSync(path)).digest("hex").toUpperCase();
}

function artifact(path) {
  if (!path || !existsSync(path) || !statSync(path).isFile()) {
    return { path: path ?? "", exists: false, sizeBytes: 0, sha256: "" };
  }
  const stats = statSync(path);
  return {
    path,
    exists: true,
    sizeBytes: stats.size,
    sha256: sha256(path),
  };
}

function packageBlock(cargoManifest) {
  return cargoManifest.match(/\[package\]([\s\S]*?)(?:\n\[|$)/)?.[1] ?? "";
}

function packageName(cargoManifest) {
  const value = packageBlock(cargoManifest).match(/^\s*name\s*=\s*"([^"]+)"/m)?.[1];
  if (!value) {
    throw new Error("missing Cargo package name");
  }
  return value;
}

function packageLicense(cargoManifest) {
  return packageBlock(cargoManifest).match(/^\s*license\s*=\s*"([^"]+)"/m)?.[1] ?? "";
}

function configuredBinaryName(tauriConfig, cargoName) {
  return tauriConfig.mainBinaryName || cargoName;
}

function lockedPackages(cargoLock) {
  const packages = new Map();
  for (const match of cargoLock.matchAll(
    /\[\[package\]\]\s+name\s*=\s*"([^"]+)"\s+version\s*=\s*"([^"]+)"/g,
  )) {
    packages.set(match[1], match[2]);
  }
  return Object.fromEntries(
    REQUIRED_PACKAGES.map((name) => [name, packages.get(name) ?? "missing"]),
  );
}

function findInstaller(directory) {
  if (!existsSync(directory)) {
    return "";
  }
  const candidates = readdirSync(directory)
      .filter((name) => name.toLowerCase().endsWith(".exe"))
      .map((name) => join(directory, name))
      .filter((path) => statSync(path).isFile())
      .sort();
  return (
    candidates.find((path) => path.toLowerCase().endsWith("desktopupsetup.exe")) ??
    candidates.at(-1) ??
    ""
  );
}

function readCString(buffer, offset) {
  if (offset < 0 || offset >= buffer.length) {
    throw new Error("PE string offset is outside the file");
  }
  let end = offset;
  while (end < buffer.length && buffer[end] !== 0) {
    end += 1;
  }
  return buffer.subarray(offset, end).toString("ascii");
}

export function readPeMetadata(path) {
  const buffer = readFileSync(path);
  if (buffer.length < 0x100 || buffer.toString("ascii", 0, 2) !== "MZ") {
    throw new Error("not a DOS/PE executable");
  }
  const peOffset = buffer.readUInt32LE(0x3c);
  if (
    peOffset + 24 > buffer.length ||
    buffer.toString("ascii", peOffset, peOffset + 4) !== "PE\u0000\u0000"
  ) {
    throw new Error("missing PE signature");
  }
  const machineValue = buffer.readUInt16LE(peOffset + 4);
  const sectionCount = buffer.readUInt16LE(peOffset + 6);
  const optionalSize = buffer.readUInt16LE(peOffset + 20);
  const optionalOffset = peOffset + 24;
  const magic = buffer.readUInt16LE(optionalOffset);
  const directoryOffset =
    magic === 0x20b ? optionalOffset + 112 : magic === 0x10b ? optionalOffset + 96 : -1;
  if (directoryOffset < 0 || optionalOffset + optionalSize > buffer.length) {
    throw new Error("unsupported PE optional header");
  }
  const subsystemValue = buffer.readUInt16LE(optionalOffset + 68);
  const importRva = buffer.readUInt32LE(directoryOffset + 8);
  const sectionOffset = optionalOffset + optionalSize;
  const sections = [];
  for (let index = 0; index < sectionCount; index += 1) {
    const offset = sectionOffset + index * 40;
    if (offset + 40 > buffer.length) {
      throw new Error("truncated PE section table");
    }
    sections.push({
      virtualSize: buffer.readUInt32LE(offset + 8),
      virtualAddress: buffer.readUInt32LE(offset + 12),
      rawSize: buffer.readUInt32LE(offset + 16),
      rawOffset: buffer.readUInt32LE(offset + 20),
    });
  }
  function rvaToOffset(rva) {
    const section = sections.find(
      (candidate) =>
        rva >= candidate.virtualAddress &&
        rva <
          candidate.virtualAddress +
            Math.max(candidate.virtualSize, candidate.rawSize),
    );
    if (!section) {
      throw new Error("PE RVA is outside mapped sections");
    }
    return section.rawOffset + (rva - section.virtualAddress);
  }

  const imports = [];
  if (importRva !== 0) {
    let descriptorOffset = rvaToOffset(importRva);
    for (let index = 0; index < 256; index += 1) {
      if (descriptorOffset + 20 > buffer.length) {
        throw new Error("truncated PE import table");
      }
      const originalThunk = buffer.readUInt32LE(descriptorOffset);
      const timestamp = buffer.readUInt32LE(descriptorOffset + 4);
      const forwarder = buffer.readUInt32LE(descriptorOffset + 8);
      const nameRva = buffer.readUInt32LE(descriptorOffset + 12);
      const firstThunk = buffer.readUInt32LE(descriptorOffset + 16);
      if (
        originalThunk === 0 &&
        timestamp === 0 &&
        forwarder === 0 &&
        nameRva === 0 &&
        firstThunk === 0
      ) {
        break;
      }
      imports.push(readCString(buffer, rvaToOffset(nameRva)));
      descriptorOffset += 20;
    }
  }
  const normalizedImports = [...new Set(imports.map((name) => name.toLowerCase()))].sort();
  return {
    machine: machineValue === 0x8664 ? "x64" : "0x" + machineValue.toString(16),
    subsystem:
      subsystemValue === 2 ? "windowsGui" : "subsystem-" + subsystemValue,
    imports: normalizedImports,
  };
}

function parseDefine(source, name) {
  return source.match(new RegExp('!define\\s+' + name + '\\s+"([^"]*)"'))?.[1] ?? "";
}

function directoryHasFiles(path) {
  return (
    existsSync(path) &&
    statSync(path).isDirectory() &&
    readdirSync(path, { withFileTypes: true }).some(
      (entry) => entry.isFile() || entry.isDirectory(),
    )
  );
}

function addGap(gaps, code, owner, detail, severity = "error") {
  gaps.push({ code, severity, owner, detail });
}

export function probeRuntimeDependencies({
  repoRoot,
  nsisTemplatePath = "",
  generatedNsisPath = "",
}) {
  const root = resolve(repoRoot);
  const tauriRoot = join(root, "src-tauri");
  const releaseRoot = join(tauriRoot, "target", "release");
  const cargoManifest = readText(join(tauriRoot, "Cargo.toml"));
  const cargoLock = readText(join(tauriRoot, "Cargo.lock"));
  const cargoConfig = readText(join(root, ".cargo", "config.toml"));
  const tauriConfig = JSON.parse(readText(join(tauriRoot, "tauri.conf.json")));
  const autostartSource = readText(join(tauriRoot, "src", "autostart_contract.rs"));
  const cargoName = packageName(cargoManifest);
  const binaryName = configuredBinaryName(tauriConfig, cargoName);
  const releasePath = join(releaseRoot, binaryName + ".exe");
  const installerPath = findInstaller(join(releaseRoot, "bundle", "nsis"));
  const generatedNsis =
    generatedNsisPath || join(releaseRoot, "nsis", "x64", "installer.nsi");
  const nsisSource =
    existsSync(generatedNsis) && statSync(generatedNsis).isFile()
      ? readText(generatedNsis)
      : "";
  const pe = existsSync(releasePath)
    ? readPeMetadata(releasePath)
    : { machine: "missing", subsystem: "missing", imports: [] };
  const imports = pe.imports;
  const dynamicVcRuntimeImports = imports.filter((name) =>
    /^(vcruntime|msvcp)\d.*\.dll$/i.test(name),
  );
  const externalSqliteImports = imports.filter((name) => /sqlite.*\.dll$/i.test(name));
  const versions = lockedPackages(cargoLock);
  const staticVcConfigured =
    /^\s*STATIC_VCRUNTIME\s*=\s*"true"\s*$/m.test(cargoConfig);
  const bundledSqliteConfigured =
    /rusqlite\s*=\s*\{[^}]*features\s*=\s*\[[^\]]*"bundled"/s.test(cargoManifest);
  const webviewMode = tauriConfig.bundle?.windows?.webviewInstallMode?.type ?? "";
  const generatedWebviewMode = parseDefine(nsisSource, "INSTALLWEBVIEW2MODE");
  const webviewDownloadUrl =
    nsisSource.match(/NSISdl::download\s+"([^"]+)"/)?.[1] ?? "";
  const webviewDownloadFailureAborts =
    /NSISdl::download[\s\S]*?Abort "\$\(webview2AbortError\)"/.test(nsisSource);
  const generatedArchitecture = parseDefine(nsisSource, "ARCH");
  const generatedInstallMode = parseDefine(nsisSource, "INSTALLMODE");
  const generatedLicense = parseDefine(nsisSource, "LICENSE");
  const productName = parseDefine(nsisSource, "PRODUCTNAME");
  const generatedInstallSuffix = nsisSource.match(
    /StrCpy\s+\$INSTDIR\s+"\$LOCALAPPDATA\\([^"]+)"/,
  )?.[1] ?? "";
  const generatedInstallRoot = generatedInstallSuffix
    ? "%LOCALAPPDATA%\\" +
      generatedInstallSuffix.replaceAll("$" + "{PRODUCTNAME}", productName)
    : "";
  const generatedResourcePayload = /INSTALLATION_SCOPE\.txt/i.test(nsisSource);
  const cleanupToken =
    nsisSource.match(
      /DeleteRegValue\s+HKCU\s+"Software\\Microsoft\\Windows\\CurrentVersion\\Run"\s+"([^"]+)"/,
    )?.[1] ?? "";
  const cleanupValueName =
    cleanupToken === "$" + "{PRODUCTNAME}" ? productName : cleanupToken;
  const expectedAutostartValue =
    autostartSource.match(
      /AUTOSTART_REGISTRY_VALUE_NAME:\s*&str\s*=\s*"([^"]+)"/,
    )?.[1] ?? "";
  const packageLicenseValue = packageLicense(cargoManifest);
  const licenseDirectory = join(tauriRoot, "licenses");
  const resourcesDirectory = join(tauriRoot, "resources");
  const noticeCandidates = [
    join(root, "THIRD_PARTY_NOTICES.txt"),
    join(root, "THIRD-PARTY-NOTICES.txt"),
    join(root, "NOTICE"),
    join(root, "NOTICE.txt"),
  ];
  const noticePath = noticeCandidates.find((path) => existsSync(path)) ?? "";
  const installProbe = probeInstallArtifacts({ repoRoot: root, nsisTemplatePath });
  const gaps = installProbe.gaps.map((gap) => ({ ...gap }));

  if (pe.machine !== "x64") {
    addGap(gaps, "releaseArchitectureMismatch", "C-RCG", "expected x64, observed " + pe.machine);
  }
  if (pe.subsystem !== "windowsGui") {
    addGap(gaps, "releaseSubsystemMismatch", "C-RCG", "expected windowsGui, observed " + pe.subsystem);
  }
  if (!staticVcConfigured || dynamicVcRuntimeImports.length > 0) {
    addGap(
      gaps,
      "staticVcRuntimeEvidenceFailed",
      "C-RCG",
      "configured=" + staticVcConfigured + ", dynamic imports=" + dynamicVcRuntimeImports.join(","),
    );
  }
  if (
    !bundledSqliteConfigured ||
    versions.rusqlite === "missing" ||
    versions["libsqlite3-sys"] === "missing" ||
    externalSqliteImports.length > 0
  ) {
    addGap(
      gaps,
      "bundledSqliteEvidenceFailed",
      "C-RCG",
      "configured=" + bundledSqliteConfigured + ", external imports=" + externalSqliteImports.join(","),
    );
  }
  if (
    webviewMode !== "downloadBootstrapper" ||
    generatedWebviewMode !== "downloadBootstrapper" ||
    !webviewDownloadUrl ||
    !webviewDownloadFailureAborts
  ) {
    addGap(
      gaps,
      "webView2ProvisioningEvidenceFailed",
      "C-RCG",
      "config=" + webviewMode + ", generated=" + generatedWebviewMode,
    );
  }
  if (generatedArchitecture !== "x64" || generatedInstallMode !== "currentUser") {
    addGap(
      gaps,
      "generatedNsisBaselineMismatch",
      "C-RCG",
      "arch=" + generatedArchitecture + ", installMode=" + generatedInstallMode,
    );
  }
  if (
    generatedInstallRoot.toLowerCase() !==
    installProbe.contract.targetInstallRoot.toLowerCase()
  ) {
    addGap(
      gaps,
      "generatedInstallRootMismatch",
      "C-RCG",
      "expected " + installProbe.contract.targetInstallRoot + ", generated " + generatedInstallRoot,
    );
  }
  if (!generatedResourcePayload) {
    addGap(
      gaps,
      "generatedResourcePayloadMissing",
      "C-RCG",
      "generated NSIS does not contain INSTALLATION_SCOPE.txt",
    );
  }
  if (!directoryHasFiles(resourcesDirectory)) {
    addGap(gaps, "resourcePayloadMissing", "C-RCG", "src-tauri/resources/ is absent or empty");
  }
  if (cleanupValueName !== expectedAutostartValue) {
    addGap(
      gaps,
      "autostartCleanupValueNameMismatch",
      "C-URV",
      "contract=" + expectedAutostartValue + ", generated NSIS=" + cleanupValueName,
    );
  }

  const dependencyEvidenceCodes = new Set([
    "releaseArchitectureMismatch",
    "releaseSubsystemMismatch",
    "staticVcRuntimeEvidenceFailed",
    "bundledSqliteEvidenceFailed",
    "webView2ProvisioningEvidenceFailed",
    "generatedNsisBaselineMismatch",
    "generatedInstallRootMismatch",
    "generatedResourcePayloadMissing",
  ]);
  const dependencyEvidencePassed = !gaps.some((gap) =>
    dependencyEvidenceCodes.has(gap.code),
  );
  const allGapsAssigned = gaps.every((gap) => Boolean(gap.owner));
  const handoffReady = dependencyEvidencePassed && allGapsAssigned;
  const releaseReady = !gaps.some((gap) => gap.severity === "error");
  return {
    probeVersion: 1,
    probeName: PROBE_NAME,
    status: releaseReady
      ? "contractPackagingReady"
      : handoffReady
        ? "handoffReadyWithAssignedGaps"
        : "handoffBlocked",
    dependencyEvidenceStatus: dependencyEvidencePassed ? "pass" : "fail",
    releaseReadiness: releaseReady ? "ready" : "notReleaseReady",
    releaseCertification: "notCertifiedUntilC-RCGMatricesPass",
    artifacts: {
      releaseExecutable: artifact(releasePath),
      nsisInstaller: artifact(installerPath),
      generatedNsis: artifact(generatedNsis),
    },
    releasePe: {
      ...pe,
      dynamicVcRuntimeImports,
      externalSqliteImports,
    },
    dependencies: {
      lockedVersions: versions,
      webView2: {
        configuredMode: webviewMode,
        generatedMode: generatedWebviewMode,
        downloadUrl: webviewDownloadUrl,
        downloadFailureAbortsInstall: webviewDownloadFailureAborts,
        networkMayBeRequiredWhenMissing: true,
        evidence: "tauriConfigAndGeneratedNsis",
      },
      vcRuntime: {
        staticConfigured: staticVcConfigured,
        dynamicRuntimeImports: dynamicVcRuntimeImports,
        evidence: "cargoEnvironmentAndPeImportTable",
      },
      sqlite: {
        bundledConfigured: bundledSqliteConfigured,
        rusqliteVersion: versions.rusqlite,
        libsqlite3SysVersion: versions["libsqlite3-sys"],
        externalDllImports: externalSqliteImports,
        evidence: "cargoManifestLockAndPeImportTable",
      },
    },
    generatedInstaller: {
      architecture: generatedArchitecture,
      installMode: generatedInstallMode,
      installRoot: generatedInstallRoot,
      resourcePayloadPresent: generatedResourcePayload,
      licensePath: generatedLicense,
      autostartCleanupToken: cleanupToken,
      autostartCleanupValueName: cleanupValueName,
      expectedAutostartValueName: expectedAutostartValue,
    },
    licenseEvidence: {
      distributionScope: "privateUse",
      licensePayloadRequired: false,
      status: "notRequiredForPrivateUse",
      packageLicense: packageLicenseValue,
      licensesDirectory: licenseDirectory,
      licensesDirectoryReady: directoryHasFiles(licenseDirectory),
      resourcesDirectory,
      resourcesDirectoryReady: directoryHasFiles(resourcesDirectory),
      thirdPartyNoticePath: noticePath,
      generatedNsisLicensePath: generatedLicense,
    },
    limitations: [
      "WebView2 downloadBootstrapper requires network only when the Evergreen runtime is missing.",
      "PE import inspection proves the absence of dynamic VC and SQLite DLL imports; it is not a software composition or license inventory.",
      "The current NSIS payload remains a development artifact and has not passed clean-machine install, upgrade, uninstall, signing, or recovery certification.",
    ],
    handoff: [
      {
        owner: "C-RLC",
        inputs: [
          "consume autostart-contract-v1",
          "implement explicit opt-in enable/disable only for the formal installed executable",
          "implement first/secondary-instance --autostart behavior without opening the control center",
        ],
      },
      {
        owner: "C-UGM",
        inputs: [
          "move roaming authority only through versioned backup, atomic switch, source retention, and rollback",
          "do not treat the inactive LocalAppData target as current authority",
        ],
      },
      {
        owner: "C-URV",
        inputs: [
          "provide the independent recovery-aware uninstaller",
          "remove the exact DesktopUp HKCU Run value even after partial restore failure",
          "retain the exact DesktopUp cleanup value across future NSIS template updates",
        ],
      },
      {
        owner: "C-RCG",
        inputs: [
          "keep this contract packaging gate at zero gaps",
          "publish build manifest, hashes, signing evidence, and clean-machine matrices",
          "decide and verify the offline WebView2 release policy",
          "reactivate license review if distribution scope changes beyond private use",
        ],
      },
    ],
    sideEffects: {
      writesFiles: false,
      writesRegistry: false,
      installsOrUninstalls: false,
      launchesProcesses: false,
      requestsElevation: false,
    },
    gaps,
  };
}

function main() {
  const report = probeRuntimeDependencies({
    repoRoot: argumentValue("--root") ?? process.cwd(),
    nsisTemplatePath: argumentValue("--nsis-template") ?? "",
    generatedNsisPath: argumentValue("--generated-nsis") ?? "",
  });
  process.stdout.write(JSON.stringify(report, null, 2) + "\n");
  if (
    process.argv.includes("--require-handoff-ready") &&
    !["handoffReadyWithAssignedGaps", "contractPackagingReady"].includes(report.status)
  ) {
    process.exitCode = 2;
  }
  if (
    process.argv.includes("--require-release-ready") &&
    report.releaseReadiness !== "ready"
  ) {
    process.exitCode = 3;
  }
}

const invokedPath = process.argv[1] ? resolve(process.argv[1]) : "";
if (invokedPath === resolve(fileURLToPath(import.meta.url))) {
  main();
}

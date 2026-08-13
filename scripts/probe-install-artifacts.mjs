import { createHash } from "node:crypto";
import {
  existsSync,
  readFileSync,
  readdirSync,
  statSync,
} from "node:fs";
import { basename, join, resolve } from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const PROBE_NAME = "install-artifact-probe-v1";

function argumentValue(name) {
  const index = process.argv.indexOf(name);
  return index >= 0 ? process.argv[index + 1] : undefined;
}

function readText(path) {
  return readFileSync(path, "utf8");
}

function sha256(path) {
  const hash = createHash("sha256");
  hash.update(readFileSync(path));
  return hash.digest("hex").toUpperCase();
}

function artifact(path) {
  if (!path || !existsSync(path) || !statSync(path).isFile()) {
    return {
      path: path ?? "",
      exists: false,
      sizeBytes: 0,
      sha256: "",
    };
  }
  const stats = statSync(path);
  return {
    path,
    exists: true,
    sizeBytes: stats.size,
    sha256: sha256(path),
  };
}

function rustString(source, constantName) {
  const pattern = new RegExp(
    "pub const " + constantName + ': &str = r?"([^"]+)";',
  );
  const match = source.match(pattern);
  if (!match) {
    throw new Error("missing Rust contract constant " + constantName);
  }
  return match[1];
}

function requiredEntries(source) {
  const block = source.match(/required_install_entries:\s*vec!\[([\s\S]*?)\]/);
  if (!block) {
    throw new Error("missing required_install_entries contract");
  }
  return [...block[1].matchAll(/"([^"]+)"/g)].map((match) => match[1]);
}

function cargoPackageName(source) {
  const packageBlock = source.match(/\[package\]([\s\S]*?)(?:\n\[|$)/);
  const name = packageBlock?.[1].match(/^\s*name\s*=\s*"([^"]+)"/m)?.[1];
  if (!name) {
    throw new Error("missing Cargo package name");
  }
  return name;
}

function configuredBinaryName(config, cargoName) {
  return config.mainBinaryName || cargoName;
}

function findNsisTemplate(explicitPath, configuredPath, tauriRoot) {
  if (explicitPath) {
    return resolve(explicitPath);
  }
  if (configuredPath) {
    return resolve(tauriRoot, configuredPath);
  }
  const cargoHome = process.env.CARGO_HOME
    ? resolve(process.env.CARGO_HOME)
    : process.env.USERPROFILE
      ? join(process.env.USERPROFILE, ".cargo")
      : "";
  const registryRoot = cargoHome ? join(cargoHome, "registry", "src") : "";
  if (!registryRoot || !existsSync(registryRoot)) {
    return "";
  }
  const candidates = [];
  for (const registry of readdirSync(registryRoot, { withFileTypes: true })) {
    if (!registry.isDirectory()) {
      continue;
    }
    const registryPath = join(registryRoot, registry.name);
    for (const crate of readdirSync(registryPath, { withFileTypes: true })) {
      if (!crate.isDirectory() || !crate.name.startsWith("tauri-bundler-")) {
        continue;
      }
      const template = join(
        registryPath,
        crate.name,
        "src",
        "bundle",
        "windows",
        "nsis",
        "installer.nsi",
      );
      if (existsSync(template)) {
        candidates.push(template);
      }
    }
  }
  return candidates.sort().at(-1) ?? "";
}

function findInstaller(nsisDirectory) {
  if (!existsSync(nsisDirectory)) {
    return "";
  }
  const candidates = readdirSync(nsisDirectory)
    .filter((name) => name.toLowerCase().endsWith(".exe"))
    .map((name) => join(nsisDirectory, name))
    .filter((path) => statSync(path).isFile())
    .sort();
  return (
    candidates.find((path) => basename(path).toLowerCase() === "desktopupsetup.exe") ??
    candidates.at(-1) ??
    ""
  );
}

function currentUserInstallRoot(templateSource, productName) {
  const suffix = templateSource.match(
    /StrCpy\s+\$INSTDIR\s+"\$LOCALAPPDATA\\([^"]+)"/,
  )?.[1];
  if (!suffix) {
    return "%LOCALAPPDATA%\\" + productName;
  }
  return (
    "%LOCALAPPDATA%\\" + suffix.replaceAll("$" + "{PRODUCTNAME}", productName)
  );
}

function bundlePlansDirectory(bundleResources, directory) {
  if (!bundleResources) {
    return false;
  }
  const values = Array.isArray(bundleResources)
    ? bundleResources
    : Object.keys(bundleResources);
  const prefix = directory.toLowerCase() + "/";
  return values.some((value) => {
    const normalized = String(value).replaceAll("\\", "/").toLowerCase();
    return normalized === directory.toLowerCase() || normalized.startsWith(prefix);
  });
}

function addGap(gaps, code, owner, detail, severity = "error") {
  gaps.push({ code, severity, owner, detail });
}

export function probeInstallArtifacts({
  repoRoot,
  nsisTemplatePath = "",
}) {
  const root = resolve(repoRoot);
  const tauriRoot = join(root, "src-tauri");
  const config = JSON.parse(readText(join(tauriRoot, "tauri.conf.json")));
  const cargoSource = readText(join(tauriRoot, "Cargo.toml"));
  const baselineSource = readText(join(tauriRoot, "src", "install_baseline.rs"));
  const releasePolicy = readText(
    join(root, "Docs", "Rules", "RELEASE_AND_UNINSTALL_POLICY.md"),
  );
  const cargoName = cargoPackageName(cargoSource);
  const binaryName = configuredBinaryName(config, cargoName);
  const targetInstallRoot = rustString(baselineSource, "INSTALL_ROOT");
  const entries = requiredEntries(baselineSource);
  const expectedExecutableName = entries[0] ?? "DesktopUp.exe";
  const requiredFinalInstallerName =
    releasePolicy.match(/\bDesktopUpSetup\.exe\b/)?.[0] ?? "DesktopUpSetup.exe";
  const releaseDirectory = join(tauriRoot, "target", "release");
  const releaseExecutablePath = join(releaseDirectory, binaryName + ".exe");
  const installerPath = findInstaller(join(releaseDirectory, "bundle", "nsis"));
  const templatePath = findNsisTemplate(
    nsisTemplatePath,
    config.bundle?.windows?.nsis?.template ?? "",
    tauriRoot,
  );
  const templateSource =
    templatePath && existsSync(templatePath) ? readText(templatePath) : "";
  const installMode = config.bundle?.windows?.nsis?.installMode ?? "currentUser";
  const productName = config.productName ?? cargoName;
  const plannedInstallRoot =
    installMode === "currentUser"
      ? currentUserInstallRoot(templateSource, productName)
      : "notEvaluatedForMode:" + installMode;
  const requestExecutionLevelUser =
    installMode === "currentUser" &&
    /RequestExecutionLevel\s+user/.test(templateSource);
  const uninstallerGenerationPlanned =
    /WriteUninstaller\s+"\$INSTDIR\\uninstall\.exe"/.test(templateSource);
  const bundleResources = config.bundle?.resources;
  const resourcesDirectoryPlanned = bundlePlansDirectory(
    bundleResources,
    "resources",
  );
  const licensesDirectoryPlanned = bundlePlansDirectory(
    bundleResources,
    "licenses",
  );
  const gaps = [];

  if (installMode !== "currentUser") {
    addGap(gaps, "installScopeMismatch", "C-RCG", "expected currentUser, observed " + installMode);
  }
  if (!requestExecutionLevelUser) {
    addGap(gaps, "elevationContractUnverified", "C-RCG", "local Tauri NSIS template did not prove RequestExecutionLevel user");
  }
  if (plannedInstallRoot.toLowerCase() !== targetInstallRoot.toLowerCase()) {
    addGap(gaps, "installRootMismatch", "C-RCG", "expected " + targetInstallRoot + ", planned " + plannedInstallRoot);
  }
  if (basename(releaseExecutablePath).toLowerCase() !== expectedExecutableName.toLowerCase()) {
    addGap(gaps, "mainExecutableNameMismatch", "C-RCG", "expected " + expectedExecutableName + ", observed " + basename(releaseExecutablePath));
  }
  if (!resourcesDirectoryPlanned) {
    addGap(gaps, "resourcesDirectoryNotBundled", "C-RCG", "bundle.resources does not include resources/");
  }
  if (!uninstallerGenerationPlanned) {
    addGap(gaps, "uninstallerGenerationUnverified", "C-URV", "local Tauri NSIS template did not prove uninstall.exe generation");
  }
  if (!installerPath) {
    addGap(gaps, "installerArtifactMissing", "C-RCG", "no NSIS executable artifact found");
  } else if (basename(installerPath).toLowerCase() !== requiredFinalInstallerName.toLowerCase()) {
    addGap(gaps, "finalInstallerNameMismatch", "C-RCG", "expected " + requiredFinalInstallerName + ", observed " + basename(installerPath));
  }
  if (!existsSync(releaseExecutablePath)) {
    addGap(gaps, "releaseExecutableMissing", "C-RCG", releaseExecutablePath);
  }

  const errorCount = gaps.filter((gap) => gap.severity === "error").length;
  return {
    probeVersion: 1,
    probeName: PROBE_NAME,
    status: errorCount === 0 ? "ready" : "gapsDetected",
    releaseReadiness: errorCount === 0 ? "ready" : "notReleaseReady",
    contract: {
      installScope: "currentUser",
      elevationRequired: false,
      targetInstallRoot,
      requiredInstallEntries: entries,
      requiredFinalInstallerName,
      runtimeInstallWritesAllowed: false,
      distributionScope: "privateUse",
      licensePayloadRequired: false,
    },
    plannedInstallerBehavior: {
      installMode,
      plannedInstallRoot,
      requestExecutionLevelUser,
      uninstallerGenerationPlanned,
      resourcesDirectoryPlanned,
      licensesDirectoryPlanned,
      recoveryBehaviorVerified: false,
      recoveryBehaviorOwner: "C-URV",
    },
    artifacts: {
      releaseExecutable: artifact(releaseExecutablePath),
      nsisInstaller: artifact(installerPath),
    },
    inspection: {
      method: "tauriConfigAndLocalNsisTemplate",
      nsisTemplatePath: templatePath,
      compressedPayloadExtracted: false,
      compressedPayloadContentsIndependentlyVerified: false,
      limitation:
        "NSIS payload contents are inferred from Tauri configuration and the locally available build-tool template; the compressed payload was not independently extracted.",
    },
    sideEffects: {
      writesInstallRoot: false,
      createsDirectories: false,
      installsOrUninstalls: false,
      requestsElevation: false,
    },
    gaps,
  };
}

function main() {
  const repoRoot = argumentValue("--root") ?? process.cwd();
  const report = probeInstallArtifacts({
    repoRoot,
    nsisTemplatePath: argumentValue("--nsis-template") ?? "",
  });
  process.stdout.write(JSON.stringify(report, null, 2) + "\n");
  if (process.argv.includes("--require-ready") && report.status !== "ready") {
    process.exitCode = 2;
  }
}

const invokedPath = process.argv[1] ? resolve(process.argv[1]) : "";
if (invokedPath === resolve(fileURLToPath(import.meta.url))) {
  main();
}

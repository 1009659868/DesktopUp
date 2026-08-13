import { spawn } from "node:child_process";
import {
  existsSync,
  mkdtempSync,
  mkdirSync,
  readFileSync,
  rmSync
} from "node:fs";
import { tmpdir } from "node:os";
import { basename, dirname, isAbsolute, join, relative, resolve, sep } from "node:path";

const CONTRACT_VERSION = "poster-state-recovery-gate-v1";
const TEMPORARY_PREFIX = "desktop-up-cpss05-";
const REPORT_ARGUMENT_PREFIX = "--cpss-recovery-gate-report=";
const EXPECTED_SCENARIOS = [
  "restart-revision-and-schema-bootstrap",
  "damaged-content-rejections",
  "schema-and-database-identity-rejections",
  "unreadable-and-query-failures",
  "cas-conflict-and-validation-preserve",
  "transactional-write-failure-rollback",
  "legacy-backup-and-idempotence",
  "legacy-failure-source-retention"
];

const releaseExecutable = resolve("src-tauri/target/release/desktop-up.exe");
if (!existsSync(releaseExecutable)) {
  throw new Error(`Release executable was not found: ${releaseExecutable}`);
}

const temporaryBase = resolve(tmpdir());
const temporaryRoot = mkdtempSync(join(temporaryBase, TEMPORARY_PREFIX));
const reportPath = join(temporaryRoot, "poster-state-recovery-gate-v1.json");
const isolatedAppData = join(temporaryRoot, "appdata");
const isolatedLocalAppData = join(temporaryRoot, "localappdata");
mkdirSync(isolatedAppData);
mkdirSync(isolatedLocalAppData);

function assertSafeTemporaryRoot(path) {
  const resolvedPath = resolve(path);
  const pathFromTemporaryBase = relative(temporaryBase, resolvedPath);
  if (
    !isAbsolute(resolvedPath) ||
    dirname(resolvedPath) !== temporaryBase ||
    !basename(resolvedPath).startsWith(TEMPORARY_PREFIX) ||
    pathFromTemporaryBase.startsWith(`..${sep}`) ||
    pathFromTemporaryBase === ".." ||
    isAbsolute(pathFromTemporaryBase)
  ) {
    throw new Error(`Refusing to clean an unsafe recovery directory: ${resolvedPath}`);
  }
}

let report;
let exitCode;
let signal;
try {
  const child = spawn(
    releaseExecutable,
    [`${REPORT_ARGUMENT_PREFIX}${reportPath}`],
    {
      windowsHide: true,
      stdio: "ignore",
      env: {
        ...process.env,
        APPDATA: isolatedAppData,
        LOCALAPPDATA: isolatedLocalAppData
      }
    }
  );
  [exitCode, signal] = await new Promise((resolveExit, reject) => {
    child.once("error", reject);
    child.once("exit", (code, exitSignal) => resolveExit([code, exitSignal]));
  });

  if (!existsSync(reportPath)) {
    throw new Error(
      `Recovery report was not written (exit=${exitCode}, signal=${signal})`
    );
  }
  report = JSON.parse(readFileSync(reportPath, "utf8"));
  if (report.contractVersion !== CONTRACT_VERSION) {
    throw new Error(`Unexpected recovery report contract: ${report.contractVersion}`);
  }
  if (report.isolated !== true) {
    throw new Error("Recovery report did not confirm isolated execution");
  }
  const actualScenarioIds = (report.scenarios ?? []).map(({ id }) => id);
  if (JSON.stringify(actualScenarioIds) !== JSON.stringify(EXPECTED_SCENARIOS)) {
    throw new Error(
      `Recovery scenario set drifted: ${JSON.stringify(actualScenarioIds)}`
    );
  }
  if (report.passed !== true || exitCode !== 0) {
    throw new Error(
      `Recovery matrix failed (exit=${exitCode}, violations=${JSON.stringify(
        report.violations ?? []
      )})`
    );
  }

  process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
} finally {
  assertSafeTemporaryRoot(temporaryRoot);
  rmSync(temporaryRoot, { recursive: true, force: true });
}

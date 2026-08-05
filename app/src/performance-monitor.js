import { PERFORMANCE_MODES } from "./poster-runtime.js";

export function normalizePerformanceMode(value) {
  return PERFORMANCE_MODES.includes(value) ? value : "auto";
}

export function formatMemoryBytes(bytes) {
  if (bytes == null) return "--";
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function formatCpuMs(cpuMs) {
  if (cpuMs == null) return "--";
  return `${Math.round(cpuMs)} ms`;
}

export function performanceModeLabel(mode) {
  switch (normalizePerformanceMode(mode)) {
    case "activeStatic":
      return "静态";
    case "activeAnimated":
      return "动态 30 FPS";
    case "paused":
      return "已暂停";
    case "stopped":
      return "已停止";
    default:
      return "自动";
  }
}

export function isReportingFps(mode, paused, hidden = false) {
  return normalizePerformanceMode(mode) === "activeAnimated" && !paused && !hidden;
}

export function buildPerformanceSample(snapshot, mode, sampledAt = new Date().toISOString()) {
  return {
    mode: normalizePerformanceMode(mode),
    memoryBytes: Number(snapshot?.memoryBytes ?? 0),
    processCount: Number(snapshot?.processCount ?? 0),
    posterFps: Number(snapshot?.posterFps ?? 0),
    cpuMs: Number(snapshot?.cpuMs ?? 0),
    pauseState: snapshot?.pauseState ?? "unknown",
    elapsedMs: Number(snapshot?.elapsedMs ?? 0),
    sampledAt
  };
}

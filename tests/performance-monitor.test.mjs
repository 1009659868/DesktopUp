import test from "node:test";
import assert from "node:assert/strict";
import {
  buildPerformanceSample,
  formatCpuMs,
  formatMemoryBytes,
  isReportingFps,
  normalizePerformanceMode,
  performanceModeLabel
} from "../app/src/performance-monitor.js";

test("performance mode normalization accepts only the four sampler states plus auto", () => {
  assert.equal(normalizePerformanceMode("activeStatic"), "activeStatic");
  assert.equal(normalizePerformanceMode("activeAnimated"), "activeAnimated");
  assert.equal(normalizePerformanceMode("paused"), "paused");
  assert.equal(normalizePerformanceMode("stopped"), "stopped");
  assert.equal(normalizePerformanceMode("auto"), "auto");
  assert.equal(normalizePerformanceMode("unknown"), "auto");
});

test("performance formatting produces simple human readable values", () => {
  assert.equal(formatMemoryBytes(1024 * 1024 * 92.4), "92.4 MB");
  assert.equal(formatMemoryBytes(null), "--");
  assert.equal(formatCpuMs(88), "88 ms");
  assert.equal(formatCpuMs(null), "--");
});

test("FPS reporting only runs for the animated sampler mode", () => {
  assert.equal(isReportingFps("activeAnimated", false), true);
  assert.equal(isReportingFps("activeStatic", false), false);
  assert.equal(isReportingFps("activeAnimated", true), false);
  assert.equal(isReportingFps("activeAnimated", false, true), false);
  assert.equal(isReportingFps("auto", false), false);
});

test("performance sample records the canonical snapshot fields", () => {
  const sample = buildPerformanceSample({
    memoryBytes: 1024 * 1024 * 50,
    processCount: 3,
    posterFps: 30,
    cpuMs: 12,
    pauseState: "activeAnimated",
    elapsedMs: 99
  }, "activeAnimated");
  assert.equal(sample.mode, "activeAnimated");
  assert.equal(sample.memoryBytes, 1024 * 1024 * 50);
  assert.equal(sample.processCount, 3);
  assert.equal(sample.posterFps, 30);
  assert.equal(sample.cpuMs, 12);
  assert.equal(sample.pauseState, "activeAnimated");
  assert.equal(sample.elapsedMs, 99);
});

test("performance labels map the four baseline states", () => {
  assert.equal(performanceModeLabel("activeStatic"), "静态");
  assert.equal(performanceModeLabel("activeAnimated"), "动态 30 FPS");
  assert.equal(performanceModeLabel("paused"), "已暂停");
  assert.equal(performanceModeLabel("stopped"), "已停止");
});

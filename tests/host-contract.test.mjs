import test from "node:test";
import assert from "node:assert/strict";
import {
  HOST_CONTRACT_VERSION,
  HOST_GEOMETRY_OWNER,
  createHostSurfaceRequest,
  isCompatibleHostSnapshot
} from "../app/src/host-contract.js";
import { buildHostSurfaceSyncArgs } from "../app/src/poster-runtime.js";
import { createDefaultSnapshot } from "../app/src/poster-core.js";

test("host surface request has a versioned native-owned contract", () => {
  const request = createHostSurfaceRequest({
    revision: 7,
    logicalSize: { width: 510, height: 412 },
    scaleFactor: 1.25,
    placementIntent: { kind: "preserve" },
    interactionRegion: {
      regionId: "poster-night-ops",
      rect: { x: 10, y: 10, width: 490, height: 392 },
      cornerRadius: 18
    }
  });

  assert.equal(request.contractVersion, HOST_CONTRACT_VERSION);
  assert.equal(request.revision, 7);
  assert.equal(request.userScale, 1);
  assert.deepEqual(request.placementIntent, { kind: "preserve" });
  assert.equal(request.interactionRegion.regionId, "poster-night-ops");
});

test("display identifiers stay opaque so Windows monitor names round-trip", () => {
  const request = createHostSurfaceRequest({
    revision: 1,
    logicalSize: { width: 100, height: 100 },
    placementIntent: {
      kind: "anchor",
      displayId: String.raw`\\.\DISPLAY1`,
      normalizedX: 1,
      normalizedY: 0
    }
  });
  assert.equal(request.placementIntent.displayId, String.raw`\\.\DISPLAY1`);
});

test("host surface request rejects invalid versions, scales, and regions", () => {
  assert.throws(() => createHostSurfaceRequest({
    contractVersion: 2,
    revision: 1,
    logicalSize: { width: 100, height: 100 }
  }), /unsupported host contract version/);
  assert.throws(() => createHostSurfaceRequest({
    revision: 1,
    logicalSize: { width: 100, height: 100 },
    scaleFactor: Number.NaN
  }), /scaleFactor/);
  assert.throws(() => createHostSurfaceRequest({
    revision: 1,
    logicalSize: { width: 100, height: 100 },
    interactionRegion: {
      regionId: "poster-primary",
      rect: { x: 20, y: 20, width: 90, height: 80 },
      cornerRadius: 8
    }
  }), /fit inside/);
  assert.throws(() => createHostSurfaceRequest({
    revision: 1,
    logicalSize: { width: 100, height: 100 },
    userScale: 4
  }), /userScale/);
});

test("poster adapter submits one canonical host surface request", () => {
  const poster = createDefaultSnapshot().posters[0];
  const args = buildHostSurfaceSyncArgs(
    poster,
    { x: 10, y: 10, width: poster.width, height: poster.height },
    {
      revision: 3,
      surfaceGutter: 10,
      deviceScaleFactor: 1.25,
      cornerRadius: 18
    }
  );

  assert.equal(args.request.revision, 3);
  assert.deepEqual(args.request.logicalSize, {
    width: poster.width + 20,
    height: poster.height + 20
  });
  assert.equal(args.request.interactionRegion.rect.width, poster.width);
  assert.equal(args.request.userScale, 1);
});

test("collapsed poster uses the target height instead of a stale expanded measurement", () => {
  const poster = {
    ...createDefaultSnapshot().posters[0],
    collapsed: true
  };
  const args = buildHostSurfaceSyncArgs(
    poster,
    { x: 10, y: 10, width: poster.width, height: poster.height },
    {
      revision: 4,
      surfaceGutter: 10,
      collapsedHeight: 116,
      deviceScaleFactor: 1.2,
      cornerRadius: 16
    }
  );

  assert.deepEqual(args.request.logicalSize, {
    width: poster.width + 20,
    height: 136
  });
  assert.deepEqual(args.request.interactionRegion.rect, {
    x: 10,
    y: 10,
    width: poster.width,
    height: 116
  });
});

test("expanded poster restores its full target height after collapse", () => {
  const poster = createDefaultSnapshot().posters[0];
  const args = buildHostSurfaceSyncArgs(
    poster,
    { x: 10, y: 10, width: poster.width, height: poster.height },
    {
      revision: 5,
      surfaceGutter: 10,
      collapsedHeight: 116,
      deviceScaleFactor: 1.2,
      cornerRadius: 16
    }
  );

  assert.equal(args.request.logicalSize.height, poster.height + 20);
  assert.equal(args.request.interactionRegion.rect.height, poster.height);
});

test("host snapshot compatibility requires the native geometry owner", () => {
  const compatible = {
    contractVersion: HOST_CONTRACT_VERSION,
    revision: 1,
    surfaceId: "poster-host",
    geometryOwner: HOST_GEOMETRY_OWNER,
    monitorScaleFactor: 1,
    deviceScaleFactor: 1.5,
    rendererScaleFactor: 1.5,
    responsiveScale: 1.25,
    userScale: 1,
    effectiveScaleFactor: 1.875
  };
  assert.equal(isCompatibleHostSnapshot(compatible), true);
  assert.equal(isCompatibleHostSnapshot({
    ...compatible,
    geometryOwner: "webview"
  }), false);
});

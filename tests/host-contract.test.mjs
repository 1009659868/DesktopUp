import test from "node:test";
import assert from "node:assert/strict";
import {
  HOST_CONTRACT_VERSION,
  HOST_GEOMETRY_OWNER,
  SURFACE_REGISTRY_VERSION,
  createHostSurfaceRequest,
  isCompatibleHostSnapshot
} from "../app/src/host-contract.js";
import { buildHostSurfaceSyncArgs } from "../app/src/poster-runtime.js";
import { createLegacyDefaultSnapshot } from "../app/src/poster-legacy-bridge.js";

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

test("surface registry supports zero or multiple overlapping regions on one poster", () => {
  const empty = createHostSurfaceRequest({
    revision: 9,
    logicalSize: { width: 300, height: 200 },
    surfaceRegistry: { registryVersion: 1, revision: 9, surfaces: [] }
  });
  assert.equal(empty.surfaceRegistry.registryVersion, SURFACE_REGISTRY_VERSION);
  assert.deepEqual(empty.surfaceRegistry.surfaces, []);

  const multiple = createHostSurfaceRequest({
    revision: 10,
    logicalSize: { width: 300, height: 200 },
    surfaceRegistry: {
      registryVersion: SURFACE_REGISTRY_VERSION,
      revision: 10,
      surfaces: [{
        surfaceId: "poster-a",
        visualRegion: {
          regionId: "poster-a-visual",
          rect: { x: 0, y: 0, width: 300, height: 200 },
          cornerRadius: 16
        },
        interactionRegions: [
          {
            regionId: "poster-a-header",
            rect: { x: 0, y: 0, width: 300, height: 64 },
            cornerRadius: 16
          },
          {
            regionId: "poster-a-tile",
            rect: { x: 120, y: 40, width: 180, height: 160 },
            cornerRadius: 12
          }
        ]
      }]
    }
  });
  assert.equal(multiple.surfaceRegistry.surfaces.length, 1);
  assert.equal(multiple.surfaceRegistry.surfaces[0].visualRegion.rect.width, 300);
  assert.equal(multiple.surfaceRegistry.surfaces[0].interactionRegions.length, 2);
});

test("surface registry rejects revision mismatch, duplicate ids, and legacy ambiguity", () => {
  const base = {
    revision: 11,
    logicalSize: { width: 200, height: 200 },
    surfaceRegistry: {
      registryVersion: SURFACE_REGISTRY_VERSION,
      revision: 11,
      surfaces: [{
        surfaceId: "poster-a",
        visualRegion: {
          regionId: "poster-a-visual",
          rect: { x: 0, y: 0, width: 200, height: 200 },
          cornerRadius: 8
        },
        interactionRegions: [{
          regionId: "poster-a-body",
          rect: { x: 0, y: 0, width: 100, height: 100 },
          cornerRadius: 8
        }]
      }]
    }
  };
  assert.throws(() => createHostSurfaceRequest({
    ...base,
    surfaceRegistry: { ...base.surfaceRegistry, revision: 12 }
  }), /must match/);
  assert.throws(() => createHostSurfaceRequest({
    ...base,
    surfaceRegistry: {
      ...base.surfaceRegistry,
      surfaces: [...base.surfaceRegistry.surfaces, base.surfaceRegistry.surfaces[0]]
    }
  }), /duplicate surfaceId/);
  assert.throws(() => createHostSurfaceRequest({
    ...base,
    interactionRegion: base.surfaceRegistry.surfaces[0].interactionRegions[0]
  }), /cannot combine/);
});
test("poster adapter submits one canonical host surface request", () => {
  const poster = createLegacyDefaultSnapshot().posters[0];
  const args = buildHostSurfaceSyncArgs(
    poster,
    { x: 10, y: 10, width: poster.width, height: poster.height },
    {
      revision: 3,
      surfaceGutter: 10,
      deviceScaleFactor: 1.25,
      cornerRadius: 18,
      interactionRegions: [
        {
          regionId: "poster-night-ops-header",
          rect: { x: 10, y: 10, width: poster.width, height: 64 },
          cornerRadius: 18
        },
        {
          regionId: "poster-night-ops-tile",
          rect: { x: 42, y: 92, width: 146, height: 100 },
          cornerRadius: 6
        }
      ]
    }
  );

  assert.equal(args.request.revision, 3);
  assert.deepEqual(args.request.logicalSize, {
    width: poster.width + 20,
    height: poster.height + 20
  });
  const registry = args.request.surfaceRegistry;
  assert.equal(args.request.interactionRegion, null);
  assert.equal(registry.registryVersion, SURFACE_REGISTRY_VERSION);
  assert.equal(registry.revision, args.request.revision);
  assert.equal(registry.surfaces[0].visualRegion.rect.width, poster.width);
  assert.equal(registry.surfaces[0].interactionRegions.length, 2);
  assert.equal(registry.surfaces[0].interactionRegions[0].rect.height, 64);
  assert.equal(args.request.userScale, 1);
});

test("collapsed poster uses the target height instead of a stale expanded measurement", () => {
  const poster = {
    ...createLegacyDefaultSnapshot().posters[0],
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
  assert.deepEqual(args.request.surfaceRegistry.surfaces[0].interactionRegions[0].rect, {
    x: 10,
    y: 10,
    width: poster.width,
    height: 116
  });
});

test("expanded poster restores its full target height after collapse", () => {
  const poster = createLegacyDefaultSnapshot().posters[0];
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
  assert.equal(
    args.request.surfaceRegistry.surfaces[0].interactionRegions[0].rect.height,
    poster.height
  );
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

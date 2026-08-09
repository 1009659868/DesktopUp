export const HOST_CONTRACT_VERSION = 1;
export const HOST_GEOMETRY_OWNER = "nativeHost";

const MAX_IDENTIFIER_LENGTH = 96;
const MAX_SURFACE_LOGICAL_EXTENT = 8192;

function assertIdentifier(field, value) {
  if (typeof value !== "string"
    || value.length === 0
    || value.length > MAX_IDENTIFIER_LENGTH
    || !/^[A-Za-z0-9._-]+$/.test(value)) {
    throw new TypeError(`${field} must be a safe non-empty identifier`);
  }
}

function assertOpaqueIdentifier(field, value) {
  if (typeof value !== "string"
    || value.length === 0
    || value.length > MAX_IDENTIFIER_LENGTH
    || /[\u0000-\u001F\u007F]/.test(value)) {
    throw new TypeError(`${field} must be a non-empty opaque identifier`);
  }
}

function assertFiniteRange(field, value, minimum, maximum) {
  if (!Number.isFinite(value) || value < minimum || value > maximum) {
    throw new RangeError(`${field} must be between ${minimum} and ${maximum}`);
  }
}

function normalizedPlacementIntent(intent = { kind: "preserve" }) {
  if (intent?.kind === "preserve") return { kind: "preserve" };
  if (intent?.kind !== "anchor") {
    throw new TypeError("placementIntent.kind must be preserve or anchor");
  }
  if (intent.displayId != null) {
    assertOpaqueIdentifier("placementIntent.displayId", intent.displayId);
  }
  assertFiniteRange("placementIntent.normalizedX", intent.normalizedX, 0, 1);
  assertFiniteRange("placementIntent.normalizedY", intent.normalizedY, 0, 1);
  return {
    kind: "anchor",
    displayId: intent.displayId ?? null,
    normalizedX: intent.normalizedX,
    normalizedY: intent.normalizedY
  };
}

function normalizedInteractionRegion(region, logicalSize) {
  if (region == null) return null;
  assertIdentifier("interactionRegion.regionId", region.regionId);
  const rect = {
    x: Number(region.rect?.x ?? 0),
    y: Number(region.rect?.y ?? 0),
    width: Number(region.rect?.width ?? 0),
    height: Number(region.rect?.height ?? 0)
  };
  assertFiniteRange("interactionRegion.rect.x", rect.x, 0, logicalSize.width);
  assertFiniteRange("interactionRegion.rect.y", rect.y, 0, logicalSize.height);
  assertFiniteRange("interactionRegion.rect.width", rect.width, Number.EPSILON, logicalSize.width);
  assertFiniteRange("interactionRegion.rect.height", rect.height, Number.EPSILON, logicalSize.height);
  if (rect.x + rect.width > logicalSize.width || rect.y + rect.height > logicalSize.height) {
    throw new RangeError("interactionRegion must fit inside logicalSize");
  }
  const cornerRadius = Number(region.cornerRadius ?? 0);
  assertFiniteRange("interactionRegion.cornerRadius", cornerRadius, 0, Math.min(rect.width, rect.height) / 2);
  return {
    regionId: region.regionId,
    rect,
    cornerRadius
  };
}

export function createHostSurfaceRequest(input = {}) {
  if (input.contractVersion != null && input.contractVersion !== HOST_CONTRACT_VERSION) {
    throw new RangeError(`unsupported host contract version ${input.contractVersion}`);
  }
  const revision = Number(input.revision ?? 1);
  if (!Number.isSafeInteger(revision) || revision <= 0) {
    throw new RangeError("revision must be a positive safe integer");
  }
  const surfaceId = input.surfaceId ?? "poster-host";
  assertIdentifier("surfaceId", surfaceId);
  const logicalSize = {
    width: Number(input.logicalSize?.width ?? 0),
    height: Number(input.logicalSize?.height ?? 0)
  };
  assertFiniteRange("logicalSize.width", logicalSize.width, 1, MAX_SURFACE_LOGICAL_EXTENT);
  assertFiniteRange("logicalSize.height", logicalSize.height, 1, MAX_SURFACE_LOGICAL_EXTENT);
  const scaleFactor = Number(input.scaleFactor ?? 1);
  assertFiniteRange("scaleFactor", scaleFactor, 0.5, 4);
  const userScale = Number(input.userScale ?? 1);
  assertFiniteRange("userScale", userScale, 0.5, 2);

  return {
    contractVersion: HOST_CONTRACT_VERSION,
    revision,
    surfaceId,
    logicalSize,
    scaleFactor,
    userScale,
    placementIntent: normalizedPlacementIntent(input.placementIntent),
    interactionRegion: normalizedInteractionRegion(input.interactionRegion, logicalSize)
  };
}

export function isCompatibleHostSnapshot(snapshot) {
  return snapshot?.contractVersion === HOST_CONTRACT_VERSION
    && snapshot?.geometryOwner === HOST_GEOMETRY_OWNER
    && typeof snapshot?.surfaceId === "string"
    && Number.isSafeInteger(snapshot?.revision)
    && Number.isFinite(snapshot?.monitorScaleFactor)
    && Number.isFinite(snapshot?.deviceScaleFactor)
    && Number.isFinite(snapshot?.rendererScaleFactor)
    && Number.isFinite(snapshot?.responsiveScale)
    && Number.isFinite(snapshot?.userScale)
    && Number.isFinite(snapshot?.effectiveScaleFactor);
}

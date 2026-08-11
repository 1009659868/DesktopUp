export const POSTER_GEOMETRY_CONTRACT_VERSION = 1;

export const POSTER_GEOMETRY_POLICY = Object.freeze({
  contractVersion: POSTER_GEOMETRY_CONTRACT_VERSION,
  safeInsets: Object.freeze({ top: 24, right: 24, bottom: 24 }),
  defaultWidthRatio: 0.22,
  minimumDefaultWidth: 280,
  minimumWidth: 240,
  maximumWidthRatio: 0.45,
  minimumExpandedHeight: 240,
  collapsedContentHeight: 60,
  surfaceGutter: 10,
  collapsedHostHeight: 80
});

function geometryError(path, code, message) {
  return { path, code, message };
}

function finitePositive(value) {
  const number = Number(value);
  return Number.isFinite(number) && number > 0 ? number : null;
}

function normalizedAxis(position, availableSpan) {
  if (availableSpan <= 0) return 0;
  return Math.min(1, Math.max(0, position / availableSpan));
}

export function resolveDefaultPosterGeometry(primaryWorkArea) {
  const errors = [];
  const displayId = typeof primaryWorkArea?.displayId === "string"
    ? primaryWorkArea.displayId.trim()
    : "";
  if (!displayId) {
    errors.push(geometryError(
      "$.displayId",
      "display_id_required",
      "The primary display must provide its opaque host displayId."
    ));
  }
  if (primaryWorkArea?.isPrimary !== true) {
    errors.push(geometryError(
      "$.isPrimary",
      "primary_display_required",
      "The V1 default poster geometry must be resolved from the primary display."
    ));
  }

  const workWidth = finitePositive(primaryWorkArea?.width);
  const workHeight = finitePositive(primaryWorkArea?.height);
  if (workWidth == null) {
    errors.push(geometryError("$.width", "invalid_work_area_width", "Work-area width must be positive and finite."));
  }
  if (workHeight == null) {
    errors.push(geometryError("$.height", "invalid_work_area_height", "Work-area height must be positive and finite."));
  }
  if (errors.length > 0) return { ok: false, value: null, errors };

  const maximumPosterWidth = Math.floor(workWidth * POSTER_GEOMETRY_POLICY.maximumWidthRatio);
  if (maximumPosterWidth < POSTER_GEOMETRY_POLICY.minimumWidth) {
    errors.push(geometryError(
      "$.width",
      "work_area_too_narrow",
      "45% of the work area is smaller than the 240px poster minimum width."
    ));
  }
  const expandedHeight = Math.floor(
    workHeight
      - POSTER_GEOMETRY_POLICY.safeInsets.top
      - POSTER_GEOMETRY_POLICY.safeInsets.bottom
  );
  if (expandedHeight < POSTER_GEOMETRY_POLICY.minimumExpandedHeight) {
    errors.push(geometryError(
      "$.height",
      "work_area_too_short",
      "The safe work-area height is smaller than the 240px expanded minimum."
    ));
  }
  if (errors.length > 0) return { ok: false, value: null, errors };

  const posterWidth = Math.min(
    Math.max(
      Math.round(workWidth * POSTER_GEOMETRY_POLICY.defaultWidthRatio),
      POSTER_GEOMETRY_POLICY.minimumDefaultWidth,
      POSTER_GEOMETRY_POLICY.minimumWidth
    ),
    maximumPosterWidth
  );
  const posterX = workWidth - POSTER_GEOMETRY_POLICY.safeInsets.right - posterWidth;
  const posterY = POSTER_GEOMETRY_POLICY.safeInsets.top;
  const hostOffsetX = posterX - POSTER_GEOMETRY_POLICY.surfaceGutter;
  const hostOffsetY = posterY - POSTER_GEOMETRY_POLICY.surfaceGutter;
  const expandedHostWidth = posterWidth + POSTER_GEOMETRY_POLICY.surfaceGutter * 2;
  const expandedHostHeight = expandedHeight + POSTER_GEOMETRY_POLICY.surfaceGutter * 2;
  const collapsedHostWidth = expandedHostWidth;
  const collapsedHostHeight = POSTER_GEOMETRY_POLICY.collapsedHostHeight;
  const normalizedX = normalizedAxis(hostOffsetX, workWidth - expandedHostWidth);
  const normalizedY = normalizedAxis(hostOffsetY, workHeight - expandedHostHeight);

  return {
    ok: true,
    errors: [],
    value: {
      contractVersion: POSTER_GEOMETRY_CONTRACT_VERSION,
      displayId,
      monitorIdentity: "primary",
      anchor: "top-right",
      normalizedRect: { x: normalizedX, y: normalizedY },
      posterRect: {
        x: posterX,
        y: posterY,
        width: posterWidth,
        height: expandedHeight
      },
      expandedHost: {
        offset: { x: hostOffsetX, y: hostOffsetY },
        logicalSize: { width: expandedHostWidth, height: expandedHostHeight }
      },
      collapsedHost: {
        offset: { x: hostOffsetX, y: hostOffsetY },
        logicalSize: { width: collapsedHostWidth, height: collapsedHostHeight }
      },
      initialPlacementIntent: {
        kind: "anchor",
        displayId,
        normalizedX,
        normalizedY
      },
      transitionPlacementIntent: { kind: "preserve" }
    }
  };
}

export function hostEnvelopeForPresentation(geometry, collapsedState) {
  if (collapsedState !== "expanded" && collapsedState !== "collapsed") {
    throw new TypeError("collapsedState must be expanded or collapsed.");
  }
  const source = collapsedState === "collapsed" ? geometry?.collapsedHost : geometry?.expandedHost;
  if (!source?.offset || !source?.logicalSize) {
    throw new TypeError("A resolved poster geometry is required.");
  }
  return {
    offset: { ...source.offset },
    logicalSize: { ...source.logicalSize },
    placementIntent: { kind: "preserve" }
  };
}

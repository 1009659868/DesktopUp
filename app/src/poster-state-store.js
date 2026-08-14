import {
  DEFAULT_TEMPLATE_KEY,
  createDefaultPosterSnapshot,
  validatePosterSnapshot
} from "./poster-contract.js";
import {
  createLegacyDefaultSnapshot,
  inspectLegacyPosterSource,
  prepareLegacyPosterMigration
} from "./poster-legacy-bridge.js";

export const POSTER_STATE_LOAD_COMMAND = "load_poster_state";
export const POSTER_STATE_REPLACE_COMMAND = "replace_poster_state";
export const POSTER_STATE_MIGRATE_COMMAND = "migrate_poster_state";
export const POSTER_STATE_CONTEXT_COMMAND = "poster_state_bootstrap_context";
export const POSTER_STATE_UPDATED_EVENT = "poster-state-updated";

export const DEFAULT_POSTER_TITLE = "NIGHT OPS";
export const DEFAULT_POSTER_SUBTITLE = "TACTICAL ARCHIVE / 01";

const PRIOR_FORMAL_DEFAULT_TITLE = "DESKTOP COLLECTION";
const PRIOR_FORMAL_DEFAULT_SUBTITLE = "";

export const BUILTIN_THEME_TEMPLATE_KEYS = Object.freeze({
  nocturne: DEFAULT_TEMPLATE_KEY,
  ember: "builtin-ember",
  glass: "builtin-glass"
});

const BUILTIN_TEMPLATE_THEMES = Object.freeze(Object.fromEntries(
  Object.entries(BUILTIN_THEME_TEMPLATE_KEYS).map(([theme, templateKey]) => [templateKey, theme])
));

export function runtimeThemeForTemplateKey(templateKey) {
  return BUILTIN_TEMPLATE_THEMES[templateKey] ?? "nocturne";
}

function cloneJson(value) {
  return JSON.parse(JSON.stringify(value));
}

function sameJsonValue(left, right) {
  if (Object.is(left, right)) return true;
  if (Array.isArray(left) || Array.isArray(right)) {
    return Array.isArray(left)
      && Array.isArray(right)
      && left.length === right.length
      && left.every((value, index) => sameJsonValue(value, right[index]));
  }
  if (!left || !right || typeof left !== "object" || typeof right !== "object") return false;
  const leftKeys = Object.keys(left);
  const rightKeys = Object.keys(right);
  return leftKeys.length === rightKeys.length
    && leftKeys.every((key) => Object.hasOwn(right, key) && sameJsonValue(left[key], right[key]));
}
const RUNTIME_MUTABLE_POSTER_FIELDS = Object.freeze([
  "title",
  "subtitle",
  "normalizedRect",
  "width",
  "height",
  "collapsedState",
  "autoCollapseEnabled",
  "isLocked",
  "layoutSpec",
  "templateKey"
]);

function prepareRuntimeMutation(snapshot, runtime, { primaryWorkArea } = {}) {
  const projection = projectRuntimeToPosterState(snapshot, runtime, { primaryWorkArea });
  if (!projection.ok) return projection;
  const currentPoster = snapshot.posters[0];
  const nextPoster = projection.snapshot.posters[0];
  const fields = {};
  for (const field of RUNTIME_MUTABLE_POSTER_FIELDS) {
    if (!sameJsonValue(currentPoster[field], nextPoster[field])) {
      fields[field] = cloneJson(nextPoster[field]);
    }
  }
  const selectedPosterId = snapshot.selectedPosterId === projection.snapshot.selectedPosterId
    ? null
    : projection.snapshot.selectedPosterId;
  return {
    ok: true,
    errors: [],
    overlay: {
      posterId: nextPoster.id,
      fields,
      selectedPosterId
    },
    empty: Object.keys(fields).length === 0 && selectedPosterId == null
  };
}

function applyRuntimeMutation(snapshot, mutation) {
  const next = cloneJson(snapshot);
  const poster = next.posters.find((entry) => entry.id === mutation.overlay.posterId);
  if (!poster) {
    return {
      ok: false,
      snapshot: null,
      errors: [{
        code: "mutation_poster_unavailable",
        message: "The pending poster mutation no longer has a target."
      }]
    };
  }
  Object.assign(poster, cloneJson(mutation.overlay.fields));
  if (mutation.overlay.selectedPosterId != null) {
    next.selectedPosterId = mutation.overlay.selectedPosterId;
  }
  const validation = validatePosterSnapshot(next);
  return {
    ok: validation.ok,
    snapshot: validation.value,
    errors: validation.errors
  };
}

export function isRecognizedPriorFormalDefault(snapshot, { primaryWorkArea } = {}) {
  const validation = validatePosterSnapshot(snapshot);
  if (!validation.ok) return false;
  try {
    const priorDefault = createDefaultPosterSnapshot({
      title: PRIOR_FORMAL_DEFAULT_TITLE,
      subtitle: PRIOR_FORMAL_DEFAULT_SUBTITLE,
      primaryWorkArea
    });
    return sameJsonValue(validation.value, priorDefault);
  } catch {
    return false;
  }
}

function clamp(value, minimum, maximum) {
  return Math.min(maximum, Math.max(minimum, value));
}

function isPositiveWorkArea(workArea) {
  return Boolean(workArea)
    && typeof workArea.displayId === "string"
    && Boolean(workArea.displayId.trim())
    && workArea.isPrimary === true
    && Number.isFinite(workArea.width)
    && Number.isFinite(workArea.height)
    && workArea.width > 0
    && workArea.height > 0;
}

export function previewPrimaryWorkArea(screenLike = globalThis.screen) {
  return {
    displayId: "browser-preview",
    isPrimary: true,
    width: Number(screenLike?.availWidth ?? 1920),
    height: Number(screenLike?.availHeight ?? 1080)
  };
}

function runtimePosition(poster, workArea) {
  if (!isPositiveWorkArea(workArea)) return { x: 0, y: 0 };
  const availableWidth = Math.max(1, workArea.width - poster.width);
  const availableHeight = Math.max(1, workArea.height - poster.height);
  return {
    x: clamp(Number(poster.normalizedRect?.x) || 0, 0, 1) * availableWidth,
    y: clamp(Number(poster.normalizedRect?.y) || 0, 0, 1) * availableHeight
  };
}

export function projectPosterStateToRuntime(snapshot, { primaryWorkArea } = {}) {
  const fallback = createLegacyDefaultSnapshot().posters[0];
  const poster = snapshot?.posters?.[0];
  if (!poster) return createEphemeralPosterRuntimeSnapshot();
  const position = runtimePosition(poster, primaryWorkArea);
  const legacyDensity = Number(poster.layoutSpec?.legacyDensity);
  const density = Number.isFinite(legacyDensity) ? clamp(legacyDensity, 0, 2) : fallback.density;
  return {
    contractVersion: snapshot.contractVersion,
    revision: snapshot.revision,
    selectedPosterId: poster.id,
    posters: [{
      ...cloneJson(fallback),
      id: poster.id,
      title: poster.title,
      subtitle: poster.subtitle,
      x: position.x,
      y: position.y,
      width: poster.width,
      height: poster.height,
      theme: runtimeThemeForTemplateKey(poster.templateKey),
      templateKey: poster.templateKey,
      collapsed: poster.collapsedState === "collapsed",
      locked: poster.isLocked,
      autoCollapse: poster.autoCollapseEnabled,
      density,
      items: cloneJson(fallback.items)
    }]
  };
}

export function createEphemeralPosterRuntimeSnapshot() {
  const fallback = createLegacyDefaultSnapshot();
  return {
    selectedPosterId: fallback.posters[0].id,
    posters: [cloneJson(fallback.posters[0])]
  };
}

function normalizedRectFromRuntime(runtimePoster, formalPoster, workArea) {
  if (!isPositiveWorkArea(workArea)) return cloneJson(formalPoster.normalizedRect);
  const availableWidth = Math.max(1, workArea.width - runtimePoster.width);
  const availableHeight = Math.max(1, workArea.height - runtimePoster.height);
  return {
    x: clamp(Number(runtimePoster.x) / availableWidth || 0, 0, 1),
    y: clamp(Number(runtimePoster.y) / availableHeight || 0, 0, 1)
  };
}

export function projectRuntimeToPosterState(snapshot, runtime, { primaryWorkArea } = {}) {
  const formalPoster = snapshot?.posters?.[0];
  const runtimePoster = runtime?.posters?.find((poster) => poster.id === formalPoster?.id)
    ?? runtime?.posters?.[0];
  if (!formalPoster || !runtimePoster) {
    return {
      ok: false,
      snapshot: null,
      errors: [{ code: "runtime_poster_unavailable", message: "No runtime poster can be persisted." }]
    };
  }

  const width = Math.max(1, Math.round(Number(runtimePoster.width)));
  const height = Math.max(1, Math.round(Number(runtimePoster.height)));
  const layoutSpec = cloneJson(formalPoster.layoutSpec);
  if (Number.isFinite(Number(runtimePoster.density))) {
    layoutSpec.legacyDensity = clamp(Math.round(Number(runtimePoster.density)), 0, 2);
  }
  const formalTheme = runtimeThemeForTemplateKey(formalPoster.templateKey);
  const runtimeTheme = Object.hasOwn(BUILTIN_THEME_TEMPLATE_KEYS, runtimePoster.theme)
    ? runtimePoster.theme
    : formalTheme;
  const templateKey = runtimeTheme === formalTheme
    ? formalPoster.templateKey
    : BUILTIN_THEME_TEMPLATE_KEYS[runtimeTheme];
  const nextPoster = {
    ...cloneJson(formalPoster),
    title: typeof runtimePoster.title === "string" && runtimePoster.title.trim()
      ? runtimePoster.title
      : formalPoster.title,
    subtitle: typeof runtimePoster.subtitle === "string"
      ? runtimePoster.subtitle
      : formalPoster.subtitle,
    normalizedRect: normalizedRectFromRuntime(
      { ...runtimePoster, width, height },
      formalPoster,
      primaryWorkArea
    ),
    width,
    height,
    collapsedState: runtimePoster.collapsed ? "collapsed" : "expanded",
    autoCollapseEnabled: Boolean(runtimePoster.autoCollapse),
    isLocked: Boolean(runtimePoster.locked),
    layoutSpec,
    templateKey
  };
  const next = {
    ...cloneJson(snapshot),
    selectedPosterId: nextPoster.id,
    posters: [nextPoster]
  };
  const validation = validatePosterSnapshot(next);
  return {
    ok: validation.ok,
    snapshot: validation.value,
    errors: validation.errors
  };
}

function rejected(code, message, details = null) {
  return { status: "rejected", code, message, details };
}

export class PosterStateClient {
  constructor({
    invoke,
    storage,
    primaryWorkArea,
    title = DEFAULT_POSTER_TITLE,
    subtitle = DEFAULT_POSTER_SUBTITLE
  } = {}) {
    this.invoke = invoke;
    this.storage = storage;
    this.primaryWorkArea = primaryWorkArea ?? null;
    this.title = title;
    this.subtitle = subtitle;
    this.mutationSequence = 0;
    this.pendingMutations = new Map();
    this.retainedMutation = null;
    this.snapshot = null;
    this.mutationQueue = Promise.resolve();
  }

  async ensureContext() {
    if (isPositiveWorkArea(this.primaryWorkArea)) return this.primaryWorkArea;
    if (typeof this.invoke !== "function") {
      this.primaryWorkArea = previewPrimaryWorkArea();
      return this.primaryWorkArea;
    }
    const context = await this.invoke(POSTER_STATE_CONTEXT_COMMAND);
    if (!isPositiveWorkArea(context?.primaryWorkArea)) {
      throw new TypeError("Native poster state context did not provide a valid primary work area.");
    }
    this.primaryWorkArea = cloneJson(context.primaryWorkArea);
    return this.primaryWorkArea;
  }

  acceptSnapshot(snapshot) {
    const validation = validatePosterSnapshot(snapshot);
    if (!validation.ok) return false;
    if (this.snapshot && validation.value.revision < this.snapshot.revision) return false;
    if (this.snapshot
        && validation.value.revision === this.snapshot.revision
        && !sameJsonValue(validation.value, this.snapshot)) {
      return false;
    }
    this.snapshot = validation.value;
    return true;
  }

  latestLocalMutation() {
    let latest = this.retainedMutation;
    for (const mutation of this.pendingMutations.values()) {
      if (!latest || mutation.sequence > latest.sequence) latest = mutation;
    }
    return latest;
  }

  localMutationStatus() {
    if (this.pendingMutations.size > 0) return "pending";
    return this.retainedMutation ? "retained" : "none";
  }

  runtimeSnapshot() {
    if (!this.snapshot) return createEphemeralPosterRuntimeSnapshot();
    const mutation = this.latestLocalMutation();
    const applied = mutation ? applyRuntimeMutation(this.snapshot, mutation) : null;
    const snapshot = applied?.ok ? applied.snapshot : this.snapshot;
    return projectPosterStateToRuntime(snapshot, { primaryWorkArea: this.primaryWorkArea });
  }

  ready(origin) {
    return {
      status: "ready",
      origin,
      snapshot: cloneJson(this.snapshot),
      runtimeSnapshot: this.runtimeSnapshot()
    };
  }

  async loadLatest(origin = "sqlite") {
    const loaded = await this.invoke(POSTER_STATE_LOAD_COMMAND);
    if (loaded?.status === "loaded" && this.acceptSnapshot(loaded.snapshot)) {
      return this.ready(origin);
    }
    if (loaded?.status === "rejected") {
      return rejected(loaded.code ?? "sqliteRejected", loaded.message ?? "SQLite state was rejected.", loaded);
    }
    if (loaded?.status === "noData") return { status: "noData" };
    return rejected("invalidLoadResult", "SQLite load returned an unsupported result.", loaded);
  }

  async createEmptyState(snapshot, origin) {
    const stored = await this.invoke(POSTER_STATE_REPLACE_COMMAND, {
      expectedRevision: null,
      snapshot
    });
    if (stored?.status === "stored" && this.acceptSnapshot(stored.snapshot)) {
      return this.ready(origin);
    }
    if (stored?.status === "conflict") return this.loadLatest("sqliteConflictWinner");
    return rejected(
      stored?.code ?? "initialStateRejected",
      stored?.message ?? "Initial poster state was rejected.",
      stored
    );
  }

  async recoverPriorFormalDefault(loaded) {
    if (loaded?.status !== "ready"
        || !isRecognizedPriorFormalDefault(loaded.snapshot, {
          primaryWorkArea: this.primaryWorkArea
        })) {
      return loaded;
    }

    const replacement = createDefaultPosterSnapshot({
      title: this.title,
      subtitle: this.subtitle,
      primaryWorkArea: this.primaryWorkArea
    });
    try {
      const stored = await this.invoke(POSTER_STATE_REPLACE_COMMAND, {
        expectedRevision: loaded.snapshot.revision,
        snapshot: replacement
      });
      if (stored?.status === "stored" && this.acceptSnapshot(stored.snapshot)) {
        return this.ready("formalDefaultIdentityRecovery");
      }
      if (stored?.status === "conflict") {
        return this.loadLatest("formalDefaultIdentityRecoveryConflictWinner");
      }
      return {
        ...loaded,
        recovery: {
          status: "deferred",
          code: stored?.code ?? "formalDefaultIdentityRecoveryRejected",
          message: stored?.message ?? "The prior formal default identity could not be recovered.",
          details: stored ?? null
        }
      };
    } catch (error) {
      return {
        ...loaded,
        recovery: {
          status: "deferred",
          code: error?.code ?? "formalDefaultIdentityRecoveryFailed",
          message: error?.message ?? String(error),
          details: error ?? null
        }
      };
    }
  }

  async bootstrap() {
    try {
      await this.ensureContext();
      if (typeof this.invoke !== "function") {
        this.acceptSnapshot(createDefaultPosterSnapshot({
          title: this.title,
          subtitle: this.subtitle,
          primaryWorkArea: this.primaryWorkArea
        }));
        return this.ready("browserPreviewEphemeral");
      }

      const loaded = await this.loadLatest();
      if (loaded.status === "ready") return this.recoverPriorFormalDefault(loaded);
      if (loaded.status !== "noData") return loaded;

      const source = await inspectLegacyPosterSource(this.storage, this.invoke);
      if (source.status === "rejected") {
        return rejected(source.code, source.message, source);
      }
      if (source.status === "loaded" && source.kind === "legacyA0") {
        const prepared = prepareLegacyPosterMigration(source.snapshot, {
          workArea: this.primaryWorkArea,
          sourceKind: source.source.kind,
          sourceKey: source.source.key
        });
        if (!prepared.request) {
          return rejected("legacyPreparationFailed", "Legacy migration request could not be prepared.", prepared);
        }
        const migrated = await this.invoke(POSTER_STATE_MIGRATE_COMMAND, {
          request: prepared.request
        });
        if (migrated?.status === "migrated" && this.acceptSnapshot(migrated.snapshot)) {
          return this.ready("legacyMigration");
        }
        if (migrated?.status === "skippedExistingState") {
          return this.loadLatest("legacyMigrationConflictWinner");
        }
        return rejected(
          migrated?.code ?? "legacyMigrationRejected",
          migrated?.message ?? "Legacy migration was rejected.",
          migrated
        );
      }
      if (source.status === "loaded" && source.kind === "formalV1") {
        const validation = validatePosterSnapshot(source.snapshot);
        if (!validation.ok) {
          return rejected("formalBridgeSnapshotRejected", "Formal bridge snapshot is invalid.", validation.errors);
        }
        return this.createEmptyState(validation.value, "formalBridgeImport");
      }
      if (source.status !== "noData") {
        return rejected("unsupportedLegacySource", "Legacy inspection returned an unsupported source.", source);
      }

      const snapshot = createDefaultPosterSnapshot({
        title: this.title,
        subtitle: this.subtitle,
        primaryWorkArea: this.primaryWorkArea
      });
      return this.createEmptyState(snapshot, "formalDefault");
    } catch (error) {
      return rejected("posterStateBootstrapFailed", String(error), error);
    }
  }

  saveRuntimeState(runtime) {
    const frozenRuntime = cloneJson(runtime);
    if (!this.snapshot) {
      return Promise.resolve(rejected("posterStateNotReady", "Poster state has not loaded."));
    }
    const prepared = prepareRuntimeMutation(this.snapshot, frozenRuntime, {
      primaryWorkArea: this.primaryWorkArea
    });
    if (!prepared.ok) {
      return Promise.resolve(rejected(
        "runtimeProjectionRejected",
        "Runtime state cannot be persisted.",
        prepared.errors
      ));
    }
    const mutation = {
      sequence: this.mutationSequence += 1,
      overlay: prepared.overlay,
      empty: prepared.empty
    };
    this.pendingMutations.set(mutation.sequence, mutation);
    const operation = async () => {
      let result;
      try {
        result = await this.saveRuntimeStateNow(mutation);
      } catch (error) {
        result = rejected(
          error?.code ?? "replaceCommandError",
          error?.message ?? String(error),
          error
        );
      }
      this.pendingMutations.delete(mutation.sequence);
      const accepted = ["stored", "noChange", "ephemeral"].includes(result?.status);
      if (accepted) {
        if (this.retainedMutation?.sequence <= mutation.sequence) this.retainedMutation = null;
      } else if (!mutation.empty
          && (!this.retainedMutation || mutation.sequence >= this.retainedMutation.sequence)) {
        this.retainedMutation = mutation;
      }
      return result;
    };
    this.mutationQueue = this.mutationQueue.then(operation, operation);
    return this.mutationQueue;
  }

  async saveRuntimeStateNow(mutation) {
    if (!this.snapshot) return rejected("posterStateNotReady", "Poster state has not loaded.");
    const attempt = async () => {
      const applied = applyRuntimeMutation(this.snapshot, mutation);
      if (!applied.ok) {
        return rejected("runtimeProjectionRejected", "Runtime state cannot be persisted.", applied.errors);
      }
      if (sameJsonValue(applied.snapshot, this.snapshot)) {
        return {
          status: "noChange",
          revision: this.snapshot.revision,
          snapshot: cloneJson(this.snapshot)
        };
      }
      if (typeof this.invoke !== "function") {
        applied.snapshot.revision = this.snapshot.revision + 1;
        if (!this.acceptSnapshot(applied.snapshot)) {
          return rejected("ephemeralSnapshotRejected", "Ephemeral poster state was rejected.");
        }
        return {
          status: "ephemeral",
          revision: this.snapshot.revision,
          snapshot: cloneJson(this.snapshot)
        };
      }
      return this.invoke(POSTER_STATE_REPLACE_COMMAND, {
        expectedRevision: this.snapshot.revision,
        snapshot: applied.snapshot
      });
    };

    let result = await attempt();
    if (result?.status === "conflict") {
      const latest = await this.loadLatest("saveConflictReload");
      if (latest.status !== "ready") return latest;
      result = await attempt();
    }
    if (result?.status === "stored" && this.acceptSnapshot(result.snapshot)) {
      return { ...result, snapshot: cloneJson(this.snapshot) };
    }
    return result?.status ? result : rejected("replaceCommandFailed", "Poster state replace failed.", result);
  }

  resetDefault() {
    const operation = () => this.resetDefaultNow();
    this.mutationQueue = this.mutationQueue.then(operation, operation);
    return this.mutationQueue;
  }

  async resetDefaultNow() {
    try {
      await this.ensureContext();
      const next = createDefaultPosterSnapshot({
        title: this.title,
        subtitle: this.subtitle,
        primaryWorkArea: this.primaryWorkArea
      });
      if (typeof this.invoke !== "function") {
        this.acceptSnapshot(next);
        this.retainedMutation = null;
        return this.ready("browserPreviewReset");
      }
      const replace = async () => this.invoke(POSTER_STATE_REPLACE_COMMAND, {
        expectedRevision: this.snapshot?.revision ?? null,
        snapshot: next
      });
      let result = await replace();
      if (result?.status === "conflict") {
        const latest = await this.loadLatest("resetConflictReload");
        if (latest.status !== "ready") return latest;
        result = await replace();
      }
      if (result?.status === "stored" && this.acceptSnapshot(result.snapshot)) {
        this.retainedMutation = null;
        return this.ready("formalReset");
      }
      return rejected(result?.code ?? "resetRejected", result?.message ?? "Poster reset was rejected.", result);
    } catch (error) {
      return rejected("resetFailed", String(error), error);
    }
  }
}

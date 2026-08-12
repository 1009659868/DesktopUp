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

const DEFAULT_POSTER_TITLE = "DESKTOP COLLECTION";

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
  constructor({ invoke, storage, primaryWorkArea, title = DEFAULT_POSTER_TITLE } = {}) {
    this.invoke = invoke;
    this.storage = storage;
    this.primaryWorkArea = primaryWorkArea ?? null;
    this.title = title;
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
    this.snapshot = validation.value;
    return true;
  }

  runtimeSnapshot() {
    return this.snapshot
      ? projectPosterStateToRuntime(this.snapshot, { primaryWorkArea: this.primaryWorkArea })
      : createEphemeralPosterRuntimeSnapshot();
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

  async bootstrap() {
    try {
      await this.ensureContext();
      if (typeof this.invoke !== "function") {
        this.acceptSnapshot(createDefaultPosterSnapshot({
          title: this.title,
          primaryWorkArea: this.primaryWorkArea
        }));
        return this.ready("browserPreviewEphemeral");
      }

      const loaded = await this.loadLatest();
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
        primaryWorkArea: this.primaryWorkArea
      });
      return this.createEmptyState(snapshot, "formalDefault");
    } catch (error) {
      return rejected("posterStateBootstrapFailed", String(error), error);
    }
  }

  saveRuntimeState(runtime) {
    const frozenRuntime = cloneJson(runtime);
    const operation = async () => {
      try {
        return await this.saveRuntimeStateNow(frozenRuntime);
      } catch (error) {
        return rejected(
          error?.code ?? "replaceCommandError",
          error?.message ?? String(error),
          error
        );
      }
    };
    this.mutationQueue = this.mutationQueue.then(operation, operation);
    return this.mutationQueue;
  }

  async saveRuntimeStateNow(runtime) {
    if (!this.snapshot) return rejected("posterStateNotReady", "Poster state has not loaded.");
    if (typeof this.invoke !== "function") return { status: "ephemeral" };
    const attempt = async () => {
      const projection = projectRuntimeToPosterState(this.snapshot, runtime, {
        primaryWorkArea: this.primaryWorkArea
      });
      if (!projection.ok) {
        return rejected("runtimeProjectionRejected", "Runtime state cannot be persisted.", projection.errors);
      }
      return this.invoke(POSTER_STATE_REPLACE_COMMAND, {
        expectedRevision: this.snapshot.revision,
        snapshot: projection.snapshot
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
        primaryWorkArea: this.primaryWorkArea
      });
      if (typeof this.invoke !== "function") {
        this.acceptSnapshot(next);
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
        return this.ready("formalReset");
      }
      return rejected(result?.code ?? "resetRejected", result?.message ?? "Poster reset was rejected.", result);
    } catch (error) {
      return rejected("resetFailed", String(error), error);
    }
  }
}

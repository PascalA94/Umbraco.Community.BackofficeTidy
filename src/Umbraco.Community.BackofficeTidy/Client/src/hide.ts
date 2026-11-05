// Pure logic for the entry point, kept free of backoffice imports so Vitest can run it without the backoffice.

/** The two registry members used, so tests can pass a fake or core's real registry. */
export interface HideRegistry {
  exclude(alias: string): void;
  isRegistered(alias: string): boolean;
  /** The registered manifest, if any. Used to tell backoffice entry points, whose module may still be loading. */
  getByAlias?(alias: string): { type?: string; js?: unknown } | undefined;
}

export interface HiddenRules {
  aliases: string[];
  warnUnknownAliases: boolean;
}

export const FETCH_FAILED_WARNING = 'BackofficeTidy: could not load rules; nothing hidden.';

export const unknownAliasWarning = (alias: string) =>
  `BackofficeTidy: no extension with alias "${alias}" (typo, or the package is not installed).`;

/**
 * Exclusions must not start sooner than this after onInit begins. docs/spikes.md (Task 2): Copilot survives an exclude made
 * within about 30 ms of onInit on both lines (0 ms on both, 10 ms on 17), and a later re-exclude does not remove it.
 * A fast or cached response must not win that race. Cost: a dashboard that is already rendered flashes up to this long.
 */
export const MIN_EXCLUDE_DELAY_MS = 100;

/**
 * Core unloads an entry point only if its module has finished loading when it is excluded. An exclude that lands while
 * the module is still in flight is lost: the module then initialises and stays for good (on Umbraco 18, Copilot over a
 * Slow 4G link). So an entry point is excluded only after its own module has loaded, which resolves the same import core
 * is waiting for, plus a short settle so core's continuation runs first. The timeout keeps a hung module from blocking.
 */
export const ENTRY_POINT_SETTLE_MS = 50;
export const ENTRY_POINT_LOAD_TIMEOUT_MS = 10_000;

const ENTRY_POINT_TYPES = new Set(['backofficeEntryPoint', 'entryPoint']);

export type ImportModule = (js: unknown) => Promise<unknown>;

export const importModule: ImportModule = async (js) =>
  typeof js === 'function' ? (js as () => unknown)() : import(/* @vite-ignore */ String(js));

export type Idle = (callback: () => void) => void;

export const idleCallback: Idle = (callback) =>
  typeof requestIdleCallback === 'function' ? requestIdleCallback(() => callback()) : void setTimeout(callback, 0);

export function applyHiddenAliases(
  registry: HideRegistry,
  aliases: string[],
  warnUnknown: boolean,
  warn: (message: string) => void,
  idle: Idle = idleCallback,
): void {
  if (aliases.length === 0) return;

  // exclude() drops the extension from isRegistered, so what is unknown must be read before excluding. An unknown alias
  // is still excluded: that also blocks a package that registers it later.
  const unknown = warnUnknown ? aliases.filter((alias) => !registry.isRegistered(alias)) : [];
  for (const alias of aliases) registry.exclude(alias);

  if (unknown.length > 0) {
    idle(() => {
      for (const alias of unknown) warn(unknownAliasWarning(alias));
    });
  }
}

function toRules(body: unknown): HiddenRules | undefined {
  if (typeof body !== 'object' || body === null) return undefined;
  const { aliases, warnUnknownAliases } = body as Record<string, unknown>;
  if (!Array.isArray(aliases) || !aliases.every((alias) => typeof alias === 'string')) return undefined;
  return { aliases: aliases as string[], warnUnknownAliases: warnUnknownAliases !== false };
}

export interface RunHideDeps {
  /** Resolves to the response body, or rejects, or resolves to nothing, when the rules could not be loaded. */
  load: () => Promise<unknown>;
  registry: HideRegistry;
  warn: (message: string) => void;
  now?: () => number;
  sleep?: (ms: number) => Promise<void>;
  idle?: Idle;
  importModule?: ImportModule;
}

/** Never throws: on any failure nothing is hidden and one warning is logged (design 1, anti-goal). */
export async function runHide({
  load,
  registry,
  warn,
  now = () => performance.now(),
  sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms)),
  idle = idleCallback,
  importModule: loadModule = importModule,
}: RunHideDeps): Promise<void> {
  const startedAt = now();
  let rules: HiddenRules | undefined;
  try {
    rules = toRules(await load());
  } catch {
    rules = undefined;
  }

  if (!rules) {
    warn(FETCH_FAILED_WARNING);
    return;
  }

  const remaining = MIN_EXCLUDE_DELAY_MS - (now() - startedAt);
  if (remaining > 0) await sleep(remaining);

  const isEntryPointWithModule = (alias: string) => {
    const manifest = registry.getByAlias?.(alias);
    return manifest !== undefined && ENTRY_POINT_TYPES.has(manifest.type ?? '') && manifest.js !== undefined;
  };
  const late = rules.aliases.filter(isEntryPointWithModule);
  applyHiddenAliases(
    registry,
    rules.aliases.filter((alias) => !late.includes(alias)),
    rules.warnUnknownAliases,
    warn,
    idle,
  );

  await Promise.all(
    late.map(async (alias) => {
      const js = registry.getByAlias?.(alias)?.js;
      let timedOut = false;
      await Promise.race([
        Promise.resolve()
          .then(() => loadModule(js))
          .catch(() => undefined),
        sleep(ENTRY_POINT_LOAD_TIMEOUT_MS).then(() => void (timedOut = true)),
      ]);
      if (!timedOut) await sleep(ENTRY_POINT_SETTLE_MS);
      registry.exclude(alias);
    }),
  );
}

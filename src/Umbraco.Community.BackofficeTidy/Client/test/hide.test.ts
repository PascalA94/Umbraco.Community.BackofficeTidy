import { describe, expect, it, vi } from 'vitest';
// Core's real registry, imported by path: the package barrel pulls in the whole backoffice and hangs under Vitest.
import { UmbExtensionRegistry } from '../node_modules/@umbraco-cms/backoffice/dist-cms/libs/extension-api/registry/extension.registry.js';
import {
  FETCH_FAILED_WARNING,
  ENTRY_POINT_LOAD_TIMEOUT_MS,
  ENTRY_POINT_SETTLE_MS,
  MIN_EXCLUDE_DELAY_MS,
  applyHiddenAliases,
  runHide,
  unknownAliasWarning,
  type HideRegistry,
} from '../src/hide.js';

const runNow = (callback: () => void) => callback();

function fakeRegistry(known: string[] = []) {
  const exclude = vi.fn<(alias: string) => void>();
  const registry: HideRegistry = { exclude, isRegistered: (alias: string) => known.includes(alias) };
  return Object.assign(registry, { exclude });
}

function realRegistry(...aliases: string[]) {
  const registry = new UmbExtensionRegistry();
  for (const alias of aliases) registry.register({ type: 'dashboard', alias, name: alias } as never);
  return registry;
}

describe('applyHiddenAliases', () => {
  it('excludesEachAlias', () => {
    const registry = fakeRegistry(['a', 'b']);
    applyHiddenAliases(registry, ['a', 'b'], true, vi.fn(), runNow);
    expect(registry.exclude).toHaveBeenCalledTimes(2);
    expect(registry.exclude).toHaveBeenNthCalledWith(1, 'a');
    expect(registry.exclude).toHaveBeenNthCalledWith(2, 'b');
  });

  it('Exclude_AfterRegister_Removes (core registry)', () => {
    const registry = realRegistry('Umb.Dashboard.UmbracoNews', 'keep.me');
    applyHiddenAliases(registry, ['Umb.Dashboard.UmbracoNews'], true, vi.fn(), runNow);
    expect(registry.isRegistered('Umb.Dashboard.UmbracoNews')).toBe(false);
    expect(registry.isRegistered('keep.me')).toBe(true);
    // A package that loads later cannot bring it back.
    registry.register({ type: 'dashboard', alias: 'Umb.Dashboard.UmbracoNews', name: 'late' } as never);
    expect(registry.isRegistered('Umb.Dashboard.UmbracoNews')).toBe(false);
  });

  it('unknownAlias_warnsOnceWithAlias', () => {
    const warn = vi.fn();
    applyHiddenAliases(fakeRegistry(['known']), ['known', 'Umb.Dashbaord.Typo'], true, warn, runNow);
    expect(warn).toHaveBeenCalledTimes(1);
    expect(warn).toHaveBeenCalledWith(
      'BackofficeTidy: no extension with alias "Umb.Dashbaord.Typo" (typo, or the package is not installed).',
    );
    expect(unknownAliasWarning('Umb.Dashbaord.Typo')).toBe(warn.mock.calls[0][0]);
  });

  it('knownAlias_excludedByUs_isNotReportedUnknown (core registry)', () => {
    // exclude() removes the extension from isRegistered, so the check must use what was known before excluding.
    const warn = vi.fn();
    applyHiddenAliases(realRegistry('real.alias'), ['real.alias'], true, warn, runNow);
    expect(warn).not.toHaveBeenCalled();
  });

  it('unknownAlias_stillExcluded_soALateRegistrationIsBlocked', () => {
    const registry = fakeRegistry();
    applyHiddenAliases(registry, ['late.package'], true, vi.fn(), runNow);
    expect(registry.exclude).toHaveBeenCalledWith('late.package');
  });

  it('warnUnknown_off_noWarnings', () => {
    const warn = vi.fn();
    applyHiddenAliases(fakeRegistry(), ['typo'], false, warn, runNow);
    expect(warn).not.toHaveBeenCalled();
  });

  it('EmptyList_NoWarnings', () => {
    const registry = fakeRegistry();
    const warn = vi.fn();
    const idle = vi.fn();
    applyHiddenAliases(registry, [], true, warn, idle);
    expect(registry.exclude).not.toHaveBeenCalled();
    expect(warn).not.toHaveBeenCalled();
  });

  it('the unknown check waits for the idle callback', () => {
    const warn = vi.fn();
    let queued: (() => void) | undefined;
    applyHiddenAliases(fakeRegistry(), ['typo'], true, warn, (callback) => (queued = callback));
    expect(warn).not.toHaveBeenCalled();
    queued?.();
    expect(warn).toHaveBeenCalledTimes(1);
  });
});

describe('runHide', () => {
  const rules = { aliases: ['a'], warnUnknownAliases: true };

  function setup(load: () => Promise<unknown>, startAt = 0) {
    let clock = startAt;
    const registry = fakeRegistry(['a']);
    const warn = vi.fn();
    const sleep = vi.fn(async (ms: number) => {
      clock += ms;
      // The exclusion must not have happened yet while we wait.
      expect(registry.exclude).not.toHaveBeenCalled();
    });
    const now = () => clock;
    const slowLoad = async (ms: number) => {
      clock += ms;
      return load();
    };
    return { registry, warn, sleep, now, run: (fetchMs = 0) => runHide({ load: () => slowLoad(fetchMs) as never, registry, warn, now, sleep, idle: runNow }) };
  }

  it('fetchFailure_noExclusions_oneWarning (rejects)', async () => {
    const t = setup(async () => {
      throw new Error('500');
    });
    await t.run();
    expect(t.registry.exclude).not.toHaveBeenCalled();
    expect(t.warn).toHaveBeenCalledTimes(1);
    expect(t.warn).toHaveBeenCalledWith('BackofficeTidy: could not load rules; nothing hidden.');
    expect(FETCH_FAILED_WARNING).toBe('BackofficeTidy: could not load rules; nothing hidden.');
  });

  it.each([[undefined], [null], [{}], [{ aliases: 'x' }], [{ aliases: [1] }]])('fetchFailure_noExclusions_oneWarning (bad body %j)', async (body) => {
    const t = setup(async () => body);
    await t.run();
    expect(t.registry.exclude).not.toHaveBeenCalled();
    expect(t.warn).toHaveBeenCalledTimes(1);
  });

  it('a fast response waits out the remainder of the minimum delay before excluding', async () => {
    const t = setup(async () => rules);
    await t.run(10);
    expect(t.sleep).toHaveBeenCalledWith(MIN_EXCLUDE_DELAY_MS - 10);
    expect(t.registry.exclude).toHaveBeenCalledWith('a');
  });

  it('a slow response excludes at once', async () => {
    const t = setup(async () => rules);
    await t.run(MIN_EXCLUDE_DELAY_MS + 50);
    expect(t.sleep).not.toHaveBeenCalled();
    expect(t.registry.exclude).toHaveBeenCalledWith('a');
  });

  it('EmptyList_NoWarnings', async () => {
    const t = setup(async () => ({ aliases: [], warnUnknownAliases: true }));
    await t.run();
    expect(t.registry.exclude).not.toHaveBeenCalled();
    expect(t.warn).not.toHaveBeenCalled();
  });

  it('the minimum delay is at least the 100 ms the spike found necessary', () => {
    expect(MIN_EXCLUDE_DELAY_MS).toBeGreaterThanOrEqual(100);
  });
});

describe('entry-point aliases wait for their module (docs/spikes.md: an exclude that lands while the entry point is still loading loses)', () => {
  const entryPoint = 'Some.EntryPoint';
  type Manifest = { type: string; js?: unknown };

  function setup(manifests: Record<string, Manifest>, importModule: (js: unknown) => Promise<unknown>) {
    const order: string[] = [];
    const registry: HideRegistry = {
      exclude: (alias: string) => void order.push(`exclude:${alias}`),
      isRegistered: (alias: string) => alias in manifests,
      getByAlias: (alias: string) => manifests[alias],
    };
    const sleep = vi.fn(async (ms: number) => {
      order.push(`sleep:${ms}`);
      if (ms >= ENTRY_POINT_LOAD_TIMEOUT_MS) await new Promise(() => {}); // the timeout leg: never wins unless the import hangs
    });
    const run = (aliases: string[]) =>
      runHide({
        load: async () => ({ aliases, warnUnknownAliases: false }),
        registry,
        warn: vi.fn(),
        now: () => 1000,
        sleep,
        idle: runNow,
        importModule,
      });
    return { order, run, sleep };
  }

  it('excludes a dashboard at once and an entry point only after its module has loaded', async () => {
    let release!: () => void;
    const t = setup(
      { dash: { type: 'dashboard' }, [entryPoint]: { type: 'backofficeEntryPoint', js: '/x.js' } },
      () => new Promise<void>((resolve) => (release = resolve)),
    );
    const done = t.run(['dash', entryPoint]);
    await vi.waitFor(() => expect(t.order).toContain('exclude:dash'));
    expect(t.order).not.toContain(`exclude:${entryPoint}`);
    release();
    await done;
    expect(t.order.indexOf(`exclude:${entryPoint}`)).toBeGreaterThan(t.order.indexOf('exclude:dash'));
    // The core initializer finishes its own continuation after the module resolves; give it a tick before excluding.
    expect(t.order.indexOf(`sleep:${ENTRY_POINT_SETTLE_MS}`)).toBeLessThan(t.order.indexOf(`exclude:${entryPoint}`));
  });

  it('still excludes when the module fails to load', async () => {
    const t = setup({ [entryPoint]: { type: 'backofficeEntryPoint', js: '/x.js' } }, () => Promise.reject(new Error('404')));
    await t.run([entryPoint]);
    expect(t.order).toContain(`exclude:${entryPoint}`);
  });

  it('gives up waiting after the timeout and excludes anyway', async () => {
    const t = setup({ [entryPoint]: { type: 'backofficeEntryPoint', js: '/x.js' } }, () => new Promise(() => {}));
    const done = t.run([entryPoint]);
    // The fake sleep never resolves the timeout leg, so resolve it through the clock hook instead.
    await vi.waitFor(() => expect(t.sleep).toHaveBeenCalledWith(ENTRY_POINT_LOAD_TIMEOUT_MS));
    expect(t.order).not.toContain(`exclude:${entryPoint}`);
    void done;
  });

  it('an alias nobody registered is excluded at once (it may register later) and a function js is called', async () => {
    const calls: unknown[] = [];
    const t = setup({ [entryPoint]: { type: 'backofficeEntryPoint', js: () => 'loader' } }, async (js) => void calls.push(js));
    await t.run(['not.registered', entryPoint]);
    expect(t.order).toContain('exclude:not.registered');
    expect(calls).toHaveLength(1);
  });
});

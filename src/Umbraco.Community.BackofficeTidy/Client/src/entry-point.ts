import type { UmbEntryPointOnInit, UmbEntryPointOnUnload } from '@umbraco-cms/backoffice/extension-api';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { getBackofficeTidyHidden } from './api/generated/sdk.gen.js';
import { runHide } from './hide.js';

// The server works out which aliases apply to this user; the client only excludes them (design 4).
// Calls go through core's configured client, so the backoffice's own token handling applies.
export const onInit: UmbEntryPointOnInit = (host, extensionRegistry) =>
  void runHide({
    load: async () => {
      const { data, error } = await tryExecute(host, getBackofficeTidyHidden({ client: umbHttpClient }), {
        disableNotifications: true,
      });
      if (error) throw error;
      return data;
    },
    registry: extensionRegistry,
    warn: (message) => console.warn(message),
  });

// UmbEntryPointModule types onUnload as required on 17.8; there is nothing to undo.
export const onUnload: UmbEntryPointOnUnload = () => {};

import type { ManifestBackofficeEntryPoint } from '@umbraco-cms/backoffice/extension-registry';

// Aliases are permanent once released. One bundle serves both Umbraco lines (design 2).
const entryPoint: ManifestBackofficeEntryPoint = {
  type: 'backofficeEntryPoint',
  alias: 'BackofficeTidy.EntryPoint',
  name: 'Backoffice Tidy Entry Point',
  js: () => import('./entry-point.js'),
};

export const manifests = [entryPoint];

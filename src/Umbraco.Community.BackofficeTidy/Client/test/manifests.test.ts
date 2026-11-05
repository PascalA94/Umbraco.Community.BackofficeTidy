import { describe, expect, it } from 'vitest';
import { manifests } from '../src/manifests.js';

describe('manifests', () => {
  it('registers exactly one backofficeEntryPoint with the permanent alias', () => {
    expect(manifests).toHaveLength(1);
    expect(manifests[0].type).toBe('backofficeEntryPoint');
    expect(manifests[0].alias).toBe('BackofficeTidy.EntryPoint');
  });
});

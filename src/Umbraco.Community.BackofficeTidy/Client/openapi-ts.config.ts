import { defineConfig } from '@hey-api/openapi-ts';

// Generates the typed client from the package's own OpenAPI document. Start the demo site first (HTTPS, see CLAUDE.md),
// then `npm run generate:api`. BACKOFFICE_TIDY_OPENAPI overrides the URL; the demo's dev certificate is self-signed.
export default defineConfig({
  input: process.env.BACKOFFICE_TIDY_OPENAPI ?? 'https://localhost:44331/umbraco/openapi/backoffice-tidy.json',
  output: { path: 'src/api/generated', format: false },
  plugins: ['@hey-api/client-fetch', '@hey-api/sdk', '@hey-api/typescript'],
});

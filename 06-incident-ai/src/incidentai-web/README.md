# IncidentAI Web

React + TypeScript incident analysis panel. For the complete stack, configuration, and verification steps, see [the application README](../../README.md).

```bash
npm ci
npm run dev
npm run test
npm run build
npm run lint
```

The Vite dev server proxies `/api` to `http://localhost:5108`, the API's HTTP launch profile. Never place Foundry, PostgreSQL, or S3 credentials in `VITE_*` variables.

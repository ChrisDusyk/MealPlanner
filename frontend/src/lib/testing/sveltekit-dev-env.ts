/**
 * Vitest browser-mode setup: SvelteKit's generated `$app/env/public` module reads
 * `globalThis.__sveltekit_dev.env` at import time, which only exists once the
 * SvelteKit client runtime has booted. Provide an empty env so components that
 * import it can load.
 */
(globalThis as Record<string, unknown>).__sveltekit_dev ??= { env: {} };

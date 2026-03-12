import devtoolsJson from 'vite-plugin-devtools-json';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vitest/config';
import { playwright } from '@vitest/browser-playwright';
import { sveltekit } from '@sveltejs/kit/vite';

const runBrowserTests = process.env.VITEST_BROWSER === '1';

export default defineConfig({
	plugins: [tailwindcss(), sveltekit(), devtoolsJson()],
	server: {
		host: '127.0.0.1',
		proxy: {
			'/api': {
				target: process.env.services__api__https__0 || process.env.services__api__http__0,
				changeOrigin: true
			},
			'/hubs': {
				target: process.env.services__api__https__0 || process.env.services__api__http__0,
				changeOrigin: true,
				ws: true
			}
		}
	},
	preview: {
		host: '127.0.0.1'
	},
	test: {
		api: {
			host: '127.0.0.1',
			port: 13337,
			strictPort: true
		},
		expect: { requireAssertions: true },
		projects: [
			{
				extends: './vite.config.ts',
				test: {
					name: 'client',
					browser: {
						enabled: runBrowserTests,
						provider: playwright(),
						instances: [{ browser: 'chromium', headless: true }]
					},
					include: runBrowserTests ? ['src/**/*.svelte.{test,spec}.{js,ts}'] : [],
					exclude: ['src/lib/server/**']
				}
			},

			{
				extends: './vite.config.ts',
				test: {
					name: 'server',
					environment: 'node',
					include: ['src/**/*.{test,spec}.{js,ts}'],
					exclude: ['src/**/*.svelte.{test,spec}.{js,ts}']
				}
			}
		]
	}
});

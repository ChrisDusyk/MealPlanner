import { page } from 'vitest/browser';
import { describe, expect, it } from 'vitest';
import { render } from 'vitest-browser-svelte';
import Page from './+page.svelte';

describe('/+page.svelte', () => {
	it('renders key public-page structure landmarks', async () => {
		render(Page, {
			data: {
				session: null,
				appUser: null,
				sessionExpired: false,
				flags: { demoBanner: false }
			}
		});

		const heading = page.getByRole('heading', {
			level: 1,
			name: /plan meals\..*shop smarter\..*eat better\./i
		});
		await expect.element(heading).toBeInTheDocument();

		await expect
			.element(page.getByRole('heading', { level: 2, name: /meal planning, simplified/i }))
			.toBeInTheDocument();
		await expect
			.element(page.getByRole('heading', { level: 2, name: /how it works/i }))
			.toBeInTheDocument();
		await expect
			.element(page.getByRole('heading', { level: 2, name: /loved by home cooks/i }))
			.toBeInTheDocument();
		await expect.element(page.getByRole('link', { name: /see how it works/i })).toBeInTheDocument();

		const heroStartFree = page.getByRole('link', { name: /start free/i });
		await expect.element(heroStartFree).toHaveAttribute('href', '/auth/signup');

		const footerStartFree = page.getByRole('link', { name: /start planning/i });
		await expect.element(footerStartFree).toHaveAttribute('href', '/auth/signup');
	});
});

import { page } from 'vitest/browser';
import { describe, expect, it } from 'vitest';
import { render } from 'vitest-browser-svelte';
import Page from './+page.svelte';

describe('/auth/signup/+page.svelte', () => {
	it('renders value messaging for meal planning, grocery generation, and sharing', async () => {
		render(Page);

		await expect
			.element(page.getByRole('heading', { level: 1, name: /plan meals, generate grocery lists/i }))
			.toBeInTheDocument();

		await expect.element(page.getByText(/weekly meal plans/i)).toBeInTheDocument();
		await expect.element(page.getByText(/generated grocery lists/i)).toBeInTheDocument();
		await expect.element(page.getByText(/share with others/i)).toBeInTheDocument();
	});
});

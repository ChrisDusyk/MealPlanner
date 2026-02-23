import { page } from 'vitest/browser';
import { describe, expect, it } from 'vitest';
import { render } from 'vitest-browser-svelte';
import Navbar from './Navbar.svelte';
import AppSidebar from './AppSidebar.svelte';

describe('navigation landmark structure', () => {
	it('renders Navbar with named primary navigation and mobile control wiring', async () => {
		render(Navbar, { session: null });

		await expect.element(page.getByRole('navigation', { name: 'Primary' })).toBeInTheDocument();
		const toggle = page.getByRole('button', { name: 'Toggle menu' });
		await expect.element(toggle).toBeInTheDocument();
		await expect.element(toggle).toHaveAttribute('aria-controls', 'mobile-menu');
		await expect.element(toggle).toHaveAttribute('aria-expanded', 'false');
	});

	it('renders AppSidebar with complementary and application navigation landmarks', async () => {
		render(AppSidebar, { session: null });

		await expect
			.element(page.getByRole('complementary', { name: 'Application sidebar' }))
			.toBeInTheDocument();
		await expect.element(page.getByRole('navigation', { name: 'Application' })).toBeInTheDocument();
	});
});

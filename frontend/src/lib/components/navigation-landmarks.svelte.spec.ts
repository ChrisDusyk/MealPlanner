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

	it('closes Navbar user menu when Escape is pressed', async () => {
		const { container } = render(Navbar, {
			session: {
				user: {
					name: 'Test User',
					email: 'test@example.com'
				},
				expires: new Date(Date.now() + 60_000).toISOString(),
				accessToken: 'test-access-token'
			}
		});

		const trigger = container.querySelector('button[aria-controls="user-menu"]') as HTMLButtonElement;
		expect(trigger).toBeTruthy();

		trigger.click();
		await expect.poll(() => trigger.getAttribute('aria-expanded')).toBe('true');
		expect(document.getElementById('user-menu')).not.toBeNull();

		document.body.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
		await expect.poll(() => trigger.getAttribute('aria-expanded')).toBe('false');
		expect(document.getElementById('user-menu')).toBeNull();
	});
});

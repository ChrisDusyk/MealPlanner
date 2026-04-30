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
					id: 'user-1',
					name: 'Test User',
					email: 'test@example.com',
					role: 'user'
				},
				accessToken: 'test-access-token',
				roles: ['user']
			}
		});

		const trigger = container.querySelector(
			'button[aria-controls="user-menu"]'
		) as HTMLButtonElement;
		expect(trigger).toBeTruthy();

		trigger.click();
		await expect.poll(() => trigger.getAttribute('aria-expanded')).toBe('true');
		expect(document.getElementById('user-menu')).not.toBeNull();

		document.body.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
		await expect.poll(() => trigger.getAttribute('aria-expanded')).toBe('false');
		expect(document.getElementById('user-menu')).toBeNull();
	});

	it('uses pointer cursor classes for logout buttons in navigation components', async () => {
		const { container: navbarContainer } = render(Navbar, {
			session: {
				user: {
					id: 'user-1',
					name: 'Test User',
					email: 'test@example.com',
					role: 'user'
				},
				accessToken: 'test-access-token',
				roles: ['user']
			}
		});

		const userMenuTrigger = navbarContainer.querySelector(
			'button[aria-controls="user-menu"]'
		) as HTMLButtonElement;
		expect(userMenuTrigger).toBeTruthy();
		userMenuTrigger.click();
		await expect.poll(() => userMenuTrigger.getAttribute('aria-expanded')).toBe('true');

		const navbarLogoutButton = navbarContainer.querySelector(
			'[data-testid="navbar-dropdown-logout"]'
		);
		expect(navbarLogoutButton).toBeTruthy();
		expect(navbarLogoutButton?.className).toContain('cursor-pointer');

		const mobileMenuTrigger = navbarContainer.querySelector(
			'button[aria-controls="mobile-menu"]'
		) as HTMLButtonElement;
		expect(mobileMenuTrigger).toBeTruthy();
		mobileMenuTrigger.click();
		await expect.poll(() => mobileMenuTrigger.getAttribute('aria-expanded')).toBe('true');

		const mobileNavbarLogoutButton = navbarContainer.querySelector(
			'[data-testid="navbar-mobile-logout"]'
		);
		expect(mobileNavbarLogoutButton).toBeTruthy();
		expect(mobileNavbarLogoutButton?.className).toContain('cursor-pointer');
		const { container: sidebarContainer } = render(AppSidebar, {
			session: {
				user: {
					id: 'user-1',
					name: 'Test User',
					email: 'test@example.com',
					role: 'user'
				},
				accessToken: 'test-access-token',
				roles: ['user']
			}
		});

		const sidebarLogoutButton = sidebarContainer.querySelector('[data-testid="sidebar-logout"]');
		expect(sidebarLogoutButton).toBeTruthy();
		expect(sidebarLogoutButton?.className).toContain('cursor-pointer');
	});
});

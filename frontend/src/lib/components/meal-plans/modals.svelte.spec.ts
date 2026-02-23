import { page } from 'vitest/browser';
import { describe, expect, it, vi } from 'vitest';
import { render } from 'vitest-browser-svelte';
import AddItemModal from './AddItemModal.svelte';
import CopyModal from './CopyModal.svelte';

describe('meal-plan modals accessibility', () => {
	it('renders AddItemModal with accessible dialog semantics', async () => {
		render(AddItemModal, {
			open: true,
			recipes: [
				{
					id: 'r-1',
					name: 'Chili',
					description: 'Bean chili',
					sourceUrl: null,
					ingredients: [],
					createdAt: new Date().toISOString(),
					updatedAt: new Date().toISOString()
				}
			],
			onSelect: vi.fn(),
			onClose: vi.fn()
		});

		const dialog = page.getByRole('dialog', { name: 'Add Item' });
		await expect.element(dialog).toBeInTheDocument();
		await expect.element(dialog).toHaveAttribute('aria-modal', 'true');
		await expect.element(dialog.getByRole('button', { name: 'Close' })).toBeInTheDocument();
		await expect.element(page.getByRole('button', { name: 'My Recipes' })).toBeInTheDocument();
		await expect.element(page.getByRole('button', { name: 'Quick Entry' })).toBeInTheDocument();
	});

	it('renders CopyModal with label and describedby wiring', async () => {
		render(CopyModal, {
			open: true,
			sourceDay: 'Monday',
			category: 'Dinner',
			onConfirm: vi.fn(),
			onClose: vi.fn()
		});

		const dialog = page.getByRole('dialog', { name: 'Copy Dinner' });
		await expect.element(dialog).toBeInTheDocument();
		await expect.element(dialog).toHaveAttribute('aria-modal', 'true');
		await expect.element(page.getByText('From Monday to other days')).toBeInTheDocument();
		await expect.element(page.getByRole('button', { name: 'Weekdays' })).toBeInTheDocument();
		await expect.element(page.getByRole('button', { name: 'All Days' })).toBeInTheDocument();
	});
});

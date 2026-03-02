import { page } from 'vitest/browser';
import { describe, expect, it, vi } from 'vitest';
import { render } from 'vitest-browser-svelte';
import AddItemModal from './AddItemModal.svelte';
import CopyModal from './CopyModal.svelte';
import ShareModal from './ShareModal.svelte';

describe('meal-plan modals accessibility', () => {
	it('renders AddItemModal with accessible dialog semantics', async () => {
		render(AddItemModal, {
			open: true,
			recipes: [
				{
					id: 'r-1',
					name: 'Chili',
					description: 'Bean chili',
					servings: 1,
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

	it('closes AddItemModal when Escape is pressed', async () => {
		const onClose = vi.fn();

		render(AddItemModal, {
			open: true,
			recipes: [
				{
					id: 'r-1',
					name: 'Chili',
					description: 'Bean chili',
					servings: 1,
					sourceUrl: null,
					ingredients: [],
					createdAt: new Date().toISOString(),
					updatedAt: new Date().toISOString()
				}
			],
			onSelect: vi.fn(),
			onClose
		});

		const dialog = page.getByRole('dialog', { name: 'Add Item' });
		await expect.element(dialog).toBeInTheDocument();

		const dialogEl = document.querySelector('[role="dialog"]');
		dialogEl?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
		expect(onClose).toHaveBeenCalledTimes(1);
	});

	it('keeps keyboard focus trapped inside CopyModal', async () => {
		render(CopyModal, {
			open: true,
			sourceDay: 'Monday',
			category: 'Dinner',
			onConfirm: vi.fn(),
			onClose: vi.fn()
		});

		const dialog = page.getByRole('dialog', { name: 'Copy Dinner' });
		await expect.element(dialog).toBeInTheDocument();

		const closeButton = dialog.getByRole('button', { name: 'Close' });
		const cancelButton = dialog.getByRole('button', { name: 'Cancel' });

		const closeButtonEl = document.querySelector(
			'[role="dialog"] button[aria-label="Close"]'
		) as HTMLElement | null;
		const cancelButtonEl = Array.from(document.querySelectorAll('[role="dialog"] button')).find(
			(button) => button.textContent?.trim() === 'Cancel'
		) as HTMLElement | undefined;

		closeButtonEl?.focus();
		await expect.element(closeButton).toHaveFocus();

		document
			.querySelector('[role="dialog"]')
			?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true, bubbles: true }));
		await expect.element(cancelButton).toHaveFocus();

		cancelButtonEl?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true }));
		await expect.element(closeButton).toHaveFocus();
	});

	it('closes ShareModal when Escape is pressed', async () => {
		const onClose = vi.fn();

		render(ShareModal, {
			open: true,
			weekStart: '2026-02-23',
			shares: [],
			onShare: vi.fn(async () => null),
			onRevoke: vi.fn(async () => undefined),
			onClose
		});

		const dialog = page.getByRole('dialog', { name: 'Share Meal Plan' });
		await expect.element(dialog).toBeInTheDocument();

		const dialogEl = document.querySelector('[role="dialog"]');
		dialogEl?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
		expect(onClose).toHaveBeenCalledTimes(1);
	});
});

import { page } from 'vitest/browser';
import { createRawSnippet } from 'svelte';
import { describe, expect, it, vi } from 'vitest';
import { render } from 'vitest-browser-svelte';
import Modal from './Modal.svelte';

const body = createRawSnippet(() => ({
	render: () =>
		'<div class="px-5 py-4"><button type="button">First</button><button type="button">Last</button></div>'
}));

describe('Modal', () => {
	it('renders accessible dialog semantics from title and subtitle', async () => {
		render(Modal, {
			open: true,
			title: 'Example Modal',
			subtitle: 'A helpful subtitle',
			onClose: vi.fn(),
			children: body
		});

		const dialog = page.getByRole('dialog', { name: 'Example Modal' });
		await expect.element(dialog).toBeInTheDocument();
		await expect.element(dialog).toHaveAttribute('aria-modal', 'true');
		await expect.element(page.getByText('A helpful subtitle')).toBeInTheDocument();

		const dialogEl = document.querySelector('[role="dialog"]') as HTMLElement;
		const describedBy = dialogEl.getAttribute('aria-describedby');
		expect(describedBy).toBeTruthy();
		expect(document.getElementById(describedBy!)?.textContent?.trim()).toBe('A helpful subtitle');
	});

	it('omits aria-describedby when no subtitle is provided', async () => {
		render(Modal, {
			open: true,
			title: 'No Subtitle',
			onClose: vi.fn(),
			children: body
		});

		const dialog = page.getByRole('dialog', { name: 'No Subtitle' });
		await expect.element(dialog).toBeInTheDocument();

		const dialogEl = document.querySelector('[role="dialog"]') as HTMLElement;
		expect(dialogEl.getAttribute('aria-describedby')).toBeNull();
	});

	it('renders nothing when closed', async () => {
		render(Modal, {
			open: false,
			title: 'Hidden',
			onClose: vi.fn(),
			children: body
		});

		expect(document.querySelector('[role="dialog"]')).toBeNull();
	});

	it('calls onClose when Escape is pressed', async () => {
		const onClose = vi.fn();

		render(Modal, {
			open: true,
			title: 'Escapable',
			onClose,
			children: body
		});

		const dialog = page.getByRole('dialog', { name: 'Escapable' });
		await expect.element(dialog).toBeInTheDocument();

		document
			.querySelector('[role="dialog"]')
			?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
		expect(onClose).toHaveBeenCalledTimes(1);
	});

	it('calls onClose when the backdrop is clicked', async () => {
		const onClose = vi.fn();

		render(Modal, {
			open: true,
			title: 'Backdrop',
			onClose,
			children: body
		});

		const dialog = page.getByRole('dialog', { name: 'Backdrop' });
		await expect.element(dialog).toBeInTheDocument();

		const backdrop = document.querySelector(
			'button[aria-label="Close"][tabindex="-1"]'
		) as HTMLElement;
		backdrop.click();
		expect(onClose).toHaveBeenCalledTimes(1);
	});

	it('keeps keyboard focus trapped inside the dialog', async () => {
		render(Modal, {
			open: true,
			title: 'Trapped',
			onClose: vi.fn(),
			children: body
		});

		const dialog = page.getByRole('dialog', { name: 'Trapped' });
		await expect.element(dialog).toBeInTheDocument();

		const closeButton = dialog.getByRole('button', { name: 'Close' });
		const lastButton = dialog.getByRole('button', { name: 'Last' });

		const closeButtonEl = document.querySelector(
			'[role="dialog"] button[aria-label="Close"]'
		) as HTMLElement | null;
		const lastButtonEl = Array.from(document.querySelectorAll('[role="dialog"] button')).find(
			(button) => button.textContent?.trim() === 'Last'
		) as HTMLElement | undefined;

		closeButtonEl?.focus();
		await expect.element(closeButton).toHaveFocus();

		document
			.querySelector('[role="dialog"]')
			?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true, bubbles: true }));
		await expect.element(lastButton).toHaveFocus();

		lastButtonEl?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true }));
		await expect.element(closeButton).toHaveFocus();
	});

	it('invokes the pass-through onkeydown before built-in handling', async () => {
		const onClose = vi.fn();
		const onkeydown = vi.fn();

		render(Modal, {
			open: true,
			title: 'Keyed',
			onClose,
			onkeydown,
			children: body
		});

		const dialog = page.getByRole('dialog', { name: 'Keyed' });
		await expect.element(dialog).toBeInTheDocument();

		document
			.querySelector('[role="dialog"]')
			?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
		expect(onkeydown).toHaveBeenCalledTimes(1);
		expect(onClose).not.toHaveBeenCalled();
	});
});

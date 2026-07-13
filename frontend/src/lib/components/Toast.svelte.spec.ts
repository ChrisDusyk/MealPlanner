import { page } from 'vitest/browser';
import { afterEach, describe, expect, it } from 'vitest';
import { render } from 'vitest-browser-svelte';
import Toast from './Toast.svelte';
import { toast } from '$lib/stores/toast.svelte';

describe('Toast', () => {
	afterEach(() => {
		toast.dismiss();
	});

	it('renders nothing while no toast is active', async () => {
		render(Toast);

		expect(document.querySelector('[role="status"], [role="alert"]')).toBeNull();
	});

	it('renders a polite status for success toasts', async () => {
		render(Toast);

		toast.success('Recipe saved');

		const status = page.getByRole('status');
		await expect.element(status).toBeInTheDocument();
		await expect.element(status).toHaveAttribute('aria-live', 'polite');
		await expect.element(page.getByText('Recipe saved')).toBeInTheDocument();
	});

	it('renders an assertive alert for error toasts', async () => {
		render(Toast);

		toast.error('Something went wrong');

		const alert = page.getByRole('alert');
		await expect.element(alert).toBeInTheDocument();
		await expect.element(alert).toHaveAttribute('aria-live', 'assertive');
		await expect.element(page.getByText('Something went wrong')).toBeInTheDocument();
	});
});

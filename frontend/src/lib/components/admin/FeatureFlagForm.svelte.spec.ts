import { describe, expect, it, vi } from 'vitest';
import { page } from 'vitest/browser';
import { render } from 'vitest-browser-svelte';
import type { CreateFeatureFlagRequest } from '$lib/api/featureFlagsApi';
import FeatureFlagForm from './FeatureFlagForm.svelte';

const BOOLEAN_DEFINITION = '{"variants":{"on":true,"off":false},"defaultVariant":"on"}';

function editData(overrides: Partial<Record<string, unknown>> = {}) {
	return {
		key: 'demo-banner',
		description: 'A demo flag.',
		valueType: 'boolean' as const,
		enabled: false,
		disabledVariant: 'off',
		definitionJson: BOOLEAN_DEFINITION,
		...overrides
	};
}

describe('FeatureFlagForm', () => {
	it('seeds variant rows and defaults from the stored definition', async () => {
		render(FeatureFlagForm, { mode: 'edit', initialData: editData(), onsubmit: vi.fn() });

		await expect.element(page.getByLabelText('Variant 1 name')).toHaveValue('on');
		await expect.element(page.getByLabelText('Variant 1 value')).toHaveValue('true');
		await expect.element(page.getByLabelText('Variant 2 name')).toHaveValue('off');
		await expect.element(page.getByLabelText('Serve while on')).toHaveValue('on');
		await expect.element(page.getByLabelText('Serve while off')).toHaveValue('off');
	});

	it('renders the key as read-only when editing', async () => {
		render(FeatureFlagForm, { mode: 'edit', initialData: editData(), onsubmit: vi.fn() });

		await expect.element(page.getByRole('textbox', { name: 'Key' })).not.toBeInTheDocument();
	});

	it('offers a key field when creating', async () => {
		render(FeatureFlagForm, { mode: 'create', onsubmit: vi.fn() });

		await expect.element(page.getByLabelText('Key')).toBeInTheDocument();
	});

	it('adds a variant row and offers it as a default', async () => {
		render(FeatureFlagForm, { mode: 'edit', initialData: editData(), onsubmit: vi.fn() });

		await page.getByRole('button', { name: 'Add variant' }).click();
		await page.getByLabelText('Variant 3 name').fill('beta');

		const defaultSelect = document.querySelector<HTMLSelectElement>('#flag-default-variant');
		expect(Array.from(defaultSelect!.options).map((option) => option.value)).toContain('beta');
	});

	it('submits the assembled definition', async () => {
		const onsubmit = vi.fn();
		render(FeatureFlagForm, { mode: 'edit', initialData: editData(), onsubmit });

		await page.getByRole('button', { name: 'Save changes' }).click();

		expect(onsubmit).toHaveBeenCalledTimes(1);
		const payload = onsubmit.mock.calls[0][0] as CreateFeatureFlagRequest;
		expect(payload.key).toBe('demo-banner');
		expect(payload.disabledVariant).toBe('off');
		expect(JSON.parse(payload.definitionJson)).toEqual({
			variants: { on: true, off: false },
			defaultVariant: 'on'
		});
	});

	it('blocks submission and explains when a variant does not match the value type', async () => {
		const onsubmit = vi.fn();
		render(FeatureFlagForm, { mode: 'edit', initialData: editData(), onsubmit });

		await page.getByLabelText('Variant 1 value').fill('"yes"');
		await page.getByRole('button', { name: 'Save changes' }).click();

		expect(onsubmit).not.toHaveBeenCalled();
		await expect
			.element(page.getByTestId('feature-flag-form-error'))
			.toHaveTextContent('Expected true or false.');
	});

	it('starts in raw mode when targeting cannot be represented by the builder', async () => {
		render(FeatureFlagForm, {
			mode: 'edit',
			initialData: editData({
				definitionJson:
					'{"variants":{"on":true,"off":false},"defaultVariant":"off","targeting":{"and":[true,false]}}'
			}),
			onsubmit: vi.fn()
		});

		await expect.element(page.getByLabelText('Targeting rules as JSON')).toBeInTheDocument();
		await expect
			.element(page.getByRole('button', { name: 'Back to rule builder' }))
			.toBeInTheDocument();
	});

	it('builds a targeting rule and includes it in the submitted definition', async () => {
		const onsubmit = vi.fn();
		render(FeatureFlagForm, { mode: 'edit', initialData: editData(), onsubmit });

		await page.getByRole('button', { name: 'Add rule' }).click();
		await page.getByLabelText('Rule 1 value').fill('admin');
		await page.getByLabelText('Rule 1 variant').selectOptions('on');
		await page.getByRole('button', { name: 'Save changes' }).click();

		const payload = onsubmit.mock.calls[0][0] as CreateFeatureFlagRequest;
		expect(JSON.parse(payload.definitionJson).targeting).toEqual({
			if: [{ '==': [{ var: 'role' }, 'admin'] }, 'on']
		});
	});
});

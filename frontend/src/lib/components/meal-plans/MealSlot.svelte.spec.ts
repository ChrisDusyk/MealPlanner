import { page } from 'vitest/browser';
import { describe, expect, it, vi } from 'vitest';
import { render } from 'vitest-browser-svelte';
import MealSlot from './MealSlot.svelte';

describe('MealSlot remove action', () => {
	it('invokes onRemove with day, category, and index', async () => {
		const onRemove = vi.fn();

		render(MealSlot, {
			day: 'Monday',
			category: 'Breakfast',
			dateLabel: 'Mon',
			items: [{ recipeId: 'r1', name: 'Pasta', servings: 2 }],
			onAdd: vi.fn(),
			onRemove,
			onCopy: vi.fn(),
			onUpdateServings: vi.fn()
		});

		const removeButton = page.getByRole('button', { name: 'Remove Pasta' });
		await removeButton.click();

		expect(onRemove).toHaveBeenCalledTimes(1);
		expect(onRemove).toHaveBeenCalledWith('Monday', 'Breakfast', 0);
	});
});

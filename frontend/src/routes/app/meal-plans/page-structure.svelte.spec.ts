import { page } from 'vitest/browser';
import { describe, expect, it } from 'vitest';
import { render } from 'vitest-browser-svelte';
import MealPlansPage from './+page.svelte';
import { WEEK_DAYS } from '$lib/api/mealPlanApi';

function buildData() {
	return {
		session: {
			user: {
				id: 'user-1',
				name: 'Test User',
				email: 'test@example.com',
				role: 'user'
			},
			accessToken: 'test-access-token',
			roles: ['user']
		},
		appUser: null,
		mealPlan: {
			id: 'plan-1',
			weekStart: '2026-02-23',
			days: WEEK_DAYS.map((day) => ({ day, slots: {} })),
			createdAt: new Date().toISOString(),
			updatedAt: new Date().toISOString()
		},
		recipes: [],
		sharedWithMe: [],
		myShares: []
	};
}

describe('meal plans route structure', () => {
	it('renders core meal-plans page heading and primary action', async () => {
		render(MealPlansPage, {
			data: buildData()
		});

		await expect
			.element(page.getByRole('heading', { level: 1, name: 'Meal Plans' }))
			.toBeInTheDocument();
		await expect
			.element(page.getByRole('button', { name: 'Generate Grocery List' }))
			.toBeInTheDocument();
		await expect.element(page.getByRole('button', { name: 'Share' })).toBeInTheDocument();
		await expect.element(page.getByRole('button', { name: 'Previous week' })).toBeInTheDocument();
		await expect.element(page.getByRole('button', { name: 'Next week' })).toBeInTheDocument();
	});
});

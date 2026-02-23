import { json, error } from '@sveltejs/kit';
import { updateDaySlot, copyCategory, removeSlotItem } from '$lib/api/mealPlanApi';
import type { RequestHandler } from './$types';

export const PUT: RequestHandler = async ({ request, locals, fetch, url }) => {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	const weekStart = url.searchParams.get('weekStart') ?? '';
	const day = url.searchParams.get('day') ?? '';
	const category = url.searchParams.get('category') ?? '';
	const onBehalfOf = url.searchParams.get('onBehalfOf') ?? undefined;

	try {
		const body = await request.json();
		const result = await updateDaySlot(
			session.accessToken,
			weekStart,
			day,
			category,
			body.items,
			fetch,
			onBehalfOf
		);
		return json(result);
	} catch (err) {
		console.error('Failed to update day slot:', err);
		const message = err instanceof Error ? err.message : 'Failed to update meal slot.';
		return json({ error: message }, { status: 500 });
	}
};

export const POST: RequestHandler = async ({ request, locals, fetch, url }) => {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	const weekStart = url.searchParams.get('weekStart') ?? '';
	const onBehalfOf = url.searchParams.get('onBehalfOf') ?? undefined;

	try {
		const body = await request.json();
		const result = await copyCategory(
			session.accessToken,
			weekStart,
			body.sourceDay,
			body.category,
			body.targetDays,
			fetch,
			onBehalfOf
		);
		return json(result);
	} catch (err) {
		console.error('Failed to copy category:', err);
		const message = err instanceof Error ? err.message : 'Failed to copy meal category.';
		return json({ error: message }, { status: 500 });
	}
};

export const DELETE: RequestHandler = async ({ locals, fetch, url }) => {
	const session = await locals.auth();
	if (!session?.accessToken) {
		error(401, 'Unauthorized');
	}

	const weekStart = url.searchParams.get('weekStart') ?? '';
	const day = url.searchParams.get('day') ?? '';
	const category = url.searchParams.get('category') ?? '';
	const itemIndex = parseInt(url.searchParams.get('itemIndex') ?? '0', 10);
	const onBehalfOf = url.searchParams.get('onBehalfOf') ?? undefined;

	try {
		const result = await removeSlotItem(
			session.accessToken,
			weekStart,
			day,
			category,
			itemIndex,
			fetch,
			onBehalfOf
		);
		return json(result);
	} catch (err) {
		console.error('Failed to remove slot item:', err);
		const message = err instanceof Error ? err.message : 'Failed to remove item.';
		return json({ error: message }, { status: 500 });
	}
};

<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { onMount } from 'svelte';
	import { SvelteURLSearchParams } from 'svelte/reactivity';
	import type { MealSlotItem, MealPlanResponse } from '$lib/api/mealPlanApi';
	import type { Recipe } from '$lib/api/recipeApi';
	import {
		MealPlanRealtimeClient,
		type MealPlanUpdatedEvent
	} from '$lib/realtime/mealPlanRealtime';
	import WeekNavigator from '$lib/components/meal-plans/WeekNavigator.svelte';
	import MealPlanGrid from '$lib/components/meal-plans/MealPlanGrid.svelte';
	import AddItemModal from '$lib/components/meal-plans/AddItemModal.svelte';
	import CopyModal from '$lib/components/meal-plans/CopyModal.svelte';
	import { toast } from '$lib/stores/toast.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	// Track server data version so we can re-seed local state on navigation
	let serverVersion = $derived(data.mealPlan.weekStart + data.mealPlan.id);

	// Mutable local copies for optimistic updates, seeded from server data.
	// The $state() captures data for SSR; the $effect below re-seeds on navigation.
	// svelte-ignore state_referenced_locally
	let mealPlan: MealPlanResponse = $state(data.mealPlan);
	// svelte-ignore state_referenced_locally
	let recipes: Recipe[] = $state(data.recipes);

	// Re-sync when server data changes (e.g. week navigation)
	$effect(() => {
		// Subscribe to the derived value so this re-runs on navigation
		void serverVersion;
		mealPlan = data.mealPlan;
		recipes = data.recipes;
		currentUserId = data.appUser?.authUserId ?? null;
	});

	// Modal state
	let addModalOpen = $state(false);
	let addModalDay = $state('');
	let addModalCategory = $state('');

	let copyModalOpen = $state(false);
	let copyModalDay = $state('');
	let copyModalCategory = $state('');

	// Promise chain for slot mutations (prevents out-of-order response overwrites)
	let pendingSlotUpdate: Promise<void> = Promise.resolve();

	let generateGroceryListLoading = $state(false);
	const realtimeClient = new MealPlanRealtimeClient();
	// svelte-ignore state_referenced_locally
	let currentUserId: string | null = $state(data.appUser?.authUserId ?? null);

	function applyRealtimeUpdate(event: MealPlanUpdatedEvent) {
		if (event.weekStart !== mealPlan.weekStart) {
			return;
		}

		// Skip our own edits — optimistic updates already applied them.
		if (currentUserId && event.changedByUserId === currentUserId) {
			return;
		}

		mealPlan = event.mealPlan;
	}

	onMount(() => {
		let disposed = false;

		void realtimeClient
			.start((event) => {
				if (disposed) return;
				applyRealtimeUpdate(event);
			})
			.catch((err) => {
				console.error('Failed to start meal plan realtime connection', err);
			});

		return () => {
			disposed = true;
			void realtimeClient.stop();
		};
	});

	// ── Navigation ──

	function handleNavigate(weekStart: string) {
		goto(resolve(`/app/meal-plans?weekStart=${weekStart}`));
	}

	async function handleGenerateGroceryList() {
		if (generateGroceryListLoading) return;
		generateGroceryListLoading = true;

		try {
			await pendingSlotUpdate;
			const params = new SvelteURLSearchParams({ weekStart: mealPlan.weekStart });
			const res = await fetch(`/app/meal-plans/grocery-list?${params}`, {
				method: 'POST'
			});

			if (!res.ok) {
				const body = await res.json().catch(() => ({}));
				throw new Error(body.error ?? 'Failed to generate grocery list.');
			}

			const body = await res.json();
			const weekStart =
				typeof body?.groceryList?.weekStart === 'string'
					? body.groceryList.weekStart
					: mealPlan.weekStart;
			const redirectParams = new SvelteURLSearchParams({ weekStart });
			await goto(resolve(`/app/grocery-lists?${redirectParams}`));
		} catch (err) {
			toast.show(err instanceof Error ? err.message : 'Failed to generate grocery list.', 'error');
		} finally {
			generateGroceryListLoading = false;
		}
	}

	// ── Add Item ──

	function handleOpenAdd(day: string, category: string) {
		addModalDay = day;
		addModalCategory = category;
		addModalOpen = true;
	}

	function queueSlotMutation(mutation: () => Promise<void>): Promise<void> {
		const next = pendingSlotUpdate.then(mutation, mutation);
		pendingSlotUpdate = next.catch(() => {
			// Keep queue alive after a failure (errors handled in each mutation)
		});
		return next;
	}

	async function handleAddItem(item: MealSlotItem) {
		const dayPlan = mealPlan.days.find((d) => d.day === addModalDay);
		if (!dayPlan) return;

		// Optimistic update
		const currentItems = dayPlan.slots[addModalCategory] ?? [];
		const newItems = [...currentItems, item];
		dayPlan.slots[addModalCategory] = newItems;

		await queueSlotMutation(async () => {
			try {
				const params = new SvelteURLSearchParams({
					weekStart: mealPlan.weekStart,
					day: addModalDay,
					category: addModalCategory
				});
				const res = await fetch(`/app/meal-plans?${params}`, {
					method: 'PUT',
					headers: { 'Content-Type': 'application/json' },
					body: JSON.stringify({ items: newItems })
				});

				if (!res.ok) throw new Error('Failed to save');
				mealPlan = await res.json();
			} catch {
				// Revert
				dayPlan.slots[addModalCategory] = currentItems;
				toast.show('Failed to add item. Please try again.', 'error');
			}
		});
	}

	// ── Remove Item ──

	async function handleRemoveItem(day: string, category: string, index: number) {
		const dayPlan = mealPlan.days.find((d) => d.day === day);
		if (!dayPlan) return;

		const currentItems = [...(dayPlan.slots[category] ?? [])];
		const newItems = currentItems.filter((_, i) => i !== index);

		// Optimistic update
		dayPlan.slots[category] = newItems;

		try {
			const params = new SvelteURLSearchParams({
				weekStart: mealPlan.weekStart,
				day,
				category,
				itemIndex: index.toString()
			});
			const res = await fetch(`/app/meal-plans?${params}`, {
				method: 'DELETE'
			});

			if (!res.ok) throw new Error('Failed to remove');
			mealPlan = await res.json();
		} catch {
			// Revert
			dayPlan.slots[category] = currentItems;
			toast.show('Failed to remove item. Please try again.', 'error');
		}
	}

	// ── Update Servings ──

	async function handleUpdateServings(
		day: string,
		category: string,
		index: number,
		servings: number
	) {
		const dayPlan = mealPlan.days.find((d) => d.day === day);
		if (!dayPlan) return;

		const currentItems = [...(dayPlan.slots[category] ?? [])];
		if (index < 0 || index >= currentItems.length) return;

		// Skip no-op updates (also prevents double-fires from onchange + onblur)
		if (currentItems[index].servings === servings) return;

		const newItems = currentItems.map((item, i) => (i === index ? { ...item, servings } : item));

		// Optimistic update
		dayPlan.slots[category] = newItems;

		await queueSlotMutation(async () => {
			try {
				const params = new SvelteURLSearchParams({
					weekStart: mealPlan.weekStart,
					day,
					category
				});
				const res = await fetch(`/app/meal-plans?${params}`, {
					method: 'PUT',
					headers: { 'Content-Type': 'application/json' },
					body: JSON.stringify({ items: newItems })
				});

				if (!res.ok) throw new Error('Failed to update servings');
				mealPlan = await res.json();
			} catch {
				// Revert
				dayPlan.slots[category] = currentItems;
				toast.show('Failed to update servings. Please try again.', 'error');
			}
		});
	}

	// ── Copy Category ──

	function handleOpenCopy(day: string, category: string) {
		copyModalDay = day;
		copyModalCategory = category;
		copyModalOpen = true;
	}

	async function handleCopyConfirm(targetDays: string[]) {
		// Wait for queued slot updates (e.g. servings change) to persist
		await pendingSlotUpdate;

		// Optimistic update: copy source items to targets
		const sourceDayPlan = mealPlan.days.find((d) => d.day === copyModalDay);
		if (!sourceDayPlan) return;

		const sourceItems = sourceDayPlan.slots[copyModalCategory] ?? [];
		const backups: Record<string, MealSlotItem[]> = {};

		for (const td of targetDays) {
			const targetDayPlan = mealPlan.days.find((d) => d.day === td);
			if (targetDayPlan) {
				backups[td] = [...(targetDayPlan.slots[copyModalCategory] ?? [])];
				targetDayPlan.slots[copyModalCategory] = [...sourceItems];
			}
		}

		try {
			const params = new SvelteURLSearchParams({ weekStart: mealPlan.weekStart });
			const res = await fetch(`/app/meal-plans?${params}`, {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify({
					sourceDay: copyModalDay,
					category: copyModalCategory,
					targetDays
				})
			});

			if (!res.ok) throw new Error('Failed to copy');
			mealPlan = await res.json();
			toast.show(
				`Copied ${copyModalCategory} from ${copyModalDay} to ${targetDays.length} ${targetDays.length === 1 ? 'day' : 'days'}`
			);
		} catch {
			// Revert
			for (const td of targetDays) {
				const targetDayPlan = mealPlan.days.find((d) => d.day === td);
				if (targetDayPlan && backups[td]) {
					targetDayPlan.slots[copyModalCategory] = backups[td];
				}
			}
			toast.show('Failed to copy. Please try again.', 'error');
		}
	}
</script>

<svelte:head>
	<title>Meal Plans — Simple Meal Planner</title>
</svelte:head>

<div class="mx-auto max-w-7xl">
	<!-- Page header -->
	<div class="mb-6 flex items-start justify-between">
		<div>
			<h1 class="font-display text-2xl font-bold text-charcoal sm:text-3xl">Meal Plans</h1>
			<p class="mt-1 text-charcoal/80">
				Plan your family's weekly meals — everyone in your family sees and edits the same plan.
			</p>
		</div>
		<div class="flex flex-wrap items-center gap-2">
			<button
				type="button"
				onclick={handleGenerateGroceryList}
				disabled={generateGroceryListLoading}
				class="flex min-h-10 items-center gap-1.5 rounded-lg bg-green-600 px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-60"
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					class="h-4 w-4"
					fill="none"
					viewBox="0 0 24 24"
					stroke="currentColor"
					stroke-width="2"
				>
					<path
						stroke-linecap="round"
						stroke-linejoin="round"
						d="M2.25 3h1.386c.51 0 .955.343 1.087.835l.383 1.437M7.5 14.25a3 3 0 0 0-3 3h15.75m-12.75-3h11.218c1.121-2.3 2.1-4.684 2.924-7.138a60.114 60.114 0 0 0-16.536-1.84M7.5 14.25 5.106 5.272M6 20.25a.75.75 0 1 1-1.5 0 .75.75 0 0 1 1.5 0Zm12.75 0a.75.75 0 1 1-1.5 0 .75.75 0 0 1 1.5 0Z"
					/>
				</svg>
				{generateGroceryListLoading ? 'Generating…' : 'Generate Grocery List'}
			</button>
		</div>
	</div>

	<!-- Week navigator -->
	<div class="mb-6">
		<WeekNavigator weekStart={mealPlan.weekStart} onNavigate={handleNavigate} />
	</div>

	<!-- Grid -->
	<MealPlanGrid
		days={mealPlan.days}
		weekStart={mealPlan.weekStart}
		onAdd={handleOpenAdd}
		onRemove={handleRemoveItem}
		onCopy={handleOpenCopy}
		onUpdateServings={handleUpdateServings}
	/>

	<!-- Add Item Modal -->
	<AddItemModal
		open={addModalOpen}
		{recipes}
		onSelect={handleAddItem}
		onClose={() => (addModalOpen = false)}
	/>

	<!-- Copy Modal -->
	<CopyModal
		open={copyModalOpen}
		sourceDay={copyModalDay}
		category={copyModalCategory}
		onConfirm={handleCopyConfirm}
		onClose={() => (copyModalOpen = false)}
	/>
</div>

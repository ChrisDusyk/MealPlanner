<script lang="ts">
	import { goto } from '$app/navigation';
	import type { MealSlotItem, MealPlanResponse } from '$lib/api/mealPlanApi';
	import type { Recipe } from '$lib/api/recipeApi';
	import WeekNavigator from '$lib/components/meal-plans/WeekNavigator.svelte';
	import MealPlanGrid from '$lib/components/meal-plans/MealPlanGrid.svelte';
	import AddItemModal from '$lib/components/meal-plans/AddItemModal.svelte';
	import CopyModal from '$lib/components/meal-plans/CopyModal.svelte';
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
	});

	// Modal state
	let addModalOpen = $state(false);
	let addModalDay = $state('');
	let addModalCategory = $state('');

	let copyModalOpen = $state(false);
	let copyModalDay = $state('');
	let copyModalCategory = $state('');

	// Toast state
	let toastMessage = $state('');
	let toastType: 'success' | 'error' = $state('success');
	let toastVisible = $state(false);

	function showToast(message: string, type: 'success' | 'error' = 'success') {
		toastMessage = message;
		toastType = type;
		toastVisible = true;
		setTimeout(() => {
			toastVisible = false;
		}, 3000);
	}

	// ── Navigation ──

	function handleNavigate(weekStart: string) {
		goto(`/app/meal-plans?weekStart=${weekStart}`);
	}

	// ── Add Item ──

	function handleOpenAdd(day: string, category: string) {
		addModalDay = day;
		addModalCategory = category;
		addModalOpen = true;
	}

	async function handleAddItem(item: MealSlotItem) {
		const dayPlan = mealPlan.days.find((d) => d.day === addModalDay);
		if (!dayPlan) return;

		// Optimistic update
		const currentItems = dayPlan.slots[addModalCategory] ?? [];
		const newItems = [...currentItems, item];
		dayPlan.slots[addModalCategory] = newItems;

		try {
			const params = new URLSearchParams({
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
			const updated: MealPlanResponse = await res.json();
			mealPlan = updated;
		} catch {
			// Revert
			dayPlan.slots[addModalCategory] = currentItems;
			showToast('Failed to add item. Please try again.', 'error');
		}
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
			const params = new URLSearchParams({
				weekStart: mealPlan.weekStart,
				day,
				category,
				itemIndex: index.toString()
			});
			const res = await fetch(`/app/meal-plans?${params}`, {
				method: 'DELETE'
			});

			if (!res.ok) throw new Error('Failed to remove');
			const updated: MealPlanResponse = await res.json();
			mealPlan = updated;
		} catch {
			// Revert
			dayPlan.slots[category] = currentItems;
			showToast('Failed to remove item. Please try again.', 'error');
		}
	}

	// ── Copy Category ──

	function handleOpenCopy(day: string, category: string) {
		copyModalDay = day;
		copyModalCategory = category;
		copyModalOpen = true;
	}

	async function handleCopyConfirm(targetDays: string[]) {
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
			const params = new URLSearchParams({ weekStart: mealPlan.weekStart });
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
			const updated: MealPlanResponse = await res.json();
			mealPlan = updated;
			showToast(
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
			showToast('Failed to copy. Please try again.', 'error');
		}
	}
</script>

<svelte:head>
	<title>Meal Plans — MealPlanner</title>
</svelte:head>

<div class="mx-auto max-w-7xl">
	<!-- Page header -->
	<div class="mb-6">
		<h1 class="font-display text-3xl font-bold text-charcoal">Meal Plans</h1>
		<p class="mt-1 text-charcoal/60">
			Plan your weekly meals — drag recipes into slots and copy across days.
		</p>
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

	<!-- Toast notification -->
	{#if toastVisible}
		<div
			class="fixed bottom-6 right-6 z-50 flex items-center gap-2 rounded-lg px-4 py-3 shadow-lg transition-all {toastType ===
			'error'
				? 'bg-red-600 text-white'
				: 'bg-green-700 text-white'}"
		>
			{#if toastType === 'success'}
				<svg
					xmlns="http://www.w3.org/2000/svg"
					class="h-5 w-5"
					fill="none"
					viewBox="0 0 24 24"
					stroke="currentColor"
					stroke-width="2"
				>
					<path
						stroke-linecap="round"
						stroke-linejoin="round"
						d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
					/>
				</svg>
			{:else}
				<svg
					xmlns="http://www.w3.org/2000/svg"
					class="h-5 w-5"
					fill="none"
					viewBox="0 0 24 24"
					stroke="currentColor"
					stroke-width="2"
				>
					<path
						stroke-linecap="round"
						stroke-linejoin="round"
						d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z"
					/>
				</svg>
			{/if}
			<span class="font-display text-sm font-medium">{toastMessage}</span>
		</div>
	{/if}
</div>

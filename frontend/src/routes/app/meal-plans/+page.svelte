<script lang="ts">
	import { goto } from '$app/navigation';
	import type { MealSlotItem, MealPlanResponse } from '$lib/api/mealPlanApi';
	import type { Recipe } from '$lib/api/recipeApi';
	import type { MealPlanShareResponse, SharedMealPlanResponse } from '$lib/api/sharingApi';
	import WeekNavigator from '$lib/components/meal-plans/WeekNavigator.svelte';
	import MealPlanGrid from '$lib/components/meal-plans/MealPlanGrid.svelte';
	import AddItemModal from '$lib/components/meal-plans/AddItemModal.svelte';
	import CopyModal from '$lib/components/meal-plans/CopyModal.svelte';
	import ShareModal from '$lib/components/meal-plans/ShareModal.svelte';
	import SharedMealPlanCard from '$lib/components/meal-plans/SharedMealPlanCard.svelte';
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
	// svelte-ignore state_referenced_locally
	let sharedWithMe: SharedMealPlanResponse[] = $state(data.sharedWithMe);
	// svelte-ignore state_referenced_locally
	let myShares: MealPlanShareResponse[] = $state(data.myShares);

	// Re-sync when server data changes (e.g. week navigation)
	$effect(() => {
		// Subscribe to the derived value so this re-runs on navigation
		void serverVersion;
		mealPlan = data.mealPlan;
		recipes = data.recipes;
		sharedWithMe = data.sharedWithMe;
		myShares = data.myShares;
	});

	// Modal state
	let addModalOpen = $state(false);
	let addModalDay = $state('');
	let addModalCategory = $state('');

	let copyModalOpen = $state(false);
	let copyModalDay = $state('');
	let copyModalCategory = $state('');

	let shareModalOpen = $state(false);

	// Track which shared plan is being edited (null = editing own plan)
	let editingSharedPlan: { ownerUserId: string; shareId: string } | null = $state(null);

	// Shared-with-me section collapsed state
	let sharedSectionOpen = $state(true);

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
		editingSharedPlan = null;
		addModalDay = day;
		addModalCategory = category;
		addModalOpen = true;
	}

	/** Get the effective meal plan being edited (own or shared) */
	function getActivePlan(): MealPlanResponse {
		if (editingSharedPlan) {
			const shared = sharedWithMe.find((s) => s.shareId === editingSharedPlan!.shareId);
			if (!shared) {
				editingSharedPlan = null;
				return mealPlan;
			}
			return shared.mealPlan;
		}
		return mealPlan;
	}

	/** Build query params, appending onBehalfOf when editing a shared plan */
	function buildParams(base: Record<string, string>): URLSearchParams {
		const params = new URLSearchParams(base);
		if (editingSharedPlan) {
			params.set('onBehalfOf', editingSharedPlan.ownerUserId);
		}
		return params;
	}

	/** Update the correct plan (own or shared) after a successful mutation */
	function applyUpdatedPlan(updated: MealPlanResponse) {
		if (editingSharedPlan) {
			sharedWithMe = sharedWithMe.map((s) =>
				s.shareId === editingSharedPlan!.shareId ? { ...s, mealPlan: updated } : s
			);
		} else {
			mealPlan = updated;
		}
	}

	async function handleAddItem(item: MealSlotItem) {
		const plan = getActivePlan();
		const dayPlan = plan.days.find((d) => d.day === addModalDay);
		if (!dayPlan) return;

		// Optimistic update
		const currentItems = dayPlan.slots[addModalCategory] ?? [];
		const newItems = [...currentItems, item];
		dayPlan.slots[addModalCategory] = newItems;

		try {
			const params = buildParams({
				weekStart: plan.weekStart,
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
			applyUpdatedPlan(updated);
		} catch {
			// Revert
			dayPlan.slots[addModalCategory] = currentItems;
			showToast('Failed to add item. Please try again.', 'error');
		}
	}

	// ── Remove Item ──

	async function handleRemoveItem(day: string, category: string, index: number) {
		const plan = getActivePlan();
		const dayPlan = plan.days.find((d) => d.day === day);
		if (!dayPlan) return;

		const currentItems = [...(dayPlan.slots[category] ?? [])];
		const newItems = currentItems.filter((_, i) => i !== index);

		// Optimistic update
		dayPlan.slots[category] = newItems;

		try {
			const params = buildParams({
				weekStart: plan.weekStart,
				day,
				category,
				itemIndex: index.toString()
			});
			const res = await fetch(`/app/meal-plans?${params}`, {
				method: 'DELETE'
			});

			if (!res.ok) throw new Error('Failed to remove');
			const updated: MealPlanResponse = await res.json();
			applyUpdatedPlan(updated);
		} catch {
			// Revert
			dayPlan.slots[category] = currentItems;
			showToast('Failed to remove item. Please try again.', 'error');
		}
	}

	// ── Copy Category ──

	function handleOpenCopy(day: string, category: string) {
		editingSharedPlan = null;
		copyModalDay = day;
		copyModalCategory = category;
		copyModalOpen = true;
	}

	async function handleCopyConfirm(targetDays: string[]) {
		const plan = getActivePlan();
		// Optimistic update: copy source items to targets
		const sourceDayPlan = plan.days.find((d) => d.day === copyModalDay);
		if (!sourceDayPlan) return;

		const sourceItems = sourceDayPlan.slots[copyModalCategory] ?? [];
		const backups: Record<string, MealSlotItem[]> = {};

		for (const td of targetDays) {
			const targetDayPlan = plan.days.find((d) => d.day === td);
			if (targetDayPlan) {
				backups[td] = [...(targetDayPlan.slots[copyModalCategory] ?? [])];
				targetDayPlan.slots[copyModalCategory] = [...sourceItems];
			}
		}

		try {
			const params = buildParams({ weekStart: plan.weekStart });
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
			applyUpdatedPlan(updated);
			showToast(
				`Copied ${copyModalCategory} from ${copyModalDay} to ${targetDays.length} ${targetDays.length === 1 ? 'day' : 'days'}`
			);
		} catch {
			// Revert
			for (const td of targetDays) {
				const targetDayPlan = plan.days.find((d) => d.day === td);
				if (targetDayPlan && backups[td]) {
					targetDayPlan.slots[copyModalCategory] = backups[td];
				}
			}
			showToast('Failed to copy. Please try again.', 'error');
		}
	}

	// ── Shared Plan Edit Wrappers ──

	function handleSharedOpenAdd(ownerUserId: string, shareId: string) {
		return (day: string, category: string) => {
			editingSharedPlan = { ownerUserId, shareId };
			addModalDay = day;
			addModalCategory = category;
			addModalOpen = true;
		};
	}

	function handleSharedRemove(ownerUserId: string, shareId: string) {
		return (day: string, category: string, index: number) => {
			editingSharedPlan = { ownerUserId, shareId };
			handleRemoveItem(day, category, index);
		};
	}

	function handleSharedOpenCopy(ownerUserId: string, shareId: string) {
		return (day: string, category: string) => {
			editingSharedPlan = { ownerUserId, shareId };
			copyModalDay = day;
			copyModalCategory = category;
			copyModalOpen = true;
		};
	}

	// ── Sharing ──

	async function handleShare(
		email: string,
		permission: string
	): Promise<string | null> {
		try {
			const params = new URLSearchParams();
			const res = await fetch(`/app/meal-plans/sharing?${params}`, {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify({
					email,
					weekStart: mealPlan.weekStart,
					permission
				})
			});

			if (!res.ok) {
				const body = await res.json();
				return body.error ?? 'Failed to share meal plan.';
			}

			// Refresh the shares list
			const sharesRes = await fetch(
				`/app/meal-plans/sharing?type=my-shares&weekStart=${mealPlan.weekStart}`
			);
			if (sharesRes.ok) myShares = await sharesRes.json();

			showToast(`Meal plan shared with ${email}`);
			return null;
		} catch (err) {
			return err instanceof Error ? err.message : 'Failed to share meal plan.';
		}
	}

	async function handleRevoke(shareId: string) {
		try {
			const res = await fetch(`/app/meal-plans/sharing?shareId=${shareId}`, {
				method: 'DELETE'
			});
			if (!res.ok) throw new Error('Failed to revoke');

			myShares = myShares.filter((s) => s.id !== shareId);
			showToast('Share revoked');
		} catch {
			showToast('Failed to revoke share.', 'error');
		}
	}

	async function handleDismiss(shareId: string) {
		try {
			const res = await fetch(
				`/app/meal-plans/sharing?action=dismiss&shareId=${shareId}`,
				{ method: 'POST' }
			);
			if (!res.ok) throw new Error('Failed to dismiss');

			sharedWithMe = sharedWithMe.filter((s) => s.shareId !== shareId);
			editingSharedPlan = null;
			showToast('Shared plan dismissed');
		} catch {
			showToast('Failed to dismiss shared plan.', 'error');
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
			<p class="mt-1 text-charcoal/60">
				Plan your weekly meals — drag recipes into slots and copy across days.
			</p>
		</div>
		<button
			onclick={() => (shareModalOpen = true)}
			class="flex items-center gap-1.5 rounded-lg bg-charcoal/5 px-3 py-2 text-sm font-medium text-charcoal transition-colors hover:bg-charcoal/10"
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
					d="M7.217 10.907a2.25 2.25 0 100 2.186m0-2.186c.18.324.283.696.283 1.093s-.103.77-.283 1.093m0-2.186l9.566-5.314m-9.566 7.5l9.566 5.314m0 0a2.25 2.25 0 103.935 2.186 2.25 2.25 0 00-3.935-2.186zm0-12.814a2.25 2.25 0 103.933-2.185 2.25 2.25 0 00-3.933 2.185z"
				/>
			</svg>
			Share
		</button>
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

	<!-- Share Modal -->
	<ShareModal
		open={shareModalOpen}
		weekStart={mealPlan.weekStart}
		shares={myShares}
		onShare={handleShare}
		onRevoke={handleRevoke}
		onClose={() => (shareModalOpen = false)}
	/>

	<!-- Shared with me section -->
	{#if sharedWithMe.length > 0}
		<div class="mt-8">
			<button
				onclick={() => (sharedSectionOpen = !sharedSectionOpen)}
				aria-expanded={sharedSectionOpen}
				aria-label="Toggle shared meal plans section"
				class="mb-3 flex items-center gap-2 text-left"
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					class="h-4 w-4 text-charcoal/40 transition-transform {sharedSectionOpen
						? 'rotate-180'
						: ''}"
					fill="none"
					viewBox="0 0 24 24"
					stroke="currentColor"
					stroke-width="2"
				>
					<path
						stroke-linecap="round"
						stroke-linejoin="round"
						d="M19.5 8.25l-7.5 7.5-7.5-7.5"
					/>
				</svg>
				<h2 class="font-display text-lg font-semibold text-charcoal">Shared with me</h2>
				<span
					class="rounded-full bg-blue-100 px-2 py-0.5 text-xs font-semibold text-blue-600"
				>
					{sharedWithMe.length}
				</span>
			</button>

			{#if sharedSectionOpen}
				<div class="flex flex-col gap-3">
					{#each sharedWithMe as shared (shared.shareId)}
						<SharedMealPlanCard
							shareId={shared.shareId}
							ownerUserId={shared.ownerUserId}
							ownerName={shared.ownerName}
							ownerEmail={shared.ownerEmail}
							permission={shared.permission}
							mealPlan={shared.mealPlan}
							onDismiss={handleDismiss}
							onAdd={shared.permission === 'ReadWrite'
								? handleSharedOpenAdd(shared.ownerUserId, shared.shareId)
								: undefined}
							onRemove={shared.permission === 'ReadWrite'
								? handleSharedRemove(shared.ownerUserId, shared.shareId)
								: undefined}
							onCopy={shared.permission === 'ReadWrite'
								? handleSharedOpenCopy(shared.ownerUserId, shared.shareId)
								: undefined}
						/>
					{/each}
				</div>
			{/if}
		</div>
	{/if}

	<!-- Toast notification -->
	{#if toastVisible}
		<div
			class="fixed bottom-6 left-6 right-6 z-50 flex items-center gap-2 rounded-lg px-4 py-3 shadow-lg transition-all sm:left-auto {toastType ===
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

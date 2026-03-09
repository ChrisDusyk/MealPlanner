<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { onMount } from 'svelte';
	import { SvelteURLSearchParams } from 'svelte/reactivity';
	import type { MealSlotItem, MealPlanResponse } from '$lib/api/mealPlanApi';
	import type { Recipe } from '$lib/api/recipeApi';
	import type { MealPlanShareResponse, SharedMealPlanResponse } from '$lib/api/sharingApi';
	import {
		MealPlanRealtimeClient,
		type MealPlanUpdatedEvent
	} from '$lib/realtime/mealPlanRealtime';
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
		currentUserId = data.appUser?.auth0UserId ?? null;
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

	// Promise chain for slot mutations (prevents out-of-order response overwrites)
	let pendingSlotUpdate: Promise<void> = Promise.resolve();

	// Shared-with-me section collapsed state
	let sharedSectionOpen = $state(true);

	// Toast state
	let toastMessage = $state('');
	let toastType: 'success' | 'error' = $state('success');
	let toastVisible = $state(false);
	let generateGroceryListLoading = $state(false);
	const realtimeClient = new MealPlanRealtimeClient();
	// svelte-ignore state_referenced_locally
	let currentUserId: string | null = $state(data.appUser?.auth0UserId ?? null);

	function applyRealtimeUpdate(event: MealPlanUpdatedEvent) {
		if (event.weekStart !== mealPlan.weekStart) {
			return;
		}

		if (currentUserId && event.ownerUserId === currentUserId) {
			mealPlan = event.mealPlan;
		}

		let sharedPlanUpdated = false;
		const nextSharedWithMe = sharedWithMe.map((shared) => {
			if (shared.ownerUserId === event.ownerUserId) {
				sharedPlanUpdated = true;
				return {
					...shared,
					mealPlan: event.mealPlan
				};
			}

			return shared;
		});

		if (sharedPlanUpdated) {
			sharedWithMe = nextSharedWithMe;
		}
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
		// @ts-expect-error resolve() with query string is valid at runtime
		goto(resolve(`/app/meal-plans?weekStart=${weekStart}`));
	}

	async function handleGenerateGroceryList() {
		if (generateGroceryListLoading) return;
		generateGroceryListLoading = true;

		try {
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
			// @ts-expect-error resolve() with query string is valid at runtime
			await goto(resolve(`/app/grocery-lists?${redirectParams}`));
		} catch (err) {
			showToast(err instanceof Error ? err.message : 'Failed to generate grocery list.', 'error');
		} finally {
			generateGroceryListLoading = false;
		}
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
		const params = new SvelteURLSearchParams(base);
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

	function queueSlotMutation(mutation: () => Promise<void>): Promise<void> {
		const next = pendingSlotUpdate.then(mutation, mutation);
		pendingSlotUpdate = next.catch(() => {
			// Keep queue alive after a failure (errors handled in each mutation)
		});
		return next;
	}

	async function handleAddItem(item: MealSlotItem) {
		const plan = getActivePlan();
		const dayPlan = plan.days.find((d) => d.day === addModalDay);
		if (!dayPlan) return;

		// Optimistic update
		const currentItems = dayPlan.slots[addModalCategory] ?? [];
		const newItems = [...currentItems, item];
		dayPlan.slots[addModalCategory] = newItems;

		await queueSlotMutation(async () => {
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
		});
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

	// ── Update Servings ──

	async function handleUpdateServings(
		day: string,
		category: string,
		index: number,
		servings: number
	) {
		const plan = getActivePlan();
		const dayPlan = plan.days.find((d) => d.day === day);
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
				const params = buildParams({
					weekStart: plan.weekStart,
					day,
					category
				});
				const res = await fetch(`/app/meal-plans?${params}`, {
					method: 'PUT',
					headers: { 'Content-Type': 'application/json' },
					body: JSON.stringify({ items: newItems })
				});

				if (!res.ok) throw new Error('Failed to update servings');
				const updated: MealPlanResponse = await res.json();
				applyUpdatedPlan(updated);
			} catch {
				// Revert
				dayPlan.slots[category] = currentItems;
				showToast('Failed to update servings. Please try again.', 'error');
			}
		});
	}

	// ── Copy Category ──

	function handleOpenCopy(day: string, category: string) {
		editingSharedPlan = null;
		copyModalDay = day;
		copyModalCategory = category;
		copyModalOpen = true;
	}

	async function handleCopyConfirm(targetDays: string[]) {
		// Wait for queued slot updates (e.g. servings change) to persist
		await pendingSlotUpdate;

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

	function handleSharedUpdateServings(ownerUserId: string, shareId: string) {
		return (day: string, category: string, index: number, servings: number) => {
			editingSharedPlan = { ownerUserId, shareId };
			handleUpdateServings(day, category, index, servings);
		};
	}

	// ── Sharing ──

	async function handleShare(email: string, permission: string): Promise<string | null> {
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
			const res = await fetch(`/app/meal-plans/sharing?action=dismiss&shareId=${shareId}`, {
				method: 'POST'
			});
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
			<p class="mt-1 text-charcoal/80">
				Plan your weekly meals — drag recipes into slots and copy across days.
			</p>
		</div>
		<div class="flex flex-wrap items-center gap-2">
			<button
				type="button"
				onclick={handleGenerateGroceryList}
				disabled={generateGroceryListLoading}
				aria-label="Generate grocery list for this meal plan week"
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

			<button
				type="button"
				onclick={() => (shareModalOpen = true)}
				class="flex min-h-10 items-center gap-1.5 rounded-lg bg-charcoal/5 px-3 py-2 text-sm font-medium text-charcoal transition-colors hover:bg-charcoal/10"
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
				type="button"
				onclick={() => (sharedSectionOpen = !sharedSectionOpen)}
				aria-expanded={sharedSectionOpen}
				aria-controls="shared-meal-plans"
				aria-label="Toggle shared meal plans section"
				class="mb-3 flex min-h-10 items-center gap-2 text-left"
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					class="h-4 w-4 text-charcoal/60 transition-transform {sharedSectionOpen
						? 'rotate-180'
						: ''}"
					fill="none"
					viewBox="0 0 24 24"
					stroke="currentColor"
					stroke-width="2"
				>
					<path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
				</svg>
				<h2 class="font-display text-lg font-semibold text-charcoal">Shared with me</h2>
				<span class="rounded-full bg-blue-100 px-2 py-0.5 text-xs font-semibold text-blue-600">
					{sharedWithMe.length}
				</span>
			</button>

			{#if sharedSectionOpen}
				<div id="shared-meal-plans" class="flex flex-col gap-3">
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
							onUpdateServings={shared.permission === 'ReadWrite'
								? handleSharedUpdateServings(shared.ownerUserId, shared.shareId)
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
			role={toastType === 'error' ? 'alert' : 'status'}
			aria-live={toastType === 'error' ? 'assertive' : 'polite'}
			aria-atomic="true"
			class="fixed right-6 bottom-6 left-6 z-50 flex items-center gap-2 rounded-lg px-4 py-3 shadow-lg transition-all sm:left-auto {toastType ===
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

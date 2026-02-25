<script lang="ts">
	import { goto, invalidateAll } from '$app/navigation';
	import { enhance } from '$app/forms';
	import { onMount } from 'svelte';
	import type { GroceryListResponse, GroceryListItem } from '$lib/api/groceryListApi';
	import type { GroceryListShareResponse, SharedGroceryListResponse } from '$lib/api/sharingApi';
	import {
		GroceryListRealtimeClient,
		type GroceryListUpdatedEvent
	} from '$lib/realtime/groceryListRealtime';
	import WeekNavigator from '$lib/components/meal-plans/WeekNavigator.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	// svelte-ignore state_referenced_locally
	let groceryList: GroceryListResponse | null = $state(data.groceryList);
	// svelte-ignore state_referenced_locally
	let weekStart: string = $state(data.weekStart);
	// svelte-ignore state_referenced_locally
	let sharedWithMe: SharedGroceryListResponse[] = $state(data.sharedWithMe);
	let newItemName = $state('');
	let loading = $state(false);
	let deleteConfirmOpen = $state(false);

	// Sharing panel state
	let sharePanelOpen = $state(false);
	let shareEmail = $state('');
	let sharePermission = $state('ReadOnly');
	let shareLoading = $state(false);
	let myShares: GroceryListShareResponse[] = $state([]);
	let sharesLoaded = $state(false);
	let sharedDraftNames: Record<string, string> = $state({});

	// Toast state
	let toastMessage = $state('');
	let toastType: 'success' | 'error' = $state('success');
	let toastVisible = $state(false);
	const realtimeClient = new GroceryListRealtimeClient();

	// Re-sync when server data changes
	let serverVersion = $derived(data.weekStart + (data.groceryList?.id ?? 'none'));

	$effect(() => {
		void serverVersion;
		groceryList = data.groceryList;
		weekStart = data.weekStart;
		sharedWithMe = data.sharedWithMe;
		// Reset share state when week changes
		shareEmail = '';
		sharePanelOpen = false;
		myShares = [];
		sharesLoaded = false;
		sharedDraftNames = {};
	});

	function showToast(message: string, type: 'success' | 'error' = 'success') {
		toastMessage = message;
		toastType = type;
		toastVisible = true;
		setTimeout(() => {
			toastVisible = false;
		}, 3000);
	}

	function applyRealtimeUpdate(event: GroceryListUpdatedEvent) {
		if (event.weekStart !== weekStart) {
			return;
		}

		if (groceryList && groceryList.id === event.groceryList.id) {
			groceryList = event.groceryList;
		}

		let sharedListUpdated = false;
		const nextSharedWithMe = sharedWithMe.map((shared) => {
			if (
				shared.ownerUserId === event.ownerUserId &&
				shared.groceryList.id === event.groceryList.id
			) {
				sharedListUpdated = true;
				return {
					...shared,
					groceryList: event.groceryList
				};
			}

			return shared;
		});

		if (sharedListUpdated) {
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
				console.error('Failed to start grocery list realtime connection', err);
			});

		return () => {
			disposed = true;
			void realtimeClient.stop();
		};
	});

	// ── Navigation ──

	function handleNavigate(newWeekStart: string) {
		goto(`/app/grocery-lists?weekStart=${newWeekStart}`);
	}

	// ── Sorted items ──

	let sortedItems = $derived.by(() => {
		if (!groceryList) return [];
		const unchecked = groceryList.items
			.map((item, index) => ({ ...item, originalIndex: index }))
			.filter((i) => !i.isChecked);
		const checked = groceryList.items
			.map((item, index) => ({ ...item, originalIndex: index }))
			.filter((i) => i.isChecked);
		return [...unchecked, ...checked];
	});

	let checkedCount = $derived(groceryList?.items.filter((i) => i.isChecked).length ?? 0);
	let totalCount = $derived(groceryList?.items.length ?? 0);
	let progress = $derived(totalCount > 0 ? (checkedCount / totalCount) * 100 : 0);

	function getSortedSharedItems(shared: SharedGroceryListResponse) {
		const unchecked = shared.groceryList.items
			.map((item, index) => ({ ...item, originalIndex: index }))
			.filter((i) => !i.isChecked);
		const checked = shared.groceryList.items
			.map((item, index) => ({ ...item, originalIndex: index }))
			.filter((i) => i.isChecked);
		return [...unchecked, ...checked];
	}

	// ── Sharing ──

	async function openSharePanel() {
		sharePanelOpen = true;
		if (!sharesLoaded) {
			try {
				const resp = await fetch(
					`/app/grocery-lists/sharing?type=my-shares&weekStart=${weekStart}`
				);
				if (resp.ok) {
					myShares = await resp.json();
				}
			} catch {
				// ignore
			}
			sharesLoaded = true;
		}
	}

	async function handleShare() {
		if (!shareEmail.trim()) return;
		shareLoading = true;
		try {
			const resp = await fetch('/app/grocery-lists/sharing', {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify({
					email: shareEmail.trim(),
					weekStart,
					permission: sharePermission
				})
			});
			if (resp.ok) {
				const newShare = await resp.json();
				myShares = [...myShares, newShare];
				shareEmail = '';
				showToast(`Shared with ${newShare.sharedWithEmail}`);
			} else {
				const body = await resp.json().catch(() => ({}));
				showToast(body?.error ?? 'Failed to share', 'error');
			}
		} catch {
			showToast('Failed to share', 'error');
		} finally {
			shareLoading = false;
		}
	}

	async function handleRevoke(shareId: string, email: string) {
		try {
			const resp = await fetch(`/app/grocery-lists/sharing?shareId=${shareId}`, {
				method: 'DELETE'
			});
			if (resp.ok) {
				myShares = myShares.filter((s) => s.id !== shareId);
				showToast(`Revoked access for ${email}`);
			} else {
				showToast('Failed to revoke', 'error');
			}
		} catch {
			showToast('Failed to revoke', 'error');
		}
	}

	async function handleDismiss(shareId: string) {
		try {
			const resp = await fetch(`/app/grocery-lists/sharing?action=dismiss&shareId=${shareId}`, {
				method: 'POST'
			});
			if (resp.ok) {
				sharedWithMe = sharedWithMe.filter((s) => s.shareId !== shareId);
				showToast('Dismissed');
			} else {
				showToast('Failed to dismiss', 'error');
			}
		} catch {
			showToast('Failed to dismiss', 'error');
		}
	}

	async function handleToggleShared(ownerUserId: string, shareId: string, itemIndex: number) {
		const sharedEntry = sharedWithMe.find((s) => s.shareId === shareId);
		if (!sharedEntry) return;
		if (sharedEntry.permission !== 'ReadWrite') {
			showToast('You only have view access to this list', 'error');
			return;
		}

		// Optimistic toggle
		sharedEntry.groceryList.items[itemIndex].isChecked =
			!sharedEntry.groceryList.items[itemIndex].isChecked;
		sharedWithMe = [...sharedWithMe];

		try {
			const params = new URLSearchParams({
				weekStart,
				ownerUserId
			});
			const resp = await fetch(
				`/app/grocery-lists/toggle-shared?itemIndex=${itemIndex}&${params}`,
				{
					method: 'PUT'
				}
			);
			if (resp.ok) {
				const updated = await resp.json();
				sharedEntry.groceryList = updated;
				sharedWithMe = [...sharedWithMe];
			} else {
				// Revert optimistic toggle
				sharedEntry.groceryList.items[itemIndex].isChecked =
					!sharedEntry.groceryList.items[itemIndex].isChecked;
				sharedWithMe = [...sharedWithMe];
				showToast('Failed to update item', 'error');
			}
		} catch {
			// Revert
			sharedEntry.groceryList.items[itemIndex].isChecked =
				!sharedEntry.groceryList.items[itemIndex].isChecked;
			sharedWithMe = [...sharedWithMe];
			showToast('Failed to update item', 'error');
		}
	}

	async function handleAddSharedItem(shareId: string, ownerUserId: string) {
		const sharedEntry = sharedWithMe.find((s) => s.shareId === shareId);
		if (!sharedEntry) return;
		if (sharedEntry.permission !== 'ReadWrite') {
			showToast('You only have view access to this list', 'error');
			return;
		}

		const name = (sharedDraftNames[shareId] ?? '').trim();
		if (!name) return;

		try {
			const params = new URLSearchParams({ weekStart, ownerUserId });
			const resp = await fetch(`/app/grocery-lists/add-shared-item?${params}`, {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify({ name })
			});

			if (resp.ok) {
				const updated = await resp.json();
				sharedEntry.groceryList = updated;
				sharedWithMe = [...sharedWithMe];
				sharedDraftNames = { ...sharedDraftNames, [shareId]: '' };
				showToast('Item added');
			} else {
				const body = await resp.json().catch(() => ({}));
				showToast(body?.error ?? 'Failed to add item', 'error');
			}
		} catch {
			showToast('Failed to add item', 'error');
		}
	}
</script>

<svelte:head>
	<title>Grocery Lists — Simple Meal Planner</title>
</svelte:head>

<div class="mx-auto max-w-3xl">
	<!-- Page header -->
	<div class="mb-6">
		<h1 class="font-display text-2xl font-bold text-charcoal sm:text-3xl">Grocery List</h1>
		<p class="mt-1 text-charcoal/80">
			Generate a shopping list from your meal plan — all ingredients combined and ready to go.
		</p>
	</div>

	<!-- Week navigator -->
	<div class="mb-6">
		<WeekNavigator {weekStart} onNavigate={handleNavigate} />
	</div>

	{#if groceryList}
		<!-- Progress bar -->
		<div class="mb-6 rounded-xl border border-green-200/60 bg-white p-4 shadow-sm">
			<div class="mb-2 flex items-center justify-between">
				<span class="font-display text-sm font-semibold text-charcoal">
					{checkedCount} of {totalCount} items checked
				</span>
				<span class="text-xs font-medium text-green-700">{Math.round(progress)}%</span>
			</div>
			<div class="h-2 overflow-hidden rounded-full bg-green-100">
				<div
					class="h-full rounded-full bg-gradient-to-r from-green-500 to-green-400 transition-all duration-300"
					style="width: {progress}%"
				></div>
			</div>
		</div>

		<!-- Actions bar -->
		<div class="mb-4 flex flex-wrap items-center gap-2">
			<form
				method="POST"
				action="?/generate"
				use:enhance={() => {
					loading = true;
					return async ({ result, update }) => {
						loading = false;
						if (result.type === 'success' && result.data?.groceryList) {
							groceryList = result.data.groceryList as GroceryListResponse;
							showToast('Grocery list regenerated');
							await invalidateAll();
						} else {
							showToast('Failed to regenerate list', 'error');
						}
						await update({ reset: false });
					};
				}}
			>
				<input type="hidden" name="weekStart" value={weekStart} />
				<button
					type="submit"
					disabled={loading}
					class="flex items-center gap-1.5 rounded-lg bg-green-600 px-3 py-2 text-sm font-medium text-white shadow-sm transition-colors hover:bg-green-700 disabled:opacity-50"
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
							d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0 3.181 3.183a8.25 8.25 0 0 0 13.803-3.7M4.031 9.865a8.25 8.25 0 0 1 13.803-3.7l3.181 3.182"
						/>
					</svg>
					Regenerate
				</button>
			</form>

			<div class="relative">
				<button
					type="button"
					onclick={openSharePanel}
					class="flex items-center gap-1.5 rounded-lg bg-blue-50 px-3 py-2 text-sm font-medium text-blue-700 transition-colors hover:bg-blue-100"
					aria-label="Share this grocery list"
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
							d="M7.217 10.907a2.25 2.25 0 1 0 0 2.186m0-2.186c.18.324.283.696.283 1.093s-.103.77-.283 1.093m0-2.186 9.566-5.314m-9.566 7.5 9.566 5.314m0 0a2.25 2.25 0 1 0 3.935 2.186 2.25 2.25 0 0 0-3.935-2.186Zm0-12.814a2.25 2.25 0 1 0 3.933-2.185 2.25 2.25 0 0 0-3.933 2.185Z"
						/>
					</svg>
					Share
				</button>

				{#if sharePanelOpen}
					<div
						class="absolute top-full left-0 z-20 mt-2 w-[min(38rem,calc(100vw-2rem))] rounded-xl border border-blue-200/60 bg-blue-50 p-4 shadow-lg"
					>
						<div class="mb-3 flex items-center justify-between">
							<h3 class="font-display text-sm font-semibold text-charcoal">Share this list</h3>
							<button
								type="button"
								onclick={() => (sharePanelOpen = false)}
								class="text-charcoal/40 hover:text-charcoal/70"
								aria-label="Close share panel"
							>
								<svg
									xmlns="http://www.w3.org/2000/svg"
									class="h-4 w-4"
									fill="none"
									viewBox="0 0 24 24"
									stroke="currentColor"
									stroke-width="2"
									><path
										stroke-linecap="round"
										stroke-linejoin="round"
										d="M6 18L18 6M6 6l12 12"
									/></svg
								>
							</button>
						</div>
						<div class="flex gap-2">
							<input
								type="email"
								bind:value={shareEmail}
								placeholder="Recipient email..."
								class="flex-1 rounded-lg border border-blue-200 bg-white px-3 py-2 text-sm text-charcoal placeholder-charcoal/40 shadow-sm focus:border-blue-400 focus:ring-1 focus:ring-blue-400 focus:outline-none"
							/>
							<select
								bind:value={sharePermission}
								class="rounded-lg border border-blue-200 bg-white px-2 py-2 text-sm text-charcoal shadow-sm focus:border-blue-400 focus:outline-none"
							>
								<option value="ReadOnly">View only</option>
								<option value="ReadWrite">Can edit</option>
							</select>
							<button
								type="button"
								onclick={handleShare}
								disabled={!shareEmail.trim() || shareLoading}
								class="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:opacity-50"
								>Share</button
							>
						</div>

						{#if myShares.length > 0}
							<ul class="mt-3 flex flex-col gap-1" role="list" aria-label="Current shares">
								{#each myShares as share (share.id)}
									<li
										class="flex items-center justify-between rounded-lg bg-white px-3 py-2 text-sm shadow-sm"
									>
										<div>
											<span class="font-medium text-charcoal">{share.sharedWithEmail}</span>
											{#if share.sharedWithName}
												<span class="ml-1 text-charcoal/50">({share.sharedWithName})</span>
											{/if}
											<span
												class="ml-2 rounded-full bg-blue-100 px-2 py-0.5 text-[10px] font-medium text-blue-700"
												>{share.permission === 'ReadOnly' ? 'View only' : 'Can edit'}</span
											>
										</div>
										<button
											type="button"
											onclick={() => handleRevoke(share.id, share.sharedWithEmail)}
											class="text-xs text-red-500 hover:text-red-700"
											aria-label="Revoke access for {share.sharedWithEmail}">Revoke</button
										>
									</li>
								{/each}
							</ul>
						{:else if sharesLoaded}
							<p class="mt-3 text-xs text-charcoal/50">Not shared with anyone yet.</p>
						{/if}
					</div>
				{/if}
			</div>

			<button
				type="button"
				onclick={() => (deleteConfirmOpen = true)}
				class="flex items-center gap-1.5 rounded-lg bg-red-50 px-3 py-2 text-sm font-medium text-red-700 transition-colors hover:bg-red-100"
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
						d="m14.74 9-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 0 1-2.244 2.077H8.084a2.25 2.25 0 0 1-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 0 0-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 0 1 3.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 0 0-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 0 0-7.5 0"
					/>
				</svg>
				Delete List
			</button>
		</div>

		<!-- Add custom item -->
		<form
			method="POST"
			action="?/addItem"
			class="mb-6 flex gap-2"
			use:enhance={() => {
				loading = true;
				return async ({ result, update }) => {
					loading = false;
					if (result.type === 'success' && result.data?.groceryList) {
						groceryList = result.data.groceryList as GroceryListResponse;
						newItemName = '';
						showToast('Item added');
					} else {
						showToast('Failed to add item', 'error');
					}
					await update({ reset: false });
				};
			}}
		>
			<input type="hidden" name="weekStart" value={weekStart} />
			<input
				type="text"
				name="name"
				bind:value={newItemName}
				placeholder="Add a custom item..."
				class="flex-1 rounded-lg border border-green-200 bg-white px-3 py-2 text-sm text-charcoal placeholder-charcoal/40 shadow-sm transition-colors focus:border-green-400 focus:ring-1 focus:ring-green-400 focus:outline-none"
			/>
			<button
				type="submit"
				disabled={!newItemName.trim() || loading}
				class="rounded-lg bg-charcoal/5 px-4 py-2 text-sm font-medium text-charcoal transition-colors hover:bg-charcoal/10 disabled:opacity-40"
			>
				Add
			</button>
		</form>

		<!-- Grocery list items -->
		<ul class="flex flex-col gap-1" role="list" aria-label="Grocery list items">
			{#each sortedItems as item (item.originalIndex)}
				<li
					class="group flex items-start gap-3 rounded-lg border px-4 py-3 transition-all {item.isChecked
						? 'border-green-100 bg-green-50/50'
						: 'border-green-200/60 bg-white shadow-sm'}"
				>
					<form
						method="POST"
						action="?/toggle"
						use:enhance={() => {
							// Optimistic toggle
							if (groceryList) {
								groceryList.items[item.originalIndex].isChecked =
									!groceryList.items[item.originalIndex].isChecked;
								groceryList = groceryList;
							}
							return async ({ result, update }) => {
								if (result.type === 'success' && result.data?.groceryList) {
									groceryList = result.data.groceryList as GroceryListResponse;
								}
								await update({ reset: false });
							};
						}}
					>
						<input type="hidden" name="weekStart" value={weekStart} />
						<input type="hidden" name="itemIndex" value={item.originalIndex} />
						<button
							type="submit"
							class="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded border-2 transition-colors {item.isChecked
								? 'border-green-500 bg-green-500 text-white'
								: 'border-green-300 hover:border-green-400'}"
							aria-label="Toggle {item.name}"
						>
							{#if item.isChecked}
								<svg
									xmlns="http://www.w3.org/2000/svg"
									class="h-3 w-3"
									fill="none"
									viewBox="0 0 24 24"
									stroke="currentColor"
									stroke-width="3"
								>
									<path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5" />
								</svg>
							{/if}
						</button>
					</form>

					<div class="min-w-0 flex-1">
						<span
							class="block font-display text-sm font-medium transition-all {item.isChecked
								? 'text-charcoal/40 line-through'
								: 'text-charcoal'}"
						>
							{item.name}
						</span>
						{#if item.quantity > 0}
							<span
								class="mt-0.5 block text-xs {item.isChecked
									? 'text-charcoal/30'
									: 'text-charcoal/60'}"
							>
								{item.quantity}
								{item.unit}
							</span>
						{/if}
						{#if item.sourceRecipeNames.length > 0}
							<div class="mt-1 flex flex-wrap gap-1">
								{#each item.sourceRecipeNames as recipeName}
									<span
										class="rounded-full px-2 py-0.5 text-[10px] font-medium {item.isChecked
											? 'bg-green-100/50 text-green-600/40'
											: 'bg-green-100 text-green-700'}"
									>
										{recipeName}
									</span>
								{/each}
							</div>
						{/if}
					</div>
				</li>
			{/each}
		</ul>

		{#if groceryList.items.length === 0}
			<div class="py-12 text-center">
				<p class="text-charcoal/60">
					Your grocery list is empty. Add some meals to your plan and regenerate.
				</p>
			</div>
		{/if}
	{:else}
		<!-- Empty state — no list generated yet -->
		<div
			class="flex flex-col items-center rounded-2xl border-2 border-dashed border-green-200 bg-white/50 px-6 py-16 text-center"
		>
			<svg
				xmlns="http://www.w3.org/2000/svg"
				class="mb-4 h-16 w-16 text-green-300"
				fill="none"
				viewBox="0 0 24 24"
				stroke="currentColor"
				stroke-width="1"
			>
				<path
					stroke-linecap="round"
					stroke-linejoin="round"
					d="M2.25 3h1.386c.51 0 .955.343 1.087.835l.383 1.437M7.5 14.25a3 3 0 0 0-3 3h15.75m-12.75-3h11.218c1.121-2.3 2.1-4.684 2.924-7.138a60.114 60.114 0 0 0-16.536-1.84M7.5 14.25 5.106 5.272M6 20.25a.75.75 0 1 1-1.5 0 .75.75 0 0 1 1.5 0Zm12.75 0a.75.75 0 1 1-1.5 0 .75.75 0 0 1 1.5 0Z"
				/>
			</svg>
			<h2 class="mb-2 font-display text-lg font-semibold text-charcoal">No grocery list yet</h2>
			<p class="mb-6 max-w-sm text-sm text-charcoal/60">
				Generate a grocery list from your meal plan for this week. All recipe ingredients will be
				combined and quantities totaled.
			</p>
			<form
				method="POST"
				action="?/generate"
				use:enhance={() => {
					loading = true;
					return async ({ result, update }) => {
						loading = false;
						if (result.type === 'success' && result.data?.groceryList) {
							groceryList = result.data.groceryList as GroceryListResponse;
							showToast('Grocery list generated!');
							await invalidateAll();
						} else {
							showToast('Failed to generate grocery list', 'error');
						}
						await update({ reset: false });
					};
				}}
			>
				<input type="hidden" name="weekStart" value={weekStart} />
				<button
					type="submit"
					disabled={loading}
					class="flex items-center gap-2 rounded-xl bg-green-600 px-6 py-3 font-display text-sm font-semibold text-white shadow-md transition-all hover:bg-green-700 hover:shadow-lg disabled:opacity-50"
				>
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
							d="M9.813 15.904 9 18.75l-.813-2.846a4.5 4.5 0 0 0-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 0 0 3.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 0 0 3.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 0 0-3.09 3.09ZM18.259 8.715 18 9.75l-.259-1.035a3.375 3.375 0 0 0-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 0 0 2.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 0 0 2.455 2.456L21.75 6l-1.036.259a3.375 3.375 0 0 0-2.455 2.456ZM16.894 20.567 16.5 21.75l-.394-1.183a2.25 2.25 0 0 0-1.423-1.423L13.5 18.75l1.183-.394a2.25 2.25 0 0 0 1.423-1.423l.394-1.183.394 1.183a2.25 2.25 0 0 0 1.423 1.423l1.183.394-1.183.394a2.25 2.25 0 0 0-1.423 1.423Z"
						/>
					</svg>
					Generate Grocery List
				</button>
			</form>
		</div>
	{/if}

	<!-- Shared with me -->
	{#if sharedWithMe.length > 0}
		<section class="mt-10" aria-labelledby="shared-with-me-heading">
			<h2 id="shared-with-me-heading" class="mb-4 font-display text-lg font-semibold text-charcoal">
				Shared with me
			</h2>
			<div class="flex flex-col gap-6">
				{#each sharedWithMe as shared (shared.shareId)}
					<div class="rounded-xl border border-purple-200/60 bg-white p-4 shadow-sm">
						<div class="mb-3 flex items-center justify-between">
							<div>
								<span class="font-display text-sm font-semibold text-charcoal"
									>{shared.ownerName || shared.ownerEmail}'s list</span
								>
								<span
									class="ml-2 rounded-full bg-purple-100 px-2 py-0.5 text-[10px] font-medium text-purple-700"
								>
									{shared.permission === 'ReadOnly' ? 'View only' : 'Can edit'}
								</span>
							</div>
							<button
								type="button"
								onclick={() => handleDismiss(shared.shareId)}
								class="text-xs text-charcoal/40 hover:text-charcoal/70"
								aria-label="Dismiss {shared.ownerName || shared.ownerEmail}'s list">Dismiss</button
							>
						</div>
						{#if shared.permission === 'ReadWrite'}
							<div class="mb-3 flex gap-2">
								<input
									type="text"
									value={sharedDraftNames[shared.shareId] ?? ''}
									oninput={(e) => {
										const value = (e.currentTarget as HTMLInputElement).value;
										sharedDraftNames = { ...sharedDraftNames, [shared.shareId]: value };
									}}
									placeholder="Add a custom item..."
									class="flex-1 rounded-lg border border-purple-200 bg-white px-3 py-2 text-sm text-charcoal placeholder-charcoal/40 shadow-sm transition-colors focus:border-purple-400 focus:ring-1 focus:ring-purple-400 focus:outline-none"
								/>
								<button
									type="button"
									disabled={!(sharedDraftNames[shared.shareId] ?? '').trim()}
									onclick={() => handleAddSharedItem(shared.shareId, shared.ownerUserId)}
									class="rounded-lg bg-purple-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-purple-700 disabled:opacity-40"
								>
									Add
								</button>
							</div>
						{/if}
						<ul class="flex flex-col gap-1" role="list" aria-label="Shared grocery items">
							{#each getSortedSharedItems(shared) as item (item.originalIndex)}
								<li
									class="flex items-start gap-3 rounded-lg border border-purple-100 px-4 py-2 {item.isChecked
										? 'bg-purple-50/40 opacity-60'
										: 'bg-white'}"
								>
									{#if shared.permission === 'ReadWrite'}
										<button
											type="button"
											onclick={() =>
												handleToggleShared(shared.ownerUserId, shared.shareId, item.originalIndex)}
											class="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded border-2 transition-colors {item.isChecked
												? 'border-purple-500 bg-purple-500 text-white'
												: 'border-purple-300 hover:border-purple-400'}"
											aria-label="Toggle {item.name}"
										>
											{#if item.isChecked}
												<svg
													xmlns="http://www.w3.org/2000/svg"
													class="h-3 w-3"
													fill="none"
													viewBox="0 0 24 24"
													stroke="currentColor"
													stroke-width="3"
													><path
														stroke-linecap="round"
														stroke-linejoin="round"
														d="M4.5 12.75l6 6 9-13.5"
													/></svg
												>
											{/if}
										</button>
									{:else}
										<span
											class="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded border-2 {item.isChecked
												? 'border-purple-300 bg-purple-300'
												: 'border-purple-200'}"
											aria-hidden="true"
										>
											{#if item.isChecked}
												<svg
													xmlns="http://www.w3.org/2000/svg"
													class="h-3 w-3 text-white"
													fill="none"
													viewBox="0 0 24 24"
													stroke="currentColor"
													stroke-width="3"
													><path
														stroke-linecap="round"
														stroke-linejoin="round"
														d="M4.5 12.75l6 6 9-13.5"
													/></svg
												>
											{/if}
										</span>
									{/if}
									<div class="min-w-0 flex-1">
										<span
											class="block text-sm font-medium {item.isChecked
												? 'text-charcoal/40 line-through'
												: 'text-charcoal'}">{item.name}</span
										>
										{#if item.quantity > 0}
											<span class="text-xs text-charcoal/50">{item.quantity} {item.unit}</span>
										{/if}
									</div>
								</li>
							{/each}
						</ul>
					</div>
				{/each}
			</div>
		</section>
	{/if}

	<!-- Delete confirmation dialog -->
	{#if deleteConfirmOpen}
		<div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm">
			<div
				class="mx-4 w-full max-w-sm rounded-2xl bg-white p-6 shadow-xl"
				role="dialog"
				aria-labelledby="delete-confirm-title"
				tabindex="-1"
			>
				<h3 id="delete-confirm-title" class="mb-2 font-display text-lg font-semibold text-charcoal">
					Delete Grocery List?
				</h3>
				<p class="mb-6 text-sm text-charcoal/70">
					This will permanently delete your grocery list for this week. You can always regenerate it
					from your meal plan.
				</p>
				<div class="flex justify-end gap-2">
					<button
						type="button"
						onclick={() => (deleteConfirmOpen = false)}
						class="rounded-lg px-4 py-2 text-sm font-medium text-charcoal/70 transition-colors hover:bg-charcoal/5"
					>
						Cancel
					</button>
					<form
						method="POST"
						action="?/delete"
						use:enhance={() => {
							loading = true;
							deleteConfirmOpen = false;
							return async ({ result, update }) => {
								loading = false;
								if (result.type === 'success') {
									groceryList = null;
									showToast('Grocery list deleted');
								} else {
									showToast('Failed to delete list', 'error');
								}
								await update({ reset: false });
							};
						}}
					>
						<input type="hidden" name="weekStart" value={weekStart} />
						<button
							type="submit"
							class="rounded-lg bg-red-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-red-700"
						>
							Delete
						</button>
					</form>
				</div>
			</div>
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

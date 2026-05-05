<script lang="ts">
	import { invalidateAll } from '$app/navigation';
	import {
		type CatalogueRecipe,
		type CatalogueRecipeListItem,
		type CatalogueSort,
		type CatalogueTag
	} from '$lib/api/catalogueApi';
	import { sanitizeUrl } from '$lib/utils/url';
	import CatalogueRecipeCard from './CatalogueRecipeCard.svelte';

	let {
		open,
		onClose
	}: {
		open: boolean;
		onClose: () => void;
	} = $props();

	let search = $state('');
	let debouncedSearch = $state('');
	let selectedTags = $state<string[]>([]);
	let sort = $state<CatalogueSort>('recent');
	let items = $state<CatalogueRecipeListItem[]>([]);
	let tags = $state<CatalogueTag[]>([]);
	let loading = $state(false);
	let loadError = $state<string | null>(null);
	let addingId = $state<string | null>(null);
	let toast = $state<{ message: string; type: 'success' | 'error' } | null>(null);
	let toastTimer: ReturnType<typeof setTimeout> | null = null;
	let detail = $state<CatalogueRecipe | null>(null);
	let detailLoading = $state(false);
	let detailError = $state<string | null>(null);

	let searchTimer: ReturnType<typeof setTimeout> | null = null;
	$effect(() => {
		const value = search;
		if (searchTimer) clearTimeout(searchTimer);
		searchTimer = setTimeout(() => {
			debouncedSearch = value.trim();
		}, 250);
		return () => {
			if (searchTimer) clearTimeout(searchTimer);
		};
	});

	$effect(() => {
		if (!open) return;
		void loadTags();
	});

	$effect(() => {
		if (!open) return;
		// Re-run when filters change
		const _ = [debouncedSearch, selectedTags.join(','), sort];
		void loadItems();
	});

	$effect(() => {
		if (!open) {
			detail = null;
			detailError = null;
		}
	});

	async function readJsonOrNull<T>(response: Response): Promise<T | null> {
		const contentType = response.headers.get('content-type') || '';
		if (!contentType.includes('application/json')) {
			return null;
		}
		return (await response.json().catch(() => null)) as T | null;
	}

	async function readErrorMessage(response: Response, fallback: string): Promise<string> {
		const body = await readJsonOrNull<{ error?: string; message?: string }>(response);
		if (body?.error) return body.error;
		if (body?.message) return body.message;
		const text = await response.text().catch(() => '');
		return text || fallback;
	}

	async function loadTags() {
		try {
			const response = await fetch('/app/recipes/catalogue/tags');
			if (!response.ok) {
				throw new Error(await readErrorMessage(response, 'Failed to load catalogue tags'));
			}
			const body = await readJsonOrNull<CatalogueTag[]>(response);
			tags = body ?? [];
		} catch (err) {
			console.error('Failed to load catalogue tags', err);
		}
	}

	async function loadItems() {
		loading = true;
		loadError = null;
		try {
			const params = new URLSearchParams();
			if (debouncedSearch) params.set('search', debouncedSearch);
			if (selectedTags.length > 0) params.set('tags', selectedTags.join(','));
			params.set('sort', sort);
			params.set('take', '60');

			const query = params.toString();
			const response = await fetch(`/app/recipes/catalogue${query ? `?${query}` : ''}`);
			if (!response.ok) {
				throw new Error(await readErrorMessage(response, 'Failed to load recipes'));
			}
			const body = await readJsonOrNull<CatalogueRecipeListItem[]>(response);
			items = body ?? [];
		} catch (err) {
			console.error('Failed to load catalogue', err);
			loadError = err instanceof Error ? err.message : 'Failed to load recipes';
		} finally {
			loading = false;
		}
	}

	function toggleTag(slug: string) {
		selectedTags = selectedTags.includes(slug)
			? selectedTags.filter((s) => s !== slug)
			: [...selectedTags, slug];
	}

	function showToast(message: string, type: 'success' | 'error' = 'success') {
		toast = { message, type };
		if (toastTimer) clearTimeout(toastTimer);
		toastTimer = setTimeout(() => {
			toast = null;
		}, 3500);
	}

	async function viewDetails(item: CatalogueRecipeListItem) {
		detail = null;
		detailLoading = true;
		detailError = null;
		try {
			const response = await fetch(`/app/recipes/catalogue/${encodeURIComponent(item.id)}`);
			if (!response.ok) {
				throw new Error(await readErrorMessage(response, 'Failed to load recipe'));
			}
			detail = await readJsonOrNull<CatalogueRecipe>(response);
		} catch (err) {
			detailError = err instanceof Error ? err.message : 'Failed to load recipe';
		} finally {
			detailLoading = false;
		}
	}

	async function addToMyRecipes(item: CatalogueRecipeListItem | CatalogueRecipe) {
		if ('alreadyAdded' in item && item.alreadyAdded) return;
		addingId = item.id;
		try {
			const response = await fetch(`/app/recipes/catalogue/${encodeURIComponent(item.id)}`, {
				method: 'POST'
			});

			if (!response.ok) {
				if (response.status === 409) {
					items = items.map((r) => (r.id === item.id ? { ...r, alreadyAdded: true } : r));
					showToast('That recipe is already in your collection', 'error');
					return;
				}

				throw new Error(await readErrorMessage(response, 'Failed to add recipe'));
			}

			items = items.map((r) => (r.id === item.id ? { ...r, alreadyAdded: true } : r));
			if (detail && detail.id === item.id) {
				detail = { ...detail, alreadyAdded: true, addCount: detail.addCount + 1 };
			}
			showToast(`Added “${item.name}” to your recipes`);
			await invalidateAll();
		} catch (err) {
			const message = err instanceof Error ? err.message : 'Failed to add recipe';
			showToast(message, 'error');
		} finally {
			addingId = null;
		}
	}

	function close() {
		onClose();
	}

	function onBackdropKey(event: KeyboardEvent) {
		if (event.key === 'Escape') close();
	}
</script>

<svelte:window onkeydown={open ? onBackdropKey : undefined} />

{#if open}
	<div
		class="fixed inset-0 z-50 flex items-stretch justify-center bg-charcoal/40 p-0 backdrop-blur-sm sm:items-center sm:p-6"
		role="dialog"
		aria-modal="true"
		aria-label="Browse recipe catalogue"
	>
		<button
			type="button"
			class="absolute inset-0 cursor-default"
			aria-label="Close catalogue"
			onclick={close}
		></button>

		<div
			class="relative flex h-full w-full max-w-5xl flex-col overflow-hidden rounded-none bg-white shadow-2xl sm:max-h-[90vh] sm:rounded-2xl"
		>
			<!-- Header -->
			<div class="flex items-center justify-between gap-4 border-b border-green-100 px-6 py-4">
				<div>
					<h2 class="font-display text-xl font-semibold text-charcoal">Recipe catalogue</h2>
					<p class="text-sm text-charcoal/50">Discover curated recipes and add them to your list</p>
				</div>
				<button
					type="button"
					class="rounded-lg p-2 text-charcoal/60 hover:bg-green-50 hover:text-charcoal"
					onclick={close}
					aria-label="Close"
				>
					<svg
						xmlns="http://www.w3.org/2000/svg"
						class="h-5 w-5"
						fill="none"
						viewBox="0 0 24 24"
						stroke="currentColor"
						stroke-width="2"
					>
						<path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
					</svg>
				</button>
			</div>

			<!-- Filter row -->
			<div class="border-b border-green-100 bg-green-50/40 px-6 py-3">
				<div class="flex flex-col gap-3 sm:flex-row sm:items-center">
					<label class="relative flex flex-1 items-center">
						<svg
							xmlns="http://www.w3.org/2000/svg"
							class="absolute left-3 h-4 w-4 text-charcoal/40"
							fill="none"
							viewBox="0 0 24 24"
							stroke="currentColor"
							stroke-width="2"
						>
							<path
								stroke-linecap="round"
								stroke-linejoin="round"
								d="m21 21-4.3-4.3M17 10.5a6.5 6.5 0 1 1-13 0 6.5 6.5 0 0 1 13 0Z"
							/>
						</svg>
						<input
							type="search"
							bind:value={search}
							placeholder="Search recipes…"
							class="w-full rounded-lg border border-green-200 bg-white py-2 pl-9 pr-3 text-sm text-charcoal placeholder:text-charcoal/40 focus:border-green-500 focus:outline-none focus:ring-2 focus:ring-green-200"
						/>
					</label>
					<select
						bind:value={sort}
						class="rounded-lg border border-green-200 bg-white px-3 py-2 text-sm text-charcoal focus:border-green-500 focus:outline-none focus:ring-2 focus:ring-green-200"
						aria-label="Sort"
					>
						<option value="recent">Most recent</option>
						<option value="popular">Most popular</option>
					</select>
				</div>

				{#if tags.length > 0}
					<div class="mt-3 flex flex-wrap gap-1.5">
						{#each tags as tag (tag.id)}
							{@const active = selectedTags.includes(tag.slug)}
							<button
								type="button"
								class="rounded-full px-3 py-1 text-xs font-medium transition-colors {active
									? 'bg-green-600 text-white'
									: 'bg-white text-charcoal/70 ring-1 ring-green-200 hover:bg-green-100'}"
								aria-pressed={active}
								onclick={() => toggleTag(tag.slug)}
							>
								{tag.name}
							</button>
						{/each}
						{#if selectedTags.length > 0}
							<button
								type="button"
								class="rounded-full px-3 py-1 text-xs font-medium text-charcoal/60 underline-offset-2 hover:underline"
								onclick={() => (selectedTags = [])}
							>
								Clear tags
							</button>
						{/if}
					</div>
				{/if}
			</div>

			<!-- Body -->
			<div class="flex-1 overflow-y-auto px-6 py-5">
				{#if loadError}
					<div class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
						{loadError}
					</div>
				{:else if loading && items.length === 0}
					<div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
						{#each Array(6) as _, i (i)}
							<div class="h-64 animate-pulse rounded-xl bg-green-50/80"></div>
						{/each}
					</div>
				{:else if items.length === 0}
					<div
						class="flex flex-col items-center justify-center rounded-xl border-2 border-dashed border-green-200 bg-green-50/40 px-6 py-16 text-center"
					>
						<h3 class="font-display text-lg font-semibold text-charcoal">No recipes found</h3>
						<p class="mt-1 text-sm text-charcoal/50">
							Try adjusting your search or removing some filters.
						</p>
					</div>
				{:else}
					<div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
						{#each items as item (item.id)}
							<CatalogueRecipeCard
								recipe={item}
								busy={addingId === item.id}
								onView={viewDetails}
								onAdd={addToMyRecipes}
							/>
						{/each}
					</div>
				{/if}
			</div>

			<!-- Toast -->
			{#if toast}
				<div
					class="pointer-events-none absolute bottom-4 left-1/2 -translate-x-1/2 rounded-lg px-4 py-2 text-sm font-medium shadow-lg {toast.type ===
					'success'
						? 'bg-green-600 text-white'
						: 'bg-red-600 text-white'}"
					role="status"
				>
					{toast.message}
				</div>
			{/if}

			<!-- Detail drawer -->
			{#if detail || detailLoading || detailError}
				{@const safeImage = detail ? sanitizeUrl(detail.imageUrl) : null}
				{@const safeSource = detail ? sanitizeUrl(detail.sourceUrl) : null}
				<div class="absolute inset-0 z-10 flex justify-end bg-charcoal/30">
					<button
						type="button"
						class="absolute inset-0 cursor-default"
						aria-label="Close details"
						onclick={() => {
							detail = null;
							detailError = null;
						}}
					></button>
					<aside
						class="relative flex h-full w-full max-w-md flex-col overflow-y-auto bg-white shadow-2xl"
					>
						<div class="flex items-center justify-between border-b border-green-100 px-5 py-3">
							<h3 class="font-display text-lg font-semibold text-charcoal">Recipe details</h3>
							<button
								type="button"
								class="rounded-lg p-2 text-charcoal/60 hover:bg-green-50"
								aria-label="Close details"
								onclick={() => {
									detail = null;
									detailError = null;
								}}
							>
								<svg
									xmlns="http://www.w3.org/2000/svg"
									class="h-5 w-5"
									fill="none"
									viewBox="0 0 24 24"
									stroke="currentColor"
									stroke-width="2"
								>
									<path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
								</svg>
							</button>
						</div>

						<div class="flex-1 px-5 py-4">
							{#if detailLoading}
								<div class="space-y-3">
									<div class="h-40 animate-pulse rounded-lg bg-green-50"></div>
									<div class="h-6 w-3/4 animate-pulse rounded bg-green-50"></div>
									<div class="h-4 w-full animate-pulse rounded bg-green-50"></div>
								</div>
							{:else if detailError}
								<div class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
									{detailError}
								</div>
							{:else if detail}
								{#if safeImage}
									<img
										src={safeImage}
										alt=""
										class="mb-4 aspect-[16/10] w-full rounded-lg object-cover"
									/>
								{/if}
								<h4 class="font-display text-xl font-semibold text-charcoal">{detail.name}</h4>
								{#if detail.description}
									<p class="mt-2 text-sm text-charcoal/70">{detail.description}</p>
								{/if}
								<div class="mt-3 flex flex-wrap gap-1.5">
									{#each detail.tags as tag (tag.id)}
										<span
											class="inline-flex items-center rounded-full bg-green-50 px-2 py-0.5 text-xs font-medium text-green-700"
										>
											{tag.name}
										</span>
									{/each}
								</div>

								<div class="mt-4 grid grid-cols-2 gap-2 text-sm text-charcoal/70">
									<div class="rounded-lg bg-green-50/60 px-3 py-2">
										<div class="text-xs text-charcoal/50">Servings</div>
										<div class="font-semibold">{detail.servings}</div>
									</div>
									<div class="rounded-lg bg-green-50/60 px-3 py-2">
										<div class="text-xs text-charcoal/50">Times added</div>
										<div class="font-semibold">{detail.addCount}</div>
									</div>
								</div>

								<h5 class="mt-5 font-display text-sm font-semibold text-charcoal">Ingredients</h5>
								<ul class="mt-2 space-y-1 text-sm text-charcoal/80">
									{#each detail.ingredients as ing, i (i)}
										<li class="flex items-baseline justify-between gap-2">
											<span>{ing.name}</span>
											<span class="text-xs text-charcoal/50">
												{ing.quantity}
												{ing.unit}
											</span>
										</li>
									{/each}
								</ul>

								{#if safeSource}
									<a
										href={safeSource}
										target="_blank"
										rel="noopener noreferrer"
										class="mt-5 inline-flex items-center gap-1.5 text-sm font-medium text-green-700 hover:underline"
									>
										View original source
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
												d="M13.5 6H6a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-7.5M14 4h6m0 0v6m0-6L10 14"
											/>
										</svg>
									</a>
								{/if}
							{/if}
						</div>

						{#if detail}
							<div class="border-t border-green-100 bg-white px-5 py-3">
								{#if detail.alreadyAdded}
									<span
										class="inline-flex w-full items-center justify-center gap-1.5 rounded-lg border border-green-200 bg-green-50 px-3 py-2 text-sm font-semibold text-green-700"
									>
										Already in your recipes
									</span>
								{:else}
									<button
										type="button"
										class="w-full rounded-lg bg-green-600 px-3 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-60"
										disabled={addingId === detail.id}
										onclick={() => detail && addToMyRecipes(detail)}
									>
										{addingId === detail.id ? 'Adding…' : 'Add to my recipes'}
									</button>
								{/if}
							</div>
						{/if}
					</aside>
				</div>
			{/if}
		</div>
	</div>
{/if}

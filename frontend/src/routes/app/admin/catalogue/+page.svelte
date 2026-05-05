<script lang="ts">
	import { goto, invalidateAll } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { untrack } from 'svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	let search = $state(untrack(() => data.search));
	let isPublishedFilter = $state<'all' | 'published' | 'draft'>(
		untrack(() =>
			data.isPublished === null ? 'all' : data.isPublished ? 'published' : 'draft'
		)
	);
	let busyId = $state<string | null>(null);
	let actionError = $state<string | null>(null);

	async function readErrorMessage(response: Response, fallback: string): Promise<string> {
		const contentType = response.headers.get('content-type') || '';
		if (contentType.includes('application/json')) {
			const body = await response.json().catch(() => ({}));
			if (typeof body?.error === 'string' && body.error) return body.error;
			if (typeof body?.message === 'string' && body.message) return body.message;
		}

		const text = await response.text().catch(() => '');
		return text || fallback;
	}

	function applyFilters() {
		const params = new URLSearchParams();
		if (search.trim()) params.set('search', search.trim());
		if (isPublishedFilter === 'published') params.set('isPublished', 'true');
		if (isPublishedFilter === 'draft') params.set('isPublished', 'false');
		const qs = params.toString();
		goto(`${resolve('/app/admin/catalogue')}${qs ? `?${qs}` : ''}`, { keepFocus: true });
	}

	async function togglePublished(id: string, current: boolean) {
		busyId = id;
		actionError = null;
		try {
			const response = await fetch(
				`/app/admin/catalogue/${encodeURIComponent(id)}/published`,
				{
					method: 'PUT',
					headers: { 'Content-Type': 'application/json' },
					body: JSON.stringify({ isPublished: !current })
				}
			);

			if (!response.ok) {
				throw new Error(await readErrorMessage(response, 'Failed to update'));
			}

			await invalidateAll();
		} catch (err) {
			actionError = err instanceof Error ? err.message : 'Failed to update';
		} finally {
			busyId = null;
		}
	}

	async function deleteRecipe(id: string, name: string) {
		if (!confirm(`Delete catalogue recipe "${name}"? This cannot be undone.`)) return;
		busyId = id;
		actionError = null;
		try {
			const response = await fetch(`/app/admin/catalogue/${encodeURIComponent(id)}`, {
				method: 'DELETE'
			});

			if (!response.ok) {
				throw new Error(await readErrorMessage(response, 'Failed to delete'));
			}

			await invalidateAll();
		} catch (err) {
			actionError = err instanceof Error ? err.message : 'Failed to delete';
		} finally {
			busyId = null;
		}
	}
</script>

<svelte:head>
	<title>Catalogue Admin | Meal Planner</title>
</svelte:head>

<div class="mx-auto max-w-6xl">
	<div class="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
		<div>
			<h1 class="font-display text-2xl font-bold text-charcoal sm:text-3xl">Recipe catalogue</h1>
			<p class="mt-1 text-charcoal/60">Curate the recipes any user can browse.</p>
		</div>
		<div class="flex flex-wrap gap-2">
			<a
				href={resolve('/app/admin/catalogue/tags')}
				class="rounded-lg border border-green-300 bg-white px-4 py-2 text-sm font-semibold text-green-700 hover:bg-green-50"
			>
				Manage tags
			</a>
			<a
				href={resolve('/app/admin/catalogue/new')}
				class="rounded-lg bg-green-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-green-700"
			>
				+ New recipe
			</a>
		</div>
	</div>

	<form
		class="mb-4 flex flex-wrap items-center gap-2"
		onsubmit={(e) => {
			e.preventDefault();
			applyFilters();
		}}
	>
		<input
			type="search"
			bind:value={search}
			placeholder="Search by name or description"
			class="flex-1 rounded-lg border border-green-200 bg-white px-3 py-2 text-sm focus:border-green-500 focus:outline-none focus:ring-2 focus:ring-green-200"
		/>
		<select
			bind:value={isPublishedFilter}
			class="rounded-lg border border-green-200 bg-white px-3 py-2 text-sm focus:border-green-500 focus:outline-none focus:ring-2 focus:ring-green-200"
		>
			<option value="all">All</option>
			<option value="published">Published only</option>
			<option value="draft">Drafts only</option>
		</select>
		<button
			type="submit"
			class="rounded-lg bg-charcoal px-4 py-2 text-sm font-semibold text-white hover:bg-charcoal/90"
		>
			Apply
		</button>
	</form>

	{#if actionError}
		<div class="mb-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">
			{actionError}
		</div>
	{/if}

	{#if data.recipes.length === 0}
		<div
			class="rounded-2xl border-2 border-dashed border-green-300/40 bg-green-50/50 px-6 py-16 text-center"
		>
			<p class="text-charcoal/60">No catalogue recipes match your filters.</p>
		</div>
	{:else}
		<div class="overflow-hidden rounded-xl border border-green-100 bg-white shadow-sm">
			<table class="min-w-full divide-y divide-green-100 text-sm">
				<thead class="bg-green-50/60 text-left text-xs uppercase tracking-wide text-charcoal/60">
					<tr>
						<th class="px-4 py-3 font-semibold">Name</th>
						<th class="px-4 py-3 font-semibold">Tags</th>
						<th class="px-4 py-3 font-semibold">Status</th>
						<th class="px-4 py-3 font-semibold text-right">Adds</th>
						<th class="px-4 py-3 font-semibold">Updated</th>
						<th class="px-4 py-3"></th>
					</tr>
				</thead>
				<tbody class="divide-y divide-green-50">
					{#each data.recipes as recipe (recipe.id)}
						<tr>
							<td class="px-4 py-3">
								<a
									href={resolve('/app/admin/catalogue/[id]', { id: recipe.id })}
									class="font-medium text-charcoal hover:text-green-700"
								>
									{recipe.name}
								</a>
							</td>
							<td class="px-4 py-3">
								<div class="flex flex-wrap gap-1">
									{#each recipe.tags as tag (tag.id)}
										<span
											class="inline-flex items-center rounded-full bg-green-50 px-2 py-0.5 text-xs font-medium text-green-700"
										>
											{tag.name}
										</span>
									{/each}
								</div>
							</td>
							<td class="px-4 py-3">
								{#if recipe.isPublished}
									<span
										class="inline-flex items-center rounded-full bg-green-100 px-2 py-0.5 text-xs font-semibold text-green-800"
									>
										Published
									</span>
								{:else}
									<span
										class="inline-flex items-center rounded-full bg-amber-100 px-2 py-0.5 text-xs font-semibold text-amber-800"
									>
										Draft
									</span>
								{/if}
							</td>
							<td class="px-4 py-3 text-right tabular-nums">{recipe.addCount}</td>
							<td class="px-4 py-3 text-charcoal/60">
								{new Date(recipe.updatedAt).toLocaleDateString()}
							</td>
							<td class="px-4 py-3">
								<div class="flex items-center justify-end gap-2 whitespace-nowrap">
									<a
										href={resolve('/app/admin/catalogue/[id]', { id: recipe.id })}
										class="text-xs font-medium text-green-700 hover:underline"
									>
										Edit
									</a>
									<button
										type="button"
										class="text-xs font-medium text-charcoal/70 hover:underline disabled:opacity-50"
										disabled={busyId === recipe.id}
										onclick={() => togglePublished(recipe.id, recipe.isPublished)}
									>
										{recipe.isPublished ? 'Unpublish' : 'Publish'}
									</button>
									<button
										type="button"
										class="text-xs font-medium text-red-600 hover:underline disabled:opacity-50"
										disabled={busyId === recipe.id}
										onclick={() => deleteRecipe(recipe.id, recipe.name)}
									>
										Delete
									</button>
								</div>
							</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
	{/if}
</div>

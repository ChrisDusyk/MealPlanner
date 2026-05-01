<script lang="ts">
	import { invalidateAll } from '$app/navigation';
	import { resolve } from '$app/paths';
	import {
		adminCreateCatalogueTag,
		adminDeleteCatalogueTag,
		adminUpdateCatalogueTag
	} from '$lib/api/adminCatalogueApi';
	import { ApiError } from '$lib/api/apiHelpers';
	import type { CatalogueTag } from '$lib/api/catalogueApi';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	let newName = $state('');
	let newSlug = $state('');
	let newError = $state<string | null>(null);
	let creating = $state(false);

	let editId = $state<string | null>(null);
	let editName = $state('');
	let editSlug = $state('');
	let editError = $state<string | null>(null);
	let savingId = $state<string | null>(null);

	function slugify(value: string): string {
		return value
			.toLowerCase()
			.trim()
			.replace(/[^a-z0-9-]+/g, '-')
			.replace(/^-+|-+$/g, '');
	}

	$effect(() => {
		if (editId === null && newSlug === '' && newName) {
			newSlug = slugify(newName);
		}
	});

	async function createTag() {
		if (!newName.trim() || !newSlug.trim()) {
			newError = 'Name and slug are required.';
			return;
		}
		creating = true;
		newError = null;
		try {
			await adminCreateCatalogueTag(data.session.accessToken, {
				name: newName.trim(),
				slug: newSlug.trim()
			});
			newName = '';
			newSlug = '';
			await invalidateAll();
		} catch (err) {
			newError = err instanceof Error ? err.message : 'Failed to create tag';
		} finally {
			creating = false;
		}
	}

	function startEdit(tag: CatalogueTag) {
		editId = tag.id;
		editName = tag.name;
		editSlug = tag.slug;
		editError = null;
	}

	function cancelEdit() {
		editId = null;
		editError = null;
	}

	async function saveEdit(id: string) {
		savingId = id;
		editError = null;
		try {
			await adminUpdateCatalogueTag(data.session.accessToken, id, {
				name: editName.trim(),
				slug: editSlug.trim()
			});
			editId = null;
			await invalidateAll();
		} catch (err) {
			editError = err instanceof Error ? err.message : 'Failed to update tag';
		} finally {
			savingId = null;
		}
	}

	async function deleteTag(tag: CatalogueTag) {
		if (!confirm(`Delete tag "${tag.name}"?`)) return;
		try {
			await adminDeleteCatalogueTag(data.session.accessToken, tag.id);
			await invalidateAll();
		} catch (err) {
			if (err instanceof ApiError && err.status === 409) {
				alert('Cannot delete: tag is in use by one or more recipes.');
			} else {
				alert(err instanceof Error ? err.message : 'Failed to delete tag');
			}
		}
	}
</script>

<svelte:head>
	<title>Catalogue Tags | Admin</title>
</svelte:head>

<div class="mx-auto max-w-3xl">
	<header class="mb-6">
		<a href={resolve('/app/admin/catalogue')} class="text-sm text-charcoal/60 hover:text-charcoal">
			← Back to catalogue
		</a>
		<h1 class="mt-1 font-display text-2xl font-bold text-charcoal sm:text-3xl">Catalogue tags</h1>
		<p class="mt-1 text-charcoal/60">Manage the tags used to categorise catalogue recipes.</p>
	</header>

	<!-- New tag form -->
	<form
		class="mb-6 rounded-xl border border-green-100 bg-white p-4 shadow-sm"
		onsubmit={(e) => {
			e.preventDefault();
			createTag();
		}}
	>
		<h2 class="mb-3 font-display text-base font-semibold text-charcoal">Add a tag</h2>
		<div class="grid gap-3 sm:grid-cols-3">
			<label class="block">
				<span class="text-xs font-medium text-charcoal/70">Name</span>
				<input
					type="text"
					bind:value={newName}
					class="mt-1 w-full rounded-lg border border-green-200 px-2 py-1.5 text-sm focus:border-green-500 focus:outline-none focus:ring-2 focus:ring-green-200"
				/>
			</label>
			<label class="block">
				<span class="text-xs font-medium text-charcoal/70">Slug</span>
				<input
					type="text"
					bind:value={newSlug}
					placeholder="my-tag"
					class="mt-1 w-full rounded-lg border border-green-200 px-2 py-1.5 text-sm focus:border-green-500 focus:outline-none focus:ring-2 focus:ring-green-200"
				/>
			</label>
			<div class="flex items-end">
				<button
					type="submit"
					disabled={creating}
					class="w-full rounded-lg bg-green-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-green-700 disabled:opacity-50"
				>
					{creating ? 'Adding…' : 'Add tag'}
				</button>
			</div>
		</div>
		{#if newError}
			<div class="mt-2 text-xs text-red-600">{newError}</div>
		{/if}
	</form>

	<!-- Tag list -->
	{#if data.tags.length === 0}
		<div
			class="rounded-2xl border-2 border-dashed border-green-300/40 bg-green-50/50 px-6 py-12 text-center"
		>
			<p class="text-charcoal/60">No tags defined yet.</p>
		</div>
	{:else}
		<ul class="divide-y divide-green-100 overflow-hidden rounded-xl border border-green-100 bg-white shadow-sm">
			{#each data.tags as tag (tag.id)}
				<li class="px-4 py-3">
					{#if editId === tag.id}
						<div class="grid gap-2 sm:grid-cols-3">
							<input
								type="text"
								bind:value={editName}
								class="rounded-lg border border-green-200 px-2 py-1.5 text-sm focus:border-green-500 focus:outline-none focus:ring-2 focus:ring-green-200"
							/>
							<input
								type="text"
								bind:value={editSlug}
								class="rounded-lg border border-green-200 px-2 py-1.5 text-sm focus:border-green-500 focus:outline-none focus:ring-2 focus:ring-green-200"
							/>
							<div class="flex gap-2">
								<button
									type="button"
									class="flex-1 rounded-lg bg-green-600 px-3 py-1.5 text-sm font-semibold text-white hover:bg-green-700 disabled:opacity-50"
									disabled={savingId === tag.id}
									onclick={() => saveEdit(tag.id)}
								>
									{savingId === tag.id ? 'Saving…' : 'Save'}
								</button>
								<button
									type="button"
									class="rounded-lg border border-green-200 px-3 py-1.5 text-sm text-charcoal hover:bg-green-50"
									onclick={cancelEdit}
								>
									Cancel
								</button>
							</div>
						</div>
						{#if editError}
							<div class="mt-2 text-xs text-red-600">{editError}</div>
						{/if}
					{:else}
						<div class="flex items-center justify-between gap-3">
							<div>
								<div class="font-medium text-charcoal">{tag.name}</div>
								<div class="text-xs text-charcoal/50">{tag.slug}</div>
							</div>
							<div class="flex gap-2">
								<button
									type="button"
									class="text-xs font-medium text-green-700 hover:underline"
									onclick={() => startEdit(tag)}
								>
									Edit
								</button>
								<button
									type="button"
									class="text-xs font-medium text-red-600 hover:underline"
									onclick={() => deleteTag(tag)}
								>
									Delete
								</button>
							</div>
						</div>
					{/if}
				</li>
			{/each}
		</ul>
	{/if}
</div>

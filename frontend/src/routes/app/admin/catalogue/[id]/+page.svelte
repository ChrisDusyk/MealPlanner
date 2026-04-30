<script lang="ts">
	import { goto, invalidateAll } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { untrack } from 'svelte';
	import {
		adminDeleteCatalogueRecipe,
		adminSetCatalogueRecipePublished,
		adminUpdateCatalogueRecipe
	} from '$lib/api/adminCatalogueApi';
	import CatalogueRecipeForm from '$lib/components/CatalogueRecipeForm.svelte';
	import type { CatalogueRecipeFormData } from '$lib/components/CatalogueRecipeForm.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	let submitting = $state(false);
	let errorMessage = $state('');
	let publishBusy = $state(false);

	const initialData: CatalogueRecipeFormData = untrack(() => ({
		name: data.recipe.name,
		description: data.recipe.description,
		servings: data.recipe.servings,
		sourceUrl: data.recipe.sourceUrl ?? '',
		imageUrl: data.recipe.imageUrl ?? '',
		ingredients: data.recipe.ingredients,
		tagIds: data.recipe.tags.map((t) => t.id),
		isPublished: data.recipe.isPublished
	}));

	async function handleSubmit(payload: any) {
		submitting = true;
		errorMessage = '';
		try {
			await adminUpdateCatalogueRecipe(data.session.accessToken, data.recipe.id, payload);
			await invalidateAll();
		} catch (err) {
			errorMessage = err instanceof Error ? err.message : 'Failed to save recipe';
		} finally {
			submitting = false;
		}
	}

	async function togglePublished() {
		publishBusy = true;
		try {
			await adminSetCatalogueRecipePublished(
				data.session.accessToken,
				data.recipe.id,
				!data.recipe.isPublished
			);
			await invalidateAll();
		} catch (err) {
			errorMessage = err instanceof Error ? err.message : 'Failed to update';
		} finally {
			publishBusy = false;
		}
	}

	async function deleteRecipe() {
		if (!confirm(`Delete catalogue recipe "${data.recipe.name}"?`)) return;
		try {
			await adminDeleteCatalogueRecipe(data.session.accessToken, data.recipe.id);
			await goto(resolve('/app/admin/catalogue'));
		} catch (err) {
			errorMessage = err instanceof Error ? err.message : 'Failed to delete';
		}
	}
</script>

<svelte:head>
	<title>Edit "{data.recipe.name}" | Catalogue Admin</title>
</svelte:head>

<div class="mx-auto max-w-3xl">
	<header class="mb-6">
		<a href={resolve('/app/admin/catalogue')} class="text-sm text-charcoal/60 hover:text-charcoal">
			← Back to catalogue
		</a>
		<div class="mt-1 flex flex-wrap items-center justify-between gap-3">
			<h1 class="font-display text-2xl font-bold text-charcoal sm:text-3xl">
				Edit catalogue recipe
			</h1>
			<div class="flex items-center gap-2">
				<span
					class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold {data
						.recipe.isPublished
						? 'bg-green-100 text-green-800'
						: 'bg-amber-100 text-amber-800'}"
				>
					{data.recipe.isPublished ? 'Published' : 'Draft'}
				</span>
				<button
					type="button"
					class="rounded-lg border border-green-300 bg-white px-3 py-1.5 text-sm font-semibold text-green-700 hover:bg-green-50 disabled:opacity-50"
					disabled={publishBusy}
					onclick={togglePublished}
				>
					{data.recipe.isPublished ? 'Unpublish' : 'Publish'}
				</button>
				<button
					type="button"
					class="rounded-lg border border-red-200 bg-white px-3 py-1.5 text-sm font-semibold text-red-600 hover:bg-red-50"
					onclick={deleteRecipe}
				>
					Delete
				</button>
			</div>
		</div>
	</header>

	<CatalogueRecipeForm
		mode="edit"
		{initialData}
		availableTags={data.tags}
		accessToken={data.session.accessToken}
		{submitting}
		{errorMessage}
		onsubmit={handleSubmit}
		oncancel={() => goto(resolve('/app/admin/catalogue'))}
	/>
</div>

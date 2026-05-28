<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import type { CatalogueRecipe } from '$lib/api/catalogueApi';
	import type {
		CreateCatalogueRecipeRequest,
		UpdateCatalogueRecipeRequest
	} from '$lib/api/adminCatalogueApi';
	import CatalogueRecipeForm from '$lib/components/CatalogueRecipeForm.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	let submitting = $state(false);
	let errorMessage = $state('');

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

	async function handleSubmit(payload: CreateCatalogueRecipeRequest | UpdateCatalogueRecipeRequest) {
		submitting = true;
		errorMessage = '';
		try {
			const response = await fetch('/app/admin/catalogue', {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify(payload)
			});

			if (!response.ok) {
				throw new Error(await readErrorMessage(response, 'Failed to create recipe'));
			}

			const created = (await response.json()) as CatalogueRecipe;
			await goto(resolve('/app/admin/catalogue/[id]', { id: created.id }));
		} catch (err) {
			errorMessage = err instanceof Error ? err.message : 'Failed to create recipe';
		} finally {
			submitting = false;
		}
	}
</script>

<svelte:head>
	<title>New Catalogue Recipe | Admin</title>
</svelte:head>

<div class="mx-auto max-w-3xl">
	<header class="mb-8">
		<a
			href={resolve('/app/admin/catalogue')}
			class="mb-4 inline-flex items-center gap-1.5 py-2 font-display text-sm font-medium text-green-600 transition-colors hover:text-green-700"
		>
			<svg
				xmlns="http://www.w3.org/2000/svg"
				class="h-4 w-4"
				fill="none"
				viewBox="0 0 24 24"
				stroke="currentColor"
				stroke-width="2"
			>
				<path stroke-linecap="round" stroke-linejoin="round" d="M15 19l-7-7 7-7" />
			</svg>
			Back to Catalogue
		</a>
		<h1 class="font-display text-2xl font-bold text-charcoal sm:text-3xl">New Catalogue Recipe</h1>
		<p class="mt-1 text-charcoal/60">Add a new recipe to the shared catalogue.</p>
	</header>

	<CatalogueRecipeForm
		mode="create"
		availableTags={data.tags}
		{submitting}
		{errorMessage}
		onsubmit={handleSubmit}
		oncancel={() => goto(resolve('/app/admin/catalogue'))}
	/>
</div>

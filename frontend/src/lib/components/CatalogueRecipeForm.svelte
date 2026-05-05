<script lang="ts">
	import type { CatalogueIngredient, CatalogueTag } from '$lib/api/catalogueApi';
	import type {
		CreateCatalogueRecipeRequest,
		UpdateCatalogueRecipeRequest
	} from '$lib/api/adminCatalogueApi';
	import type { ImportIngredientsResponse } from '$lib/api/recipeApi';
	import { untrack } from 'svelte';

	export interface CatalogueRecipeFormData {
		name: string;
		description: string;
		servings: number;
		sourceUrl: string;
		imageUrl: string;
		ingredients: CatalogueIngredient[];
		tagIds: string[];
		isPublished: boolean;
	}

	let {
		initialData,
		availableTags,
		mode,
		submitting = false,
		errorMessage = '',
		onsubmit,
		oncancel
	}: {
		initialData?: CatalogueRecipeFormData;
		availableTags: CatalogueTag[];
		mode: 'create' | 'edit';
		submitting?: boolean;
		errorMessage?: string;
		onsubmit: (
			data: CreateCatalogueRecipeRequest | UpdateCatalogueRecipeRequest,
			isPublished: boolean
		) => void;
		oncancel?: () => void;
	} = $props();

	const _init = untrack(() => initialData);

	let name = $state(_init?.name ?? '');
	let description = $state(_init?.description ?? '');
	let servings = $state(_init?.servings ?? 1);
	let sourceUrl = $state(_init?.sourceUrl ?? '');
	let imageUrl = $state(_init?.imageUrl ?? '');
	let isPublished = $state(_init?.isPublished ?? false);
	let selectedTagIds = $state<string[]>(_init?.tagIds ?? []);
	let ingredients = $state<(CatalogueIngredient & { _id: number })[]>(
		(_init?.ingredients ?? []).map((ing, i) => ({ ...ing, _id: i + 1 }))
	);
	let nextId = $state((_init?.ingredients ?? []).length + 1);

	let importBusy = $state(false);
	let importMessage = $state<{ text: string; type: 'success' | 'error' } | null>(null);
	let validationErrors = $state<Record<string, string>>({});

	function addIngredient() {
		ingredients = [
			...ingredients,
			{ _id: nextId++, name: '', quantity: 0, unit: '', isPantryStaple: false }
		];
	}

	function removeIngredient(id: number) {
		ingredients = ingredients.filter((i) => i._id !== id);
	}

	function toggleTag(id: string) {
		selectedTagIds = selectedTagIds.includes(id)
			? selectedTagIds.filter((t) => t !== id)
			: [...selectedTagIds, id];
	}

	function validate(): boolean {
		const errors: Record<string, string> = {};
		if (!name.trim()) errors.name = 'Name is required.';
		if (servings < 1) errors.servings = 'Servings must be at least 1.';
		if (sourceUrl.trim()) {
			try {
				const u = new URL(sourceUrl);
				if (u.protocol !== 'http:' && u.protocol !== 'https:')
					errors.sourceUrl = 'URL must start with http:// or https://';
			} catch {
				errors.sourceUrl = 'Please enter a valid URL.';
			}
		}
		if (imageUrl.trim()) {
			try {
				const u = new URL(imageUrl);
				if (u.protocol !== 'http:' && u.protocol !== 'https:')
					errors.imageUrl = 'Image URL must start with http:// or https://';
			} catch {
				errors.imageUrl = 'Please enter a valid image URL.';
			}
		}
		for (const ing of ingredients) {
			if (!ing.name.trim() && ing.quantity === 0 && !ing.unit) continue;
			if (!ing.name.trim()) errors[`ing-${ing._id}-name`] = 'Required';
			if (ing.name.trim() && ing.quantity <= 0) errors[`ing-${ing._id}-qty`] = 'Required';
		}
		validationErrors = errors;
		return Object.keys(errors).length === 0;
	}

	function buildPayload() {
		return {
			name: name.trim(),
			description: description.trim(),
			servings,
			sourceUrl: sourceUrl.trim() || null,
			imageUrl: imageUrl.trim() || null,
			ingredients: ingredients
				.filter((i) => i.name.trim())
				.map((i) => ({
					name: i.name.trim(),
					quantity: i.quantity,
					unit: i.unit,
					isPantryStaple: i.isPantryStaple
				})),
			tagIds: selectedTagIds
		};
	}

	function handleSubmit(event: Event) {
		event.preventDefault();
		if (!validate()) return;
		const base = buildPayload();
		if (mode === 'create') {
			onsubmit({ ...base, isPublished } satisfies CreateCatalogueRecipeRequest, isPublished);
		} else {
			onsubmit(base satisfies UpdateCatalogueRecipeRequest, isPublished);
		}
	}

	async function importFromUrl() {
		importMessage = null;
		const url = sourceUrl.trim();
		if (!url) {
			importMessage = { text: 'Enter a source URL first.', type: 'error' };
			return;
		}
		importBusy = true;
		try {
			const response = await fetch('/app/admin/catalogue/import-from-url', {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify({ sourceUrl: url })
			});

			if (!response.ok) {
				const contentType = response.headers.get('content-type') || '';
				if (contentType.includes('application/json')) {
					const body = await response.json().catch(() => ({}));
					const message =
						typeof body?.error === 'string' && body.error
							? body.error
							: 'Failed to import ingredients';
					throw new Error(message);
				}

				throw new Error((await response.text()) || 'Failed to import ingredients');
			}

			const result = (await response.json()) as ImportIngredientsResponse;
			if (result.recipeName && !name.trim()) name = result.recipeName;
			if (typeof result.servings === 'number' && servings === 1) servings = result.servings;
			ingredients = result.ingredients.map((ing, i) => ({ ...ing, _id: nextId + i }));
			nextId += result.ingredients.length;
			importMessage = {
				text:
					`Imported ${result.ingredients.length} ingredient${result.ingredients.length === 1 ? '' : 's'}` +
					(result.warnings.length ? ` (with ${result.warnings.length} warning${result.warnings.length === 1 ? '' : 's'})` : ''),
				type: 'success'
			};
		} catch (err) {
			importMessage = {
				text: err instanceof Error ? err.message : 'Failed to import ingredients',
				type: 'error'
			};
		} finally {
			importBusy = false;
		}
	}

	const units = ['', 'tsp', 'tbsp', 'cup', 'oz', 'lb', 'g', 'kg', 'ml', 'L', 'piece', 'clove', 'can'];
</script>

<form onsubmit={handleSubmit} class="space-y-6">
	{#if errorMessage}
		<div class="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">
			{errorMessage}
		</div>
	{/if}

	<!-- Basics -->
	<div class="grid gap-4 sm:grid-cols-2">
		<label class="block">
			<span class="text-sm font-medium text-charcoal">Name</span>
			<input
				type="text"
				bind:value={name}
				class="mt-1 w-full rounded-lg border border-green-200 px-3 py-2 text-sm focus:border-green-500 focus:outline-none focus:ring-2 focus:ring-green-200"
			/>
			{#if validationErrors.name}
				<span class="mt-1 block text-xs text-red-600">{validationErrors.name}</span>
			{/if}
		</label>
		<label class="block">
			<span class="text-sm font-medium text-charcoal">Servings</span>
			<input
				type="number"
				min="1"
				bind:value={servings}
				class="mt-1 w-full rounded-lg border border-green-200 px-3 py-2 text-sm focus:border-green-500 focus:outline-none focus:ring-2 focus:ring-green-200"
			/>
			{#if validationErrors.servings}
				<span class="mt-1 block text-xs text-red-600">{validationErrors.servings}</span>
			{/if}
		</label>
	</div>

	<label class="block">
		<span class="text-sm font-medium text-charcoal">Description</span>
		<textarea
			bind:value={description}
			rows="3"
			class="mt-1 w-full rounded-lg border border-green-200 px-3 py-2 text-sm focus:border-green-500 focus:outline-none focus:ring-2 focus:ring-green-200"
		></textarea>
	</label>

	<div class="grid gap-4 sm:grid-cols-2">
		<label class="block">
			<span class="text-sm font-medium text-charcoal">Source URL (optional)</span>
			<input
				type="url"
				bind:value={sourceUrl}
				placeholder="https://example.com/recipe"
				class="mt-1 w-full rounded-lg border border-green-200 px-3 py-2 text-sm focus:border-green-500 focus:outline-none focus:ring-2 focus:ring-green-200"
			/>
			{#if validationErrors.sourceUrl}
				<span class="mt-1 block text-xs text-red-600">{validationErrors.sourceUrl}</span>
			{/if}
		</label>
		<label class="block">
			<span class="text-sm font-medium text-charcoal">Image URL (optional)</span>
			<input
				type="url"
				bind:value={imageUrl}
				placeholder="https://example.com/image.jpg"
				class="mt-1 w-full rounded-lg border border-green-200 px-3 py-2 text-sm focus:border-green-500 focus:outline-none focus:ring-2 focus:ring-green-200"
			/>
			{#if validationErrors.imageUrl}
				<span class="mt-1 block text-xs text-red-600">{validationErrors.imageUrl}</span>
			{/if}
		</label>
	</div>

	<!-- Import from URL -->
	<div class="rounded-lg border border-green-100 bg-green-50/50 p-4">
		<div class="flex flex-wrap items-center justify-between gap-3">
			<div>
				<h3 class="text-sm font-semibold text-charcoal">Import from URL</h3>
				<p class="text-xs text-charcoal/60">
					Fetch ingredients and the recipe name from the source URL above.
				</p>
			</div>
			<button
				type="button"
				class="rounded-lg bg-green-600 px-3 py-1.5 text-sm font-semibold text-white hover:bg-green-700 disabled:opacity-50"
				disabled={importBusy || !sourceUrl.trim()}
				onclick={importFromUrl}
			>
				{importBusy ? 'Importing…' : 'Import ingredients'}
			</button>
		</div>
		{#if importMessage}
			<div
				class="mt-2 text-xs {importMessage.type === 'success' ? 'text-green-700' : 'text-red-700'}"
			>
				{importMessage.text}
			</div>
		{/if}
	</div>

	<!-- Tags -->
	<div>
		<span class="block text-sm font-medium text-charcoal">Tags</span>
		{#if availableTags.length === 0}
			<p class="mt-1 text-xs text-charcoal/50">
				No tags defined yet. Create some on the tags page.
			</p>
		{:else}
			<div class="mt-2 flex flex-wrap gap-1.5">
				{#each availableTags as tag (tag.id)}
					{@const active = selectedTagIds.includes(tag.id)}
					<button
						type="button"
						class="rounded-full px-3 py-1 text-xs font-medium transition-colors {active
							? 'bg-green-600 text-white'
							: 'bg-white text-charcoal/70 ring-1 ring-green-200 hover:bg-green-100'}"
						aria-pressed={active}
						onclick={() => toggleTag(tag.id)}
					>
						{tag.name}
					</button>
				{/each}
			</div>
		{/if}
	</div>

	<!-- Ingredients -->
	<div>
		<div class="flex items-center justify-between">
			<span class="text-sm font-medium text-charcoal">Ingredients</span>
			<button
				type="button"
				class="text-xs font-semibold text-green-700 hover:underline"
				onclick={addIngredient}
			>
				+ Add ingredient
			</button>
		</div>
		<div class="mt-2 space-y-2">
			{#each ingredients as ing (ing._id)}
				<div class="grid grid-cols-12 gap-2">
					<input
						type="text"
						placeholder="Ingredient"
						bind:value={ing.name}
						class="col-span-6 rounded-lg border border-green-200 px-2 py-1.5 text-sm focus:border-green-500 focus:outline-none focus:ring-1 focus:ring-green-200"
					/>
					<input
						type="number"
						step="0.01"
						min="0"
						placeholder="Qty"
						bind:value={ing.quantity}
						class="col-span-2 rounded-lg border border-green-200 px-2 py-1.5 text-sm focus:border-green-500 focus:outline-none focus:ring-1 focus:ring-green-200"
					/>
					<select
						bind:value={ing.unit}
						class="col-span-2 rounded-lg border border-green-200 px-2 py-1.5 text-sm focus:border-green-500 focus:outline-none focus:ring-1 focus:ring-green-200"
					>
						{#each units as unit (unit)}
							<option value={unit}>{unit || '—'}</option>
						{/each}
					</select>
					<label class="col-span-1 flex items-center justify-center text-xs">
						<input type="checkbox" bind:checked={ing.isPantryStaple} class="mr-1" />
						Pantry
					</label>
					<button
						type="button"
						class="col-span-1 rounded-lg text-charcoal/50 hover:bg-red-50 hover:text-red-600"
						aria-label="Remove ingredient"
						onclick={() => removeIngredient(ing._id)}
					>
						✕
					</button>
				</div>
				{#if validationErrors[`ing-${ing._id}-name`] || validationErrors[`ing-${ing._id}-qty`]}
					<div class="text-xs text-red-600">
						{validationErrors[`ing-${ing._id}-name`] ?? validationErrors[`ing-${ing._id}-qty`]}
					</div>
				{/if}
			{/each}
		</div>
	</div>

	{#if mode === 'create'}
		<label class="flex items-center gap-2">
			<input type="checkbox" bind:checked={isPublished} />
			<span class="text-sm text-charcoal">Publish immediately</span>
		</label>
	{/if}

	<div class="flex justify-end gap-2">
		{#if oncancel}
			<button
				type="button"
				class="rounded-lg border border-green-200 px-4 py-2 text-sm font-medium text-charcoal hover:bg-green-50"
				onclick={oncancel}
			>
				Cancel
			</button>
		{/if}
		<button
			type="submit"
			disabled={submitting}
			class="rounded-lg bg-green-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-green-700 disabled:opacity-50"
		>
			{submitting ? 'Saving…' : mode === 'create' ? 'Create recipe' : 'Save changes'}
		</button>
	</div>
</form>

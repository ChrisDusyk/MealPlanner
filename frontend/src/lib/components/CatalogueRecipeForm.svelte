<script lang="ts">
	import { resolve } from '$app/paths';
	import type { CatalogueIngredient, CatalogueTag } from '$lib/api/catalogueApi';
	import type {
		CreateCatalogueRecipeRequest,
		UpdateCatalogueRecipeRequest
	} from '$lib/api/adminCatalogueApi';
	import type { ImportIngredientsResponse } from '$lib/api/recipeApi';
	import { flip } from 'svelte/animate';
	import { quintOut } from 'svelte/easing';
	import { fade, fly, slide } from 'svelte/transition';
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
	let importWarnings = $state<string[]>([]);
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
		importWarnings = [];
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
			importWarnings = result.warnings;
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

	function getImagePreviewUrl(url: string): string | null {
		const trimmed = url.trim();
		if (!trimmed) return null;
		try {
			const parsed = new URL(trimmed);
			if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') return null;
			return parsed.toString();
		} catch {
			return null;
		}
	}

	const units = ['', 'tsp', 'tbsp', 'cup', 'oz', 'lb', 'g', 'kg', 'ml', 'L', 'piece', 'clove', 'can'];
	let imagePreviewUrl = $derived(getImagePreviewUrl(imageUrl));
	let validationErrorMessages = $derived(Object.values(validationErrors));
</script>

<form
	onsubmit={handleSubmit}
	aria-describedby={validationErrorMessages.length > 0 ? 'catalogue-recipe-form-errors' : undefined}
	class="space-y-8"
>
	{#if validationErrorMessages.length > 0}
		<div
			id="catalogue-recipe-form-errors"
			class="sr-only"
			role="alert"
			aria-live="assertive"
			aria-atomic="true"
		>
			Please fix the following errors: {validationErrorMessages.join(', ')}
		</div>
	{/if}

	{#if errorMessage}
		<div
			role="alert"
			transition:slide={{ duration: 300 }}
			class="mb-2 flex items-center gap-3 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
		>
			<svg
				xmlns="http://www.w3.org/2000/svg"
				class="h-5 w-5 shrink-0"
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
			<span class="flex-1">{errorMessage}</span>
			<button
				type="button"
				class="ml-auto inline-flex h-5 w-5 items-center justify-center rounded-full text-red-500 hover:bg-red-100 focus:ring-2 focus:ring-red-300 focus:outline-none"
				aria-label="Dismiss error"
				onclick={() => (errorMessage = '')}
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					class="h-3 w-3"
					fill="none"
					viewBox="0 0 24 24"
					stroke="currentColor"
					stroke-width="2"
				>
					<path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
				</svg>
			</button>
		</div>
	{/if}

	<section class="overflow-hidden rounded-xl border border-green-200/50 bg-white shadow-sm">
		<div class="border-b border-green-100/60 bg-green-50/30 px-6 py-4">
			<h2 class="font-display text-lg font-semibold text-charcoal">Recipe Details</h2>
		</div>
		<div class="space-y-5 p-6">
			<div>
				<label for="catalogue-name" class="mb-1.5 block text-sm font-medium text-charcoal/80">
					Recipe Name <span class="text-red-400">*</span>
				</label>
				<input
					id="catalogue-name"
					type="text"
					bind:value={name}
					placeholder="e.g. Weeknight Baked Salmon"
					aria-invalid={!!validationErrors.name}
					aria-describedby={validationErrors.name ? 'catalogue-name-error' : undefined}
					class="w-full rounded-lg border px-4 py-2.5 text-sm text-charcoal transition-colors placeholder:text-charcoal/30 focus:ring-2 focus:ring-green-500/40 focus:outline-none
					{validationErrors.name
						? 'border-red-300 bg-red-50/50 focus:border-red-400'
						: 'border-green-200/60 bg-white focus:border-green-400'}"
				/>
				{#if validationErrors.name}
					<p
						id="catalogue-name-error"
						transition:slide={{ duration: 200 }}
						class="mt-1 text-xs text-red-500"
					>
						{validationErrors.name}
					</p>
				{/if}
			</div>

			<div>
				<label for="catalogue-desc" class="mb-1.5 block text-sm font-medium text-charcoal/80">
					Description
				</label>
				<textarea
					id="catalogue-desc"
					bind:value={description}
					rows="3"
					placeholder="A short description of this catalogue recipe..."
					class="w-full resize-none rounded-lg border border-green-200/60 bg-white px-4 py-2.5 text-sm text-charcoal transition-colors placeholder:text-charcoal/30 focus:border-green-400 focus:ring-2 focus:ring-green-500/40 focus:outline-none"
				></textarea>
			</div>

			<div>
				<label for="catalogue-servings" class="mb-1.5 block text-sm font-medium text-charcoal/80">
					Servings
				</label>
				<input
					id="catalogue-servings"
					type="number"
					min="1"
					step="1"
					bind:value={servings}
					aria-invalid={!!validationErrors.servings}
					aria-describedby={validationErrors.servings ? 'catalogue-servings-error' : undefined}
					class="w-24 rounded-lg border px-4 py-2.5 text-sm text-charcoal transition-colors placeholder:text-charcoal/30 focus:ring-2 focus:ring-green-500/40 focus:outline-none
					{validationErrors.servings
						? 'border-red-300 bg-red-50/50 focus:border-red-400'
						: 'border-green-200/60 bg-white focus:border-green-400'}"
				/>
				{#if validationErrors.servings}
					<p
						id="catalogue-servings-error"
						transition:slide={{ duration: 200 }}
						class="mt-1 text-xs text-red-500"
					>
						{validationErrors.servings}
					</p>
				{/if}
				<p class="mt-1 text-xs text-charcoal/50">How many servings this recipe yields.</p>
			</div>

			<div>
				<label for="catalogue-url" class="mb-1.5 block text-sm font-medium text-charcoal/80">
					Source URL <span class="text-xs text-charcoal/60">(optional)</span>
				</label>
				<input
					id="catalogue-url"
					type="url"
					bind:value={sourceUrl}
					placeholder="https://example.com/recipe"
					aria-invalid={!!validationErrors.sourceUrl}
					aria-describedby={validationErrors.sourceUrl ? 'catalogue-url-error' : undefined}
					class="w-full rounded-lg border px-4 py-2.5 text-sm text-charcoal transition-colors placeholder:text-charcoal/30 focus:ring-2 focus:ring-green-500/40 focus:outline-none
					{validationErrors.sourceUrl
						? 'border-red-300 bg-red-50/50 focus:border-red-400'
						: 'border-green-200/60 bg-white focus:border-green-400'}"
				/>
				{#if validationErrors.sourceUrl}
					<p
						id="catalogue-url-error"
						transition:slide={{ duration: 200 }}
						class="mt-1 text-xs text-red-500"
					>
						{validationErrors.sourceUrl}
					</p>
				{/if}
				<div class="mt-3 flex flex-wrap items-center gap-3">
					<button
						type="button"
						onclick={importFromUrl}
						disabled={importBusy || submitting}
						class="inline-flex items-center gap-2 rounded-lg border border-green-300 bg-green-50 px-3.5 py-2 font-display text-xs font-semibold text-green-700 transition-all hover:bg-green-100 disabled:cursor-not-allowed disabled:opacity-60"
					>
						{#if importBusy}
							<svg class="h-3.5 w-3.5 animate-spin" viewBox="0 0 24 24" fill="none">
								<circle
									cx="12"
									cy="12"
									r="10"
									stroke="currentColor"
									stroke-width="3"
									class="opacity-25"
								/>
								<path
									fill="currentColor"
									class="opacity-75"
									d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
								/>
							</svg>
							Importing...
						{:else}
							Import ingredients from URL
						{/if}
					</button>
					<span class="text-xs text-charcoal/55">Import replaces existing ingredient rows.</span>
				</div>

				{#if importMessage}
					<p
						class="mt-2 rounded-md border px-3 py-2 text-xs {importMessage.type === 'success'
							? 'border-emerald-200 bg-emerald-50 text-emerald-700'
							: 'border-red-200 bg-red-50 text-red-700'}"
					>
						{importMessage.text}
					</p>
				{/if}

				{#if importWarnings.length > 0}
					<div
						class="mt-2 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-xs text-amber-800"
					>
						<p class="font-semibold">Import warnings</p>
						<ul class="mt-1 list-disc space-y-1 pl-5">
							{#each importWarnings as warning, index (`${warning}-${index}`)}
								<li>{warning}</li>
							{/each}
						</ul>
					</div>
				{/if}
			</div>

			<div>
				<label for="catalogue-image-url" class="mb-1.5 block text-sm font-medium text-charcoal/80">
					Image URL <span class="text-xs text-charcoal/60">(optional)</span>
				</label>
				<input
					id="catalogue-image-url"
					type="url"
					bind:value={imageUrl}
					placeholder="https://example.com/image.jpg"
					aria-invalid={!!validationErrors.imageUrl}
					aria-describedby={validationErrors.imageUrl ? 'catalogue-image-error' : undefined}
					class="w-full rounded-lg border px-4 py-2.5 text-sm text-charcoal transition-colors placeholder:text-charcoal/30 focus:ring-2 focus:ring-green-500/40 focus:outline-none
					{validationErrors.imageUrl
						? 'border-red-300 bg-red-50/50 focus:border-red-400'
						: 'border-green-200/60 bg-white focus:border-green-400'}"
				/>
				{#if validationErrors.imageUrl}
					<p
						id="catalogue-image-error"
						transition:slide={{ duration: 200 }}
						class="mt-1 text-xs text-red-500"
					>
						{validationErrors.imageUrl}
					</p>
				{/if}

				{#if imagePreviewUrl}
					<div class="mt-3 overflow-hidden rounded-lg border border-green-100 bg-green-50/40 p-2">
						<img
							src={imagePreviewUrl}
							alt="Recipe preview"
							loading="lazy"
							class="h-32 w-full rounded-md object-cover"
						/>
					</div>
				{/if}
			</div>
		</div>
	</section>

	<section class="overflow-hidden rounded-xl border border-green-200/50 bg-white shadow-sm">
		<div class="border-b border-green-100/60 bg-green-50/30 px-6 py-4">
			<h2 class="font-display text-lg font-semibold text-charcoal">Tags</h2>
			<p class="mt-0.5 text-xs text-charcoal/50">Choose tags to help users find this recipe.</p>
		</div>
		<div class="p-6">
			{#if availableTags.length === 0}
				<p class="text-sm text-charcoal/50">No tags defined yet. Create some on the tags page.</p>
			{:else}
				<div class="flex flex-wrap gap-1.5">
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
	</section>

	<section class="overflow-hidden rounded-xl border border-green-200/50 bg-white shadow-sm">
		<div
			class="flex items-center justify-between border-b border-green-100/60 bg-green-50/30 px-6 py-4"
		>
			<div>
				<h2 class="font-display text-lg font-semibold text-charcoal">Ingredients</h2>
				{#if ingredients.length > 0}
					<p transition:fade={{ duration: 200 }} class="mt-0.5 text-xs text-charcoal/60">
						{ingredients.length} ingredient{ingredients.length !== 1 ? 's' : ''} added
					</p>
				{/if}
			</div>
			<button
				type="button"
				onclick={addIngredient}
				class="flex items-center gap-1.5 rounded-lg bg-green-600 px-3.5 py-2 font-display text-xs font-semibold text-white shadow-sm transition-all hover:bg-green-700 hover:shadow-md active:scale-[0.97]"
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					class="h-4 w-4"
					fill="none"
					viewBox="0 0 24 24"
					stroke="currentColor"
					stroke-width="2.5"
				>
					<path stroke-linecap="round" stroke-linejoin="round" d="M12 4v16m8-8H4" />
				</svg>
				Add
			</button>
		</div>

		<div class="p-6">
			{#if ingredients.length === 0}
				<div
					transition:fade={{ duration: 200 }}
					class="flex flex-col items-center justify-center rounded-xl border-2 border-dashed border-green-200/40 py-12"
				>
					<div
						class="mb-3 flex h-12 w-12 items-center justify-center rounded-xl bg-green-100 text-green-500"
					>
						<svg
							xmlns="http://www.w3.org/2000/svg"
							class="h-6 w-6"
							fill="none"
							viewBox="0 0 24 24"
							stroke="currentColor"
							stroke-width="1.5"
						>
							<path
								stroke-linecap="round"
								stroke-linejoin="round"
								d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"
							/>
						</svg>
					</div>
					<p class="font-display text-sm font-medium text-charcoal/50">No ingredients yet</p>
					<p class="mt-0.5 text-xs text-charcoal/50">
						Click "Add" above to start building your ingredient list
					</p>
				</div>
			{:else}
				<div
					class="mb-3 hidden grid-cols-[1fr_5rem_7rem_5rem_2.5rem] gap-3 px-1 text-xs font-medium tracking-wider text-charcoal/60 uppercase sm:grid"
				>
					<span>Ingredient</span>
					<span>Qty</span>
					<span>Unit</span>
					<span>Staple</span>
					<span></span>
				</div>

				<div class="space-y-2">
					{#each ingredients as ing (ing._id)}
						<div
							class="group flex flex-col gap-2 rounded-lg border border-green-100/60 bg-green-50/20 p-3 transition-all hover:border-green-200/80 hover:bg-green-50/50 sm:grid sm:grid-cols-[1fr_5rem_7rem_5rem_2.5rem] sm:items-start sm:gap-3"
							animate:flip={{ duration: 300, easing: quintOut }}
							in:fly={{ y: -10, duration: 300, easing: quintOut }}
							out:slide={{ duration: 250, easing: quintOut }}
						>
							<div>
								<span class="mb-1 block text-xs font-medium text-charcoal/60 sm:hidden"
									>Ingredient</span
								>
								<input
									id="catalogue-ingredient-{ing._id}-name"
									type="text"
									bind:value={ing.name}
									placeholder="e.g. Chicken breast"
									aria-label="Ingredient name"
									aria-invalid={!!validationErrors[`ing-${ing._id}-name`]}
									aria-describedby={validationErrors[`ing-${ing._id}-name`]
										? `catalogue-ingredient-${ing._id}-name-error`
										: undefined}
									class="w-full rounded-md border border-green-200/50 bg-white px-3 py-2 text-sm text-charcoal transition-colors placeholder:text-charcoal/25 focus:border-green-400 focus:ring-2 focus:ring-green-500/30 focus:outline-none
									{validationErrors[`ing-${ing._id}-name`] ? 'border-red-300 bg-red-50/50' : ''}"
								/>
								{#if validationErrors[`ing-${ing._id}-name`]}
									<p
										id="catalogue-ingredient-{ing._id}-name-error"
										transition:slide={{ duration: 150 }}
										class="mt-0.5 text-[11px] text-red-500"
									>
										{validationErrors[`ing-${ing._id}-name`]}
									</p>
								{/if}
							</div>

							<div class="flex items-start gap-2 sm:contents">
								<div class="w-20 sm:w-auto">
									<span class="mb-1 block text-xs font-medium text-charcoal/60 sm:hidden"
										>Qty</span
									>
									<input
										id="catalogue-ingredient-{ing._id}-qty"
										type="number"
										bind:value={ing.quantity}
										min="0"
										max="9999"
										step="0.25"
										placeholder="0"
										aria-label="Quantity"
										aria-invalid={!!validationErrors[`ing-${ing._id}-qty`]}
										aria-describedby={validationErrors[`ing-${ing._id}-qty`]
											? `catalogue-ingredient-${ing._id}-qty-error`
											: undefined}
										class="w-full rounded-md border border-green-200/50 bg-white px-3 py-2 text-sm text-charcoal transition-colors placeholder:text-charcoal/25 focus:border-green-400 focus:ring-2 focus:ring-green-500/30 focus:outline-none
										{validationErrors[`ing-${ing._id}-qty`] ? 'border-red-300 bg-red-50/50' : ''}"
									/>
									{#if validationErrors[`ing-${ing._id}-qty`]}
										<p
											id="catalogue-ingredient-{ing._id}-qty-error"
											transition:slide={{ duration: 150 }}
											class="mt-0.5 text-[11px] text-red-500"
										>
											{validationErrors[`ing-${ing._id}-qty`]}
										</p>
									{/if}
								</div>

								<div class="flex-1 sm:flex-initial">
									<span class="mb-1 block text-xs font-medium text-charcoal/60 sm:hidden">Unit</span
									>
									<select
										bind:value={ing.unit}
										aria-label="Unit"
										class="w-full appearance-none rounded-md border border-green-200/50 bg-white px-3 py-2 text-sm text-charcoal transition-colors focus:border-green-400 focus:ring-2 focus:ring-green-500/30 focus:outline-none"
									>
										{#each units as unit (unit)}
											<option value={unit}>{unit || '—'}</option>
										{/each}
									</select>
								</div>

								<div class="flex flex-col items-center sm:flex-initial">
									<span class="mb-1 block text-xs font-medium text-charcoal/60 sm:hidden"
										>Staple</span
									>
									<label
										class="relative mt-0.5 inline-flex cursor-pointer items-center"
										aria-label="Mark as pantry staple"
										title="Pantry staple — items you typically have on hand"
									>
										<input type="checkbox" bind:checked={ing.isPantryStaple} class="peer sr-only" />
										<div
											class="h-5 w-9 rounded-full bg-charcoal/15 transition-colors peer-checked:bg-amber-500 peer-focus-visible:ring-2 peer-focus-visible:ring-green-500/30 after:absolute after:top-[2px] after:left-[2px] after:h-4 after:w-4 after:rounded-full after:bg-white after:shadow-sm after:transition-transform peer-checked:after:translate-x-full"
										></div>
									</label>
								</div>

								<button
									type="button"
									onclick={() => removeIngredient(ing._id)}
									class="mt-5 flex h-9 w-9 items-center justify-center rounded-md text-charcoal/50 transition-all hover:bg-red-50 hover:text-red-500 active:scale-90 sm:mt-0.5"
									aria-label="Remove ingredient"
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
											d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"
										/>
									</svg>
								</button>
							</div>
						</div>
					{/each}
				</div>

				<button
					type="button"
					onclick={addIngredient}
					class="mt-4 flex w-full items-center justify-center gap-2 rounded-lg border-2 border-dashed border-green-200/50 py-3 font-display text-sm font-medium text-green-600/70 transition-all hover:border-green-300 hover:bg-green-50/50 hover:text-green-700 active:scale-[0.99]"
				>
					<svg
						xmlns="http://www.w3.org/2000/svg"
						class="h-4 w-4"
						fill="none"
						viewBox="0 0 24 24"
						stroke="currentColor"
						stroke-width="2"
					>
						<path stroke-linecap="round" stroke-linejoin="round" d="M12 4v16m8-8H4" />
					</svg>
					Add another ingredient
				</button>
			{/if}
		</div>
	</section>

	{#if mode === 'create'}
		<div class="rounded-xl border border-green-200/50 bg-white p-4 shadow-sm">
			<label class="flex items-center gap-2">
				<input
					type="checkbox"
					bind:checked={isPublished}
					class="h-4 w-4 rounded border-green-300 text-green-600 focus:ring-2 focus:ring-green-200"
				/>
				<span class="text-sm text-charcoal">Publish immediately</span>
			</label>
		</div>
	{/if}

	<div class="flex items-center justify-end gap-3 pb-4">
		{#if oncancel}
			<a
				href={resolve('/app/admin/catalogue')}
				onclick={(event) => {
					event.preventDefault();
					oncancel();
				}}
				class="rounded-lg border border-green-200/60 px-6 py-2.5 font-display text-sm font-medium text-charcoal/70 transition-all hover:border-green-300 hover:bg-green-50"
			>
				Cancel
			</a>
		{/if}
		<button
			type="submit"
			disabled={submitting}
			class="flex items-center gap-2 rounded-lg bg-green-600 px-6 py-2.5 font-display text-sm font-semibold text-white shadow-md shadow-green-900/20 transition-all hover:bg-green-700 hover:shadow-lg disabled:cursor-not-allowed disabled:opacity-60"
		>
			{#if submitting}
				<svg class="h-4 w-4 animate-spin" viewBox="0 0 24 24" fill="none">
					<circle
						cx="12"
						cy="12"
						r="10"
						stroke="currentColor"
						stroke-width="3"
						class="opacity-25"
					/>
					<path
						fill="currentColor"
						class="opacity-75"
						d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
					/>
				</svg>
				Saving...
			{:else}
				{mode === 'create' ? 'Create recipe' : 'Save changes'}
			{/if}
		</button>
	</div>
</form>

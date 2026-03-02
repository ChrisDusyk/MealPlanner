<script lang="ts">
	import { slide, fly, fade } from 'svelte/transition';
	import { flip } from 'svelte/animate';
	import { quintOut } from 'svelte/easing';
	import { untrack } from 'svelte';
	import type { Ingredient, CreateRecipeRequest } from '$lib/api/recipeApi';

	interface RecipeFormData {
		name: string;
		description: string;
		servings: number;
		sourceUrl: string;
		ingredients: Ingredient[];
	}

	let {
		initialData = undefined,
		submitLabel = 'Save Recipe',
		submitting = false,
		errorMessage = '',
		onsubmit: onSubmitCallback
	}: {
		initialData?: RecipeFormData;
		submitLabel?: string;
		submitting?: boolean;
		errorMessage?: string;
		onsubmit: (data: CreateRecipeRequest) => void;
	} = $props();

	// Snapshot initial values once — form state is owned by this component after mount
	const _init = untrack(() => initialData);

	// Form state — initialized from snapshot
	let name = $state(_init?.name ?? '');
	let description = $state(_init?.description ?? '');
	let servings = $state(_init?.servings ?? 1);
	let sourceUrl = $state(_init?.sourceUrl ?? '');
	let ingredients: (Ingredient & { _id: number })[] = $state(
		(_init?.ingredients ?? []).map((ing, i) => ({ ...ing, _id: i + 1 }))
	);
	let nextId = $state((_init?.ingredients ?? []).length + 1);

	// Validation state
	let validationErrors: Record<string, string> = $state({});
	let importingIngredients = $state(false);
	let importErrorMessage = $state('');
	let importSuccessMessage = $state('');
	let importWarnings: string[] = $state([]);

	// Common units for the select dropdown
	const units = [
		'',
		'tsp',
		'tbsp',
		'cup',
		'oz',
		'fl oz',
		'lb',
		'g',
		'kg',
		'ml',
		'L',
		'pinch',
		'dash',
		'piece',
		'slice',
		'clove',
		'can',
		'bottle',
		'package',
		'bunch',
		'sprig',
		'whole'
	];

	function addIngredient() {
		ingredients = [...ingredients, { _id: nextId++, name: '', quantity: 0, unit: '', isPantryStaple: false }];
	}

	function removeIngredient(id: number) {
		ingredients = ingredients.filter((i) => i._id !== id);
	}

	function validate(): boolean {
		const errors: Record<string, string> = {};

		if (!name.trim()) {
			errors.name = 'Recipe name is required.';
		}
		if (sourceUrl.trim()) {
			try {
				const url = new URL(sourceUrl);
				if (url.protocol !== 'http:' && url.protocol !== 'https:') {
					errors.sourceUrl = 'URL must start with http:// or https://';
				}
			} catch {
				errors.sourceUrl = 'Please enter a valid URL.';
			}
		}

		// Validate ingredients that have any data filled in
		for (let i = 0; i < ingredients.length; i++) {
			const ing = ingredients[i];
			if (!ing.name.trim() && ing.quantity === 0 && !ing.unit) continue;
			if (!ing.name.trim()) {
				errors[`ingredient-${ing._id}-name`] = 'Name required';
			}
			if (ing.name.trim() && ing.quantity <= 0) {
				errors[`ingredient-${ing._id}-qty`] = 'Quantity required';
			}
		}

		validationErrors = errors;
		return Object.keys(errors).length === 0;
	}

	function focusFirstInvalidField() {
		const fieldOrder: Array<{ id: string; errorKey: string }> = [
			{ id: 'recipe-name', errorKey: 'name' },
			{ id: 'recipe-url', errorKey: 'sourceUrl' },
			...ingredients.flatMap((ingredient) => [
				{ id: `ingredient-${ingredient._id}-name`, errorKey: `ingredient-${ingredient._id}-name` },
				{ id: `ingredient-${ingredient._id}-qty`, errorKey: `ingredient-${ingredient._id}-qty` }
			])
		];

		for (const { id, errorKey } of fieldOrder) {
			const element = document.getElementById(id);
			if (element instanceof HTMLElement && validationErrors[errorKey] !== undefined) {
				element.focus();
				return;
			}
		}
	}

	function handleSubmit() {
		if (!validate()) {
			focusFirstInvalidField();
			return;
		}

		const request: CreateRecipeRequest = {
			name: name.trim(),
			description: description.trim(),
			servings,
			sourceUrl: sourceUrl.trim() || undefined,
			ingredients: ingredients
				.filter((i) => i.name.trim())
				.map((i) => ({
					name: i.name.trim(),
					quantity: i.quantity,
					unit: i.unit,
					isPantryStaple: i.isPantryStaple
				}))
		};

		onSubmitCallback(request);
	}

	function clearImportFeedback() {
		importErrorMessage = '';
		importSuccessMessage = '';
		importWarnings = [];
	}

	function validateSourceUrlForImport(): boolean {
		const trimmed = sourceUrl.trim();
		if (!trimmed) {
			validationErrors = {
				...validationErrors,
				sourceUrl: 'Enter a source URL before importing ingredients.'
			};
			return false;
		}

		try {
			const url = new URL(trimmed);
			if (url.protocol !== 'http:' && url.protocol !== 'https:') {
				validationErrors = {
					...validationErrors,
					sourceUrl: 'URL must start with http:// or https://'
				};
				return false;
			}
		} catch {
			validationErrors = {
				...validationErrors,
				sourceUrl: 'Please enter a valid URL.'
			};
			return false;
		}

		if (validationErrors.sourceUrl) {
			const { sourceUrl: _ignored, ...remaining } = validationErrors;
			validationErrors = remaining;
		}

		return true;
	}

	function replaceIngredientRows(imported: Ingredient[]) {
		ingredients = imported.map((ingredient, index) => ({
			_id: index + 1,
			name: ingredient.name,
			quantity: ingredient.quantity,
			unit: ingredient.unit,
			isPantryStaple: ingredient.isPantryStaple ?? false
		}));
		nextId = ingredients.length + 1;
	}

	async function importFromSourceUrl() {
		if (importingIngredients || submitting) return;

		clearImportFeedback();

		if (!validateSourceUrlForImport()) {
			focusFirstInvalidField();
			return;
		}

		importingIngredients = true;
		try {
			const response = await fetch('/app/recipes/import-ingredients', {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify({ sourceUrl: sourceUrl.trim() })
			});

			const payload: {
				ingredients?: unknown;
				warnings?: unknown;
				recipeName?: unknown;
				servings?: unknown;
				error?: unknown;
			} | null = await response.json().catch(() => null);

			if (!response.ok) {
				importErrorMessage =
					typeof payload?.error === 'string'
						? payload.error
						: 'Failed to import ingredients from the provided URL.';
				return;
			}

			const rawIngredients = Array.isArray(payload?.ingredients)
				? (payload.ingredients as unknown[])
				: [];

			const importedIngredients: Ingredient[] = rawIngredients
				.filter(
					(item): item is { name: string; quantity: number; unit: string; isPantryStaple?: boolean } =>
						typeof item === 'object' &&
						item !== null &&
						typeof (item as { name?: unknown }).name === 'string' &&
						typeof (item as { quantity?: unknown }).quantity === 'number' &&
						typeof (item as { unit?: unknown }).unit === 'string'
				)
				.map((item) => ({
					name: item.name.trim(),
					quantity: Number.isFinite(item.quantity) ? item.quantity : 0,
					unit: item.unit.trim(),
					isPantryStaple: item.isPantryStaple === true
				}))
				.filter((item) => item.name.length > 0);

			const rawWarnings = Array.isArray(payload?.warnings)
				? (payload.warnings as unknown[])
				: [];
			importWarnings = rawWarnings.filter((warning): warning is string => typeof warning === 'string');

			replaceIngredientRows(importedIngredients);

			// Auto-fill recipe name and servings when form fields are empty/default
			const importedParts: string[] = [];

			if (
				typeof payload?.recipeName === 'string' &&
				payload.recipeName.trim().length > 0 &&
				name.trim().length === 0
			) {
				name = payload.recipeName.trim();
				importedParts.push('recipe name');
			}

			if (
				typeof payload?.servings === 'number' &&
				Number.isFinite(payload.servings) &&
				payload.servings > 0 &&
				servings === 1
			) {
				servings = Math.round(payload.servings);
				importedParts.push('servings');
			}

			if (importedIngredients.length) {
				const ingredientText = `${importedIngredients.length} ingredient${importedIngredients.length === 1 ? '' : 's'}`;
				const importedDescriptions = [ingredientText, ...importedParts];
				let importedDescriptionText: string;

				if (importedDescriptions.length === 1) {
					importedDescriptionText = importedDescriptions[0];
				} else if (importedDescriptions.length === 2) {
					importedDescriptionText = `${importedDescriptions[0]} and ${importedDescriptions[1]}`;
				} else {
					importedDescriptionText = `${importedDescriptions.slice(0, -1).join(', ')}, and ${importedDescriptions[importedDescriptions.length - 1]}`;
				}

				importSuccessMessage = `Imported ${importedDescriptionText} and replaced current rows.`;
			} else {
				importSuccessMessage = 'No ingredients were detected on that page.';
			}
		} catch {
			importErrorMessage = 'An unexpected error occurred while importing ingredients.';
		} finally {
			importingIngredients = false;
		}
	}

	let validationErrorMessages = $derived(Object.values(validationErrors));
</script>

<!-- Error banner -->
{#if errorMessage}
	<div
		role="alert"
		transition:slide={{ duration: 300 }}
		class="mb-6 flex items-center gap-3 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
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

<!-- Form -->
<form
	onsubmit={(e) => {
		e.preventDefault();
		handleSubmit();
	}}
	aria-describedby={validationErrorMessages.length > 0 ? 'recipe-form-errors' : undefined}
	class="space-y-8"
>
	{#if validationErrorMessages.length > 0}
		<div
			id="recipe-form-errors"
			class="sr-only"
			role="alert"
			aria-live="assertive"
			aria-atomic="true"
		>
			Please fix the following errors: {validationErrorMessages.join(', ')}
		</div>
	{/if}

	<!-- Recipe Details Section -->
	<section class="overflow-hidden rounded-xl border border-green-200/50 bg-white shadow-sm">
		<div class="border-b border-green-100/60 bg-green-50/30 px-6 py-4">
			<h2 class="font-display text-lg font-semibold text-charcoal">Recipe Details</h2>
		</div>
		<div class="space-y-5 p-6">
			<!-- Name -->
			<div>
				<label for="recipe-name" class="mb-1.5 block text-sm font-medium text-charcoal/80">
					Recipe Name <span class="text-red-400">*</span>
				</label>
				<input
					id="recipe-name"
					type="text"
					bind:value={name}
					placeholder="e.g. Grandma's Chicken Soup"
					aria-invalid={!!validationErrors.name}
					aria-describedby={validationErrors.name ? 'recipe-name-error' : undefined}
					class="w-full rounded-lg border px-4 py-2.5 text-sm text-charcoal transition-colors placeholder:text-charcoal/30 focus:ring-2 focus:ring-green-500/40 focus:outline-none
					{validationErrors.name
						? 'border-red-300 bg-red-50/50 focus:border-red-400'
						: 'border-green-200/60 bg-white focus:border-green-400'}"
				/>
				{#if validationErrors.name}
					<p
						id="recipe-name-error"
						transition:slide={{ duration: 200 }}
						class="mt-1 text-xs text-red-500"
					>
						{validationErrors.name}
					</p>
				{/if}
			</div>

			<!-- Description -->
			<div>
				<label for="recipe-desc" class="mb-1.5 block text-sm font-medium text-charcoal/80">
					Description
				</label>
				<textarea
					id="recipe-desc"
					bind:value={description}
					placeholder="A short description of this recipe..."
					rows="3"
					class="w-full resize-none rounded-lg border border-green-200/60 bg-white px-4 py-2.5 text-sm text-charcoal transition-colors placeholder:text-charcoal/30 focus:border-green-400 focus:ring-2 focus:ring-green-500/40 focus:outline-none"
				></textarea>
			</div>

			<!-- Servings -->
			<div>
				<label for="recipe-servings" class="mb-1.5 block text-sm font-medium text-charcoal/80">
					Servings
				</label>
				<input
					id="recipe-servings"
					type="number"
					min="1"
					step="1"
					bind:value={servings}
					class="w-24 rounded-lg border border-green-200/60 bg-white px-4 py-2.5 text-sm text-charcoal transition-colors placeholder:text-charcoal/30 focus:border-green-400 focus:ring-2 focus:ring-green-500/40 focus:outline-none"
				/>
				<p class="mt-1 text-xs text-charcoal/50">How many servings this recipe yields.</p>
			</div>

			<!-- Source URL -->
			<div>
				<label for="recipe-url" class="mb-1.5 block text-sm font-medium text-charcoal/80">
					Source URL <span class="text-xs text-charcoal/60">(optional)</span>
				</label>
				<input
					id="recipe-url"
					type="url"
					bind:value={sourceUrl}
					placeholder="https://example.com/recipe"
					aria-invalid={!!validationErrors.sourceUrl}
					aria-describedby={validationErrors.sourceUrl ? 'recipe-url-error' : undefined}
					class="w-full rounded-lg border px-4 py-2.5 text-sm text-charcoal transition-colors placeholder:text-charcoal/30 focus:ring-2 focus:ring-green-500/40 focus:outline-none
					{validationErrors.sourceUrl
						? 'border-red-300 bg-red-50/50 focus:border-red-400'
						: 'border-green-200/60 bg-white focus:border-green-400'}"
				/>
				{#if validationErrors.sourceUrl}
					<p
						id="recipe-url-error"
						transition:slide={{ duration: 200 }}
						class="mt-1 text-xs text-red-500"
					>
						{validationErrors.sourceUrl}
					</p>
				{/if}
				<div class="mt-3 flex flex-wrap items-center gap-3">
					<button
						type="button"
						onclick={importFromSourceUrl}
						disabled={importingIngredients || submitting}
						class="inline-flex items-center gap-2 rounded-lg border border-green-300 bg-green-50 px-3.5 py-2 font-display text-xs font-semibold text-green-700 transition-all hover:bg-green-100 disabled:cursor-not-allowed disabled:opacity-60"
					>
						{#if importingIngredients}
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

				{#if importErrorMessage}
					<p class="mt-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
						{importErrorMessage}
					</p>
				{/if}

				{#if importSuccessMessage}
					<p
						class="mt-2 rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs text-emerald-700"
					>
						{importSuccessMessage}
					</p>
				{/if}
			</div>
		</div>
	</section>

	<!-- Ingredients Section -->
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
			{#if importWarnings.length > 0}
				<div class="mb-4 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-xs text-amber-800">
					<p class="font-semibold">Import warnings</p>
					<ul class="mt-1 list-disc space-y-1 pl-5">
						{#each importWarnings as warning}
							<li>{warning}</li>
						{/each}
					</ul>
				</div>
			{/if}

			{#if ingredients.length === 0}
				<!-- Empty state -->
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
				<!-- Column headers (hidden on mobile) -->
				<div
					class="mb-3 hidden grid-cols-[1fr_5rem_7rem_5rem_2.5rem] gap-3 px-1 text-xs font-medium tracking-wider text-charcoal/60 uppercase sm:grid"
				>
					<span>Ingredient</span>
					<span>Qty</span>
					<span>Unit</span>
					<span>Staple</span>
					<span></span>
				</div>

				<!-- Ingredient rows -->
				<div class="space-y-2">
					{#each ingredients as ingredient (ingredient._id)}
						<div
							class="group flex flex-col gap-2 rounded-lg border border-green-100/60 bg-green-50/20 p-3 transition-all hover:border-green-200/80 hover:bg-green-50/50 sm:grid sm:grid-cols-[1fr_5rem_7rem_5rem_2.5rem] sm:items-start sm:gap-3"
							animate:flip={{ duration: 300, easing: quintOut }}
							in:fly={{ y: -10, duration: 300, easing: quintOut }}
							out:slide={{ duration: 250, easing: quintOut }}
						>
							<!-- Name -->
							<div>
								<span class="mb-1 block text-xs font-medium text-charcoal/60 sm:hidden"
									>Ingredient</span
								>
								<input
									id="ingredient-{ingredient._id}-name"
									type="text"
									bind:value={ingredient.name}
									placeholder="e.g. Chicken breast"
									aria-label="Ingredient name"
									aria-invalid={!!validationErrors[`ingredient-${ingredient._id}-name`]}
									aria-describedby={validationErrors[`ingredient-${ingredient._id}-name`]
										? `ingredient-${ingredient._id}-name-error`
										: undefined}
									class="w-full rounded-md border border-green-200/50 bg-white px-3 py-2 text-sm text-charcoal transition-colors placeholder:text-charcoal/25 focus:border-green-400 focus:ring-2 focus:ring-green-500/30 focus:outline-none
									{validationErrors[`ingredient-${ingredient._id}-name`] ? 'border-red-300 bg-red-50/50' : ''}"
								/>
								{#if validationErrors[`ingredient-${ingredient._id}-name`]}
									<p
										id="ingredient-{ingredient._id}-name-error"
										transition:slide={{ duration: 150 }}
										class="mt-0.5 text-[11px] text-red-500"
									>
										{validationErrors[`ingredient-${ingredient._id}-name`]}
									</p>
								{/if}
							</div>

							<!-- Qty, Unit, Staple & Remove: row on mobile, grid children on desktop -->
							<div class="flex items-start gap-2 sm:contents">
								<!-- Quantity -->
								<div class="w-20 sm:w-auto">
									<span class="mb-1 block text-xs font-medium text-charcoal/60 sm:hidden">Qty</span>
									<input
										id="ingredient-{ingredient._id}-qty"
										type="number"
										bind:value={ingredient.quantity}
										min="0"
										max="9999"
										step="0.25"
										placeholder="0"
										aria-label="Quantity"
										aria-invalid={!!validationErrors[`ingredient-${ingredient._id}-qty`]}
										aria-describedby={validationErrors[`ingredient-${ingredient._id}-qty`]
											? `ingredient-${ingredient._id}-qty-error`
											: undefined}
										class="w-full rounded-md border border-green-200/50 bg-white px-3 py-2 text-sm text-charcoal transition-colors placeholder:text-charcoal/25 focus:border-green-400 focus:ring-2 focus:ring-green-500/30 focus:outline-none
										{validationErrors[`ingredient-${ingredient._id}-qty`] ? 'border-red-300 bg-red-50/50' : ''}"
									/>
									{#if validationErrors[`ingredient-${ingredient._id}-qty`]}
										<p
											id="ingredient-{ingredient._id}-qty-error"
											transition:slide={{ duration: 150 }}
											class="mt-0.5 text-[11px] text-red-500"
										>
											{validationErrors[`ingredient-${ingredient._id}-qty`]}
										</p>
									{/if}
								</div>

								<!-- Unit -->
								<div class="flex-1 sm:flex-initial">
									<span class="mb-1 block text-xs font-medium text-charcoal/60 sm:hidden">Unit</span
									>
									<select
										bind:value={ingredient.unit}
										aria-label="Unit"
										class="w-full appearance-none rounded-md border border-green-200/50 bg-white px-3 py-2 text-sm text-charcoal transition-colors focus:border-green-400 focus:ring-2 focus:ring-green-500/30 focus:outline-none"
									>
										{#each units as u}
											<option value={u}>{u || '—'}</option>
										{/each}
									</select>
								</div>

								<!-- Pantry staple toggle -->
								<div class="flex flex-col items-center sm:flex-initial">
									<span class="mb-1 block text-xs font-medium text-charcoal/60 sm:hidden">Staple</span>
									<label
										class="relative mt-0.5 inline-flex cursor-pointer items-center"
										aria-label="Mark as pantry staple"
										title="Pantry staple — items you typically have on hand"
									>
										<input
											type="checkbox"
											bind:checked={ingredient.isPantryStaple}
											class="peer sr-only"
										/>
										<div
											class="h-5 w-9 rounded-full bg-charcoal/15 transition-colors after:absolute after:top-[2px] after:left-[2px] after:h-4 after:w-4 after:rounded-full after:bg-white after:shadow-sm after:transition-transform peer-checked:bg-amber-500 peer-checked:after:translate-x-full peer-focus-visible:ring-2 peer-focus-visible:ring-green-500/30"
										></div>
									</label>
								</div>

								<!-- Remove button -->
								<button
									type="button"
									onclick={() => removeIngredient(ingredient._id)}
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

				<!-- Add another button at bottom -->
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

	<!-- Submit area -->
	<div class="flex items-center justify-end gap-3 pb-8">
		<a
			href="/app/recipes"
			class="rounded-lg border border-green-200/60 px-6 py-2.5 font-display text-sm font-medium text-charcoal/70 transition-all hover:border-green-300 hover:bg-green-50"
		>
			Cancel
		</a>
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
				{submitLabel}
			{/if}
		</button>
	</div>
</form>

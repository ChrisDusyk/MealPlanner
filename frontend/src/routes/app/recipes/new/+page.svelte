<script lang="ts">
	import { goto } from '$app/navigation';
	import { slide, fly, fade } from 'svelte/transition';
	import { flip } from 'svelte/animate';
	import { quintOut } from 'svelte/easing';
	import type { Ingredient, CreateRecipeRequest } from '$lib/api/recipeApi';

	// Form state
	let name = $state('');
	let description = $state('');
	let sourceUrl = $state('');
	let ingredients: (Ingredient & { _id: number })[] = $state([]);
	let nextId = $state(1);

	// UI state
	let submitting = $state(false);
	let errorMessage = $state('');
	let validationErrors: Record<string, string> = $state({});

	// Common units for the select dropdown
	const units = [
		'', 'tsp', 'tbsp', 'cup', 'oz', 'fl oz', 'lb', 'g', 'kg', 'ml', 'L',
		'pinch', 'dash', 'piece', 'slice', 'clove', 'can', 'bottle', 'package', 'bunch', 'sprig', 'whole'
	];

	function addIngredient() {
		ingredients = [
			...ingredients,
			{ _id: nextId++, name: '', quantity: 0, unit: '' }
		];
	}

	function removeIngredient(id: number) {
		ingredients = ingredients.filter((i) => i._id !== id);
	}

	function validate(): boolean {
		const errors: Record<string, string> = {};

		if (!name.trim()) {
			errors.name = 'Recipe name is required.';
		}
		if (!sourceUrl.trim()) {
			errors.sourceUrl = 'Source URL is required.';
		} else {
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

	async function handleSubmit() {
		if (!validate()) return;

		submitting = true;
		errorMessage = '';

		const request: CreateRecipeRequest = {
			name: name.trim(),
			description: description.trim(),
			sourceUrl: sourceUrl.trim(),
			ingredients: ingredients
				.filter((i) => i.name.trim())
				.map((i) => ({
					name: i.name.trim(),
					quantity: i.quantity,
					unit: i.unit
				}))
		};

		try {
			const response = await fetch('/app/recipes/new', {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify(request)
			});

			if (!response.ok) {
				try {
					const contentType = response.headers.get('content-type') || '';
					if (contentType.includes('application/json')) {
						const result = await response.json();
						errorMessage = result.error || result.message || 'Failed to create recipe.';
					} else {
						errorMessage = (await response.text()) || 'Failed to create recipe.';
					}
				} catch {
					errorMessage = 'Failed to create recipe.';
				}
				return;
			}

			await goto('/app/recipes');
		} catch (err) {
			errorMessage = 'An unexpected error occurred. Please try again.';
		} finally {
			submitting = false;
		}
	}
</script>

<svelte:head>
	<title>New Recipe — MealPlanner</title>
</svelte:head>

<div class="mx-auto max-w-3xl">
	<!-- Page header -->
	<div class="mb-8">
		<a
			href="/app/recipes"
			class="mb-4 inline-flex items-center gap-1.5 font-display text-sm font-medium text-green-600 transition-colors hover:text-green-700"
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
			Back to Recipes
		</a>
		<h1 class="font-display text-3xl font-bold text-charcoal">New Recipe</h1>
		<p class="mt-1 text-charcoal/60">Add a new recipe to your collection.</p>
	</div>

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
			<span>{errorMessage}</span>
			<button
				onclick={() => (errorMessage = '')}
				class="ml-auto text-red-400 transition-colors hover:text-red-600"
				aria-label="Dismiss error"
			>
				<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
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
		class="space-y-8"
	>
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
						class="w-full rounded-lg border px-4 py-2.5 text-sm text-charcoal transition-colors placeholder:text-charcoal/30 focus:outline-none focus:ring-2 focus:ring-green-500/40
						{validationErrors.name
							? 'border-red-300 bg-red-50/50 focus:border-red-400'
							: 'border-green-200/60 bg-white focus:border-green-400'}"
					/>
					{#if validationErrors.name}
						<p id="recipe-name-error" transition:slide={{ duration: 200 }} class="mt-1 text-xs text-red-500">
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
						class="w-full resize-none rounded-lg border border-green-200/60 bg-white px-4 py-2.5 text-sm text-charcoal transition-colors placeholder:text-charcoal/30 focus:border-green-400 focus:outline-none focus:ring-2 focus:ring-green-500/40"
					></textarea>
				</div>

				<!-- Source URL -->
				<div>
					<label for="recipe-url" class="mb-1.5 block text-sm font-medium text-charcoal/80">
						Source URL <span class="text-red-400">*</span>
					</label>
					<input
						id="recipe-url"
						type="url"
						bind:value={sourceUrl}
						placeholder="https://example.com/recipe"
						aria-invalid={!!validationErrors.sourceUrl}
						aria-describedby={validationErrors.sourceUrl ? 'recipe-url-error' : undefined}
						class="w-full rounded-lg border px-4 py-2.5 text-sm text-charcoal transition-colors placeholder:text-charcoal/30 focus:outline-none focus:ring-2 focus:ring-green-500/40
						{validationErrors.sourceUrl
							? 'border-red-300 bg-red-50/50 focus:border-red-400'
							: 'border-green-200/60 bg-white focus:border-green-400'}"
					/>
					{#if validationErrors.sourceUrl}
						<p id="recipe-url-error" transition:slide={{ duration: 200 }} class="mt-1 text-xs text-red-500">
							{validationErrors.sourceUrl}
						</p>
					{/if}
				</div>
			</div>
		</section>

		<!-- Ingredients Section -->
		<section class="overflow-hidden rounded-xl border border-green-200/50 bg-white shadow-sm">
			<div class="flex items-center justify-between border-b border-green-100/60 bg-green-50/30 px-6 py-4">
				<div>
					<h2 class="font-display text-lg font-semibold text-charcoal">Ingredients</h2>
					{#if ingredients.length > 0}
						<p transition:fade={{ duration: 200 }} class="mt-0.5 text-xs text-charcoal/40">
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
					<!-- Empty state -->
					<div
						transition:fade={{ duration: 200 }}
						class="flex flex-col items-center justify-center rounded-xl border-2 border-dashed border-green-200/40 py-12"
					>
						<div class="mb-3 flex h-12 w-12 items-center justify-center rounded-xl bg-green-100 text-green-500">
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
						<p class="mt-0.5 text-xs text-charcoal/30">Click "Add" above to start building your ingredient list</p>
					</div>
				{:else}
					<!-- Column headers -->
					<div class="mb-3 grid grid-cols-[1fr_5rem_7rem_2.5rem] gap-3 px-1 text-xs font-medium uppercase tracking-wider text-charcoal/40">
						<span>Ingredient</span>
						<span>Qty</span>
						<span>Unit</span>
						<span></span>
					</div>

					<!-- Ingredient rows -->
					<div class="space-y-2">
						{#each ingredients as ingredient (ingredient._id)}
							<div
								class="group grid grid-cols-[1fr_5rem_7rem_2.5rem] items-start gap-3 rounded-lg border border-green-100/60 bg-green-50/20 p-3 transition-all hover:border-green-200/80 hover:bg-green-50/50"
								animate:flip={{ duration: 300, easing: quintOut }}
								in:fly={{ y: -10, duration: 300, easing: quintOut }}
								out:slide={{ duration: 250, easing: quintOut }}
							>
								<!-- Name -->
								<div>
									<input
										type="text"
										bind:value={ingredient.name}
										placeholder="e.g. Chicken breast"
										aria-label="Ingredient name"
										aria-invalid={!!validationErrors[`ingredient-${ingredient._id}-name`]}
										class="w-full rounded-md border border-green-200/50 bg-white px-3 py-2 text-sm text-charcoal transition-colors placeholder:text-charcoal/25 focus:border-green-400 focus:outline-none focus:ring-2 focus:ring-green-500/30
										{validationErrors[`ingredient-${ingredient._id}-name`]
											? 'border-red-300 bg-red-50/50'
											: ''}"
									/>
									{#if validationErrors[`ingredient-${ingredient._id}-name`]}
										<p transition:slide={{ duration: 150 }} class="mt-0.5 text-[11px] text-red-500">
											{validationErrors[`ingredient-${ingredient._id}-name`]}
										</p>
									{/if}
								</div>

								<!-- Quantity -->
								<div>
									<input
										type="number"
										bind:value={ingredient.quantity}
										min="0"
										max="9999"
										step="0.25"
										placeholder="0"
										aria-label="Quantity"
										aria-invalid={!!validationErrors[`ingredient-${ingredient._id}-qty`]}
										class="w-full rounded-md border border-green-200/50 bg-white px-3 py-2 text-sm text-charcoal transition-colors placeholder:text-charcoal/25 focus:border-green-400 focus:outline-none focus:ring-2 focus:ring-green-500/30
										{validationErrors[`ingredient-${ingredient._id}-qty`]
											? 'border-red-300 bg-red-50/50'
											: ''}"
									/>
									{#if validationErrors[`ingredient-${ingredient._id}-qty`]}
										<p transition:slide={{ duration: 150 }} class="mt-0.5 text-[11px] text-red-500">
											{validationErrors[`ingredient-${ingredient._id}-qty`]}
										</p>
									{/if}
								</div>

								<!-- Unit -->
								<select
									bind:value={ingredient.unit}
									aria-label="Unit"
									class="w-full appearance-none rounded-md border border-green-200/50 bg-white px-3 py-2 text-sm text-charcoal transition-colors focus:border-green-400 focus:outline-none focus:ring-2 focus:ring-green-500/30"
								>
									{#each units as u}
										<option value={u}>{u || '—'}</option>
									{/each}
								</select>

								<!-- Remove button -->
								<button
									type="button"
									onclick={() => removeIngredient(ingredient._id)}
									class="mt-0.5 flex h-9 w-9 items-center justify-center rounded-md text-charcoal/30 transition-all hover:bg-red-50 hover:text-red-500 active:scale-90"
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
						<circle cx="12" cy="12" r="10" stroke="currentColor" stroke-width="3" class="opacity-25" />
						<path
							fill="currentColor"
							class="opacity-75"
							d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
						/>
					</svg>
					Saving...
				{:else}
					Save Recipe
				{/if}
			</button>
		</div>
	</form>
</div>

<script lang="ts">
	import { untrack } from 'svelte';
	import {
		FEATURE_FLAG_VALUE_TYPES,
		type CreateFeatureFlagRequest,
		type FeatureFlagValueType
	} from '$lib/api/featureFlagsApi';
	import {
		buildDefinition,
		parseDefinition,
		type VariantDraft
	} from '$lib/featureFlags/definition';
	import {
		buildTargeting,
		parseTargeting,
		TARGETING_ATTRIBUTES,
		TARGETING_OPERATOR_LABELS,
		TARGETING_OPERATORS,
		type PercentageRollout,
		type TargetingRule
	} from '$lib/featureFlags/targeting';

	export interface FeatureFlagFormData {
		key: string;
		description: string;
		valueType: FeatureFlagValueType;
		enabled: boolean;
		disabledVariant: string;
		definitionJson: string;
	}

	let {
		initialData,
		mode,
		submitting = false,
		errorMessage = '',
		onsubmit,
		oncancel
	}: {
		initialData?: FeatureFlagFormData;
		mode: 'create' | 'edit';
		submitting?: boolean;
		errorMessage?: string;
		onsubmit: (data: CreateFeatureFlagRequest) => void;
		oncancel?: () => void;
	} = $props();

	const init = untrack(() => initialData);
	const parsed = parseDefinition(init?.definitionJson ?? '');
	const initialTargeting = parseTargeting(
		parsed.targetingJson === '' ? null : JSON.parse(parsed.targetingJson)
	);

	let key = $state(init?.key ?? '');
	let description = $state(init?.description ?? '');
	let valueType = $state<FeatureFlagValueType>(init?.valueType ?? 'boolean');
	let enabled = $state(init?.enabled ?? false);
	let disabledVariant = $state(init?.disabledVariant ?? '');
	let defaultVariant = $state(parsed.defaultVariant);
	let variants = $state<VariantDraft[]>(
		parsed.variants.length > 0
			? parsed.variants
			: [
					{ name: 'on', valueJson: 'true' },
					{ name: 'off', valueJson: 'false' }
				]
	);

	// Targeting the builder cannot express must not be silently rewritten, so an
	// unparseable block locks the editor into raw JSON mode.
	let targetingMode = $state<'builder' | 'raw'>(initialTargeting === null ? 'raw' : 'builder');
	let builderLocked = $state(initialTargeting === null && parsed.targetingJson !== '');
	let rules = $state<TargetingRule[]>(initialTargeting?.rules ?? []);
	let rollout = $state<PercentageRollout | null>(initialTargeting?.rollout ?? null);
	let targetingJson = $state(parsed.targetingJson);

	let localError = $state('');

	const variantNames = $derived(
		variants.map((variant) => variant.name.trim()).filter((name) => name !== '')
	);

	/** Placeholder that shows the shape a variant value should take. */
	const valuePlaceholder = $derived(
		{
			boolean: 'true',
			string: '"beta"',
			number: '42',
			object: '{ "limit": 10 }'
		}[valueType]
	);

	function addVariant() {
		variants = [...variants, { name: '', valueJson: '' }];
	}

	function removeVariant(index: number) {
		const removed = variants[index]?.name.trim();
		variants = variants.filter((_, i) => i !== index);

		if (removed && defaultVariant === removed) defaultVariant = '';
		if (removed && disabledVariant === removed) disabledVariant = '';
	}

	function addRule() {
		rules = [...rules, { attribute: 'role', operator: '==', value: '', variant: '' }];
	}

	function removeRule(index: number) {
		rules = rules.filter((_, i) => i !== index);
	}

	function toggleRollout() {
		rollout = rollout
			? null
			: { variant: variantNames[0] ?? '', percentage: 10, fallbackVariant: variantNames[1] ?? '' };
	}

	/** Moves the current builder rules into the raw editor so nothing is lost. */
	function switchToRaw() {
		const built = buildTargeting({ rules, rollout });
		targetingJson = built === null ? '' : JSON.stringify(built, null, 2);
		targetingMode = 'raw';
	}

	function switchToBuilder() {
		const trimmed = targetingJson.trim();

		if (trimmed === '') {
			rules = [];
			rollout = null;
			targetingMode = 'builder';
			builderLocked = false;
			return;
		}

		let parsedRules: ReturnType<typeof parseTargeting> = null;
		try {
			parsedRules = parseTargeting(JSON.parse(trimmed));
		} catch {
			parsedRules = null;
		}

		if (parsedRules === null) {
			localError =
				'These targeting rules use JsonLogic the builder cannot represent. Keep editing them as raw JSON.';
			builderLocked = true;
			return;
		}

		rules = parsedRules.rules;
		rollout = parsedRules.rollout;
		targetingMode = 'builder';
		builderLocked = false;
	}

	function handleSubmit(event: SubmitEvent) {
		event.preventDefault();
		localError = '';

		let resolvedTargeting = targetingJson;
		if (targetingMode === 'builder') {
			const built = buildTargeting({ rules, rollout });
			resolvedTargeting = built === null ? '' : JSON.stringify(built);
		}

		const definition = buildDefinition({
			variants,
			defaultVariant,
			disabledVariant,
			targetingJson: resolvedTargeting,
			valueType
		});

		if ('error' in definition) {
			localError = definition.error;
			return;
		}

		onsubmit({
			key: key.trim(),
			valueType,
			enabled,
			disabledVariant: disabledVariant === '' ? null : disabledVariant,
			definitionJson: definition.definitionJson,
			description: description.trim() === '' ? null : description.trim()
		});
	}

	const inputClass =
		'w-full rounded-lg border border-green-200 px-3 py-2 text-sm focus:border-green-500 focus:outline-none focus:ring-2 focus:ring-green-200';
	const labelClass = 'block text-sm font-semibold text-charcoal';
	const hintClass = 'mt-1 text-xs text-charcoal/60';
</script>

<form onsubmit={handleSubmit} class="space-y-8">
	{#if errorMessage || localError}
		<p
			class="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700"
			role="alert"
			data-testid="feature-flag-form-error"
		>
			{errorMessage || localError}
		</p>
	{/if}

	<section class="space-y-4 rounded-xl border border-green-200/60 bg-white p-4 shadow-sm">
		<h2 class="font-display text-lg font-semibold text-charcoal">Details</h2>

		<div>
			<label class={labelClass} for="flag-key">Key</label>
			{#if mode === 'create'}
				<input
					id="flag-key"
					class={inputClass}
					bind:value={key}
					placeholder="new-checkout-flow"
					autocomplete="off"
					required
				/>
				<p class={hintClass}>
					Lowercase letters, numbers, and hyphens. This is what calling code evaluates, so it cannot
					be changed later.
				</p>
			{:else}
				<p
					class="rounded-lg border border-green-100 bg-green-50/60 px-3 py-2 font-mono text-sm text-charcoal"
				>
					{key}
				</p>
				<p class={hintClass}>Keys are immutable — delete and recreate the flag to rename it.</p>
			{/if}
		</div>

		<div>
			<label class={labelClass} for="flag-description">Description</label>
			<input
				id="flag-description"
				class={inputClass}
				bind:value={description}
				placeholder="What this flag controls"
			/>
		</div>

		<div>
			<label class={labelClass} for="flag-value-type">Value type</label>
			<select id="flag-value-type" class={inputClass} bind:value={valueType}>
				{#each FEATURE_FLAG_VALUE_TYPES as type (type)}
					<option value={type}>{type}</option>
				{/each}
			</select>
			<p class={hintClass}>Every variant of a flag must be the same type.</p>
		</div>
	</section>

	<section class="space-y-4 rounded-xl border border-green-200/60 bg-white p-4 shadow-sm">
		<div class="flex items-center justify-between gap-4">
			<h2 class="font-display text-lg font-semibold text-charcoal">Variants</h2>
			<button
				type="button"
				onclick={addVariant}
				class="rounded-lg border border-green-300 bg-white px-3 py-1.5 text-xs font-semibold text-green-700 hover:bg-green-50"
			>
				Add variant
			</button>
		</div>

		<ul class="space-y-3">
			{#each variants as variant, index (index)}
				<li class="flex flex-wrap items-start gap-2 sm:flex-nowrap">
					<div class="min-w-0 flex-1">
						<label class="sr-only" for="variant-name-{index}">Variant {index + 1} name</label>
						<input
							id="variant-name-{index}"
							class={inputClass}
							bind:value={variant.name}
							placeholder="Name"
							autocomplete="off"
						/>
					</div>
					<div class="min-w-0 flex-1">
						<label class="sr-only" for="variant-value-{index}">Variant {index + 1} value</label>
						<input
							id="variant-value-{index}"
							class="{inputClass} font-mono"
							bind:value={variant.valueJson}
							placeholder={valuePlaceholder}
							autocomplete="off"
						/>
					</div>
					<button
						type="button"
						onclick={() => removeVariant(index)}
						class="shrink-0 px-2 py-2 text-xs font-medium text-red-600 hover:underline"
					>
						Remove
					</button>
				</li>
			{/each}
		</ul>

		{#if variants.length === 0}
			<p class="text-sm text-charcoal/60">No variants yet — add at least one.</p>
		{/if}
	</section>

	<section class="space-y-4 rounded-xl border border-green-200/60 bg-white p-4 shadow-sm">
		<h2 class="font-display text-lg font-semibold text-charcoal">Defaults</h2>

		<div class="flex items-center gap-3">
			<input id="flag-enabled" type="checkbox" bind:checked={enabled} class="h-4 w-4" />
			<label class={labelClass} for="flag-enabled">Flag is on</label>
		</div>

		<div>
			<label class={labelClass} for="flag-default-variant">Serve while on</label>
			<select id="flag-default-variant" class={inputClass} bind:value={defaultVariant}>
				<option value="">Choose a variant…</option>
				{#each variantNames as name (name)}
					<option value={name}>{name}</option>
				{/each}
			</select>
			<p class={hintClass}>Used when no targeting rule matches.</p>
		</div>

		<div>
			<label class={labelClass} for="flag-disabled-variant">Serve while off</label>
			<select id="flag-disabled-variant" class={inputClass} bind:value={disabledVariant}>
				<option value="">Fall back to each caller's code default</option>
				{#each variantNames as name (name)}
					<option value={name}>{name}</option>
				{/each}
			</select>
			<p class={hintClass}>
				Pick a variant to keep the off value under your control here — targeting is ignored while the
				flag is off, so switching it off is a kill switch. Leaving this blank means flagd reports the
				flag as disabled and every caller falls back to whatever default it passes in code.
			</p>
		</div>
	</section>

	<section class="space-y-4 rounded-xl border border-green-200/60 bg-white p-4 shadow-sm">
		<div class="flex flex-wrap items-center justify-between gap-2">
			<h2 class="font-display text-lg font-semibold text-charcoal">Targeting</h2>
			{#if targetingMode === 'builder'}
				<button
					type="button"
					onclick={switchToRaw}
					class="text-xs font-medium text-green-700 hover:underline"
				>
					Edit as JSON
				</button>
			{:else}
				<button
					type="button"
					onclick={switchToBuilder}
					class="text-xs font-medium text-green-700 hover:underline"
				>
					Back to rule builder
				</button>
			{/if}
		</div>

		{#if targetingMode === 'builder'}
			<p class={hintClass}>
				Rules are checked top to bottom while the flag is on; the first match wins. Anything that
				matches no rule gets the "serve while on" variant.
			</p>

			<ul class="space-y-3">
				{#each rules as rule, index (index)}
					<li class="flex flex-wrap items-center gap-2">
						<label class="sr-only" for="rule-attribute-{index}">Rule {index + 1} attribute</label>
						<select id="rule-attribute-{index}" class="{inputClass} w-auto" bind:value={rule.attribute}>
							{#each TARGETING_ATTRIBUTES as attribute (attribute)}
								<option value={attribute}>{attribute}</option>
							{/each}
						</select>

						<label class="sr-only" for="rule-operator-{index}">Rule {index + 1} operator</label>
						<select id="rule-operator-{index}" class="{inputClass} w-auto" bind:value={rule.operator}>
							{#each TARGETING_OPERATORS as operator (operator)}
								<option value={operator}>{TARGETING_OPERATOR_LABELS[operator]}</option>
							{/each}
						</select>

						<label class="sr-only" for="rule-value-{index}">Rule {index + 1} value</label>
						<input
							id="rule-value-{index}"
							class="{inputClass} w-auto flex-1"
							bind:value={rule.value}
							placeholder={rule.operator === 'in' ? 'a@x.com, b@x.com' : 'admin'}
							autocomplete="off"
						/>

						<span class="text-sm text-charcoal/60">serve</span>

						<label class="sr-only" for="rule-variant-{index}">Rule {index + 1} variant</label>
						<select id="rule-variant-{index}" class="{inputClass} w-auto" bind:value={rule.variant}>
							<option value="">Choose…</option>
							{#each variantNames as name (name)}
								<option value={name}>{name}</option>
							{/each}
						</select>

						<button
							type="button"
							onclick={() => removeRule(index)}
							class="px-2 text-xs font-medium text-red-600 hover:underline"
						>
							Remove
						</button>
					</li>
				{/each}
			</ul>

			<button
				type="button"
				onclick={addRule}
				class="rounded-lg border border-green-300 bg-white px-3 py-1.5 text-xs font-semibold text-green-700 hover:bg-green-50"
			>
				Add rule
			</button>

			<div class="border-t border-green-100 pt-4">
				<div class="flex items-center gap-3">
					<input
						id="flag-rollout-enabled"
						type="checkbox"
						checked={rollout !== null}
						onchange={toggleRollout}
						class="h-4 w-4"
					/>
					<label class={labelClass} for="flag-rollout-enabled">Percentage rollout</label>
				</div>

				{#if rollout}
					<div class="mt-3 flex flex-wrap items-center gap-2">
						<label class="sr-only" for="flag-rollout-percentage">Rollout percentage</label>
						<input
							id="flag-rollout-percentage"
							type="number"
							min="1"
							max="99"
							class="{inputClass} w-24"
							bind:value={rollout.percentage}
						/>
						<span class="text-sm text-charcoal/60">% of users get</span>

						<label class="sr-only" for="flag-rollout-variant">Rollout variant</label>
						<select id="flag-rollout-variant" class="{inputClass} w-auto" bind:value={rollout.variant}>
							<option value="">Choose…</option>
							{#each variantNames as name (name)}
								<option value={name}>{name}</option>
							{/each}
						</select>

						<span class="text-sm text-charcoal/60">, the rest get</span>

						<label class="sr-only" for="flag-rollout-fallback">Rollout fallback variant</label>
						<select
							id="flag-rollout-fallback"
							class="{inputClass} w-auto"
							bind:value={rollout.fallbackVariant}
						>
							<option value="">Choose…</option>
							{#each variantNames as name (name)}
								<option value={name}>{name}</option>
							{/each}
						</select>
					</div>
					<p class={hintClass}>
						Buckets are stable per user — the same person keeps the same variant as the percentage
						grows.
					</p>
				{/if}
			</div>
		{:else}
			{#if builderLocked}
				<p class="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
					These rules use JsonLogic the builder cannot represent, so they stay in raw JSON to avoid
					rewriting them.
				</p>
			{/if}
			<label class="sr-only" for="flag-targeting-json">Targeting rules as JSON</label>
			<textarea
				id="flag-targeting-json"
				class="{inputClass} h-48 font-mono"
				bind:value={targetingJson}
				placeholder={'{\n  "if": [{ "==": [{ "var": "role" }, "admin"] }, "on"]\n}'}
			></textarea>
			<p class={hintClass}>
				flagd JsonLogic. Leave blank for no targeting. Rules return a variant name.
			</p>
		{/if}
	</section>

	<div class="flex items-center gap-3">
		<button
			type="submit"
			disabled={submitting}
			class="rounded-lg bg-green-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-green-700 disabled:opacity-50"
		>
			{submitting ? 'Saving…' : mode === 'create' ? 'Create flag' : 'Save changes'}
		</button>
		{#if oncancel}
			<button
				type="button"
				onclick={oncancel}
				class="rounded-lg border border-green-300 bg-white px-4 py-2 text-sm font-semibold text-green-700 hover:bg-green-50"
			>
				Cancel
			</button>
		{/if}
	</div>
</form>

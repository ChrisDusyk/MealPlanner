<script lang="ts">
	import { goto, invalidateAll } from '$app/navigation';
	import { resolve } from '$app/paths';
	import type {
		CreateFeatureFlagRequest,
		EvaluateFeatureFlagResponse
	} from '$lib/api/featureFlagsApi';
	import FeatureFlagForm from '$lib/components/admin/FeatureFlagForm.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	let submitting = $state(false);
	let errorMessage = $state('');

	let targetingKey = $state('');
	let email = $state('');
	let role = $state('');
	let evaluating = $state(false);
	let evaluation = $state<EvaluateFeatureFlagResponse | null>(null);
	let evaluationError = $state('');

	async function handleSubmit(payload: CreateFeatureFlagRequest) {
		submitting = true;
		errorMessage = '';

		try {
			const response = await fetch(
				resolve('/app/admin/feature-flags/[key]', { key: data.flag.key }),
				{
					method: 'PUT',
					headers: { 'Content-Type': 'application/json' },
					body: JSON.stringify(payload)
				}
			);

			if (!response.ok) {
				const body = await response.json().catch(() => ({}));
				throw new Error(body?.error ?? 'Failed to save the feature flag.');
			}

			await invalidateAll();
			await goto(resolve('/app/admin/feature-flags'));
		} catch (err) {
			errorMessage = err instanceof Error ? err.message : 'Failed to save the feature flag.';
		} finally {
			submitting = false;
		}
	}

	async function handleEvaluate() {
		evaluating = true;
		evaluationError = '';
		evaluation = null;

		try {
			const response = await fetch(
				resolve('/app/admin/feature-flags/[key]/evaluate', { key: data.flag.key }),
				{
					method: 'POST',
					headers: { 'Content-Type': 'application/json' },
					body: JSON.stringify({
						targetingKey: targetingKey || null,
						email: email || null,
						role: role || null
					})
				}
			);

			const body = await response.json().catch(() => ({}));
			if (!response.ok) {
				throw new Error(body?.error ?? 'Failed to evaluate the flag.');
			}

			evaluation = body as EvaluateFeatureFlagResponse;
		} catch (err) {
			evaluationError = err instanceof Error ? err.message : 'Failed to evaluate the flag.';
		} finally {
			evaluating = false;
		}
	}

	const inputClass =
		'w-full rounded-lg border border-green-200 px-3 py-2 text-sm focus:border-green-500 focus:outline-none focus:ring-2 focus:ring-green-200';
</script>

<svelte:head>
	<title>{data.flag.key} | Feature flags | Admin | Meal Planner</title>
</svelte:head>

<div class="mx-auto max-w-3xl">
	<header class="mb-8">
		<a
			href={resolve('/app/admin/feature-flags')}
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
			Back to feature flags
		</a>
		<h1 class="font-display text-2xl font-bold text-charcoal sm:text-3xl">{data.flag.key}</h1>
		<p class="mt-1 text-charcoal/60">
			Last updated {new Date(data.flag.updatedAt).toLocaleString()}.
		</p>
	</header>

	<FeatureFlagForm
		mode="edit"
		initialData={{
			key: data.flag.key,
			description: data.flag.description ?? '',
			valueType: data.flag.valueType,
			enabled: data.flag.enabled,
			disabledVariant: data.flag.disabledVariant ?? '',
			definitionJson: data.flag.definitionJson
		}}
		{submitting}
		{errorMessage}
		onsubmit={handleSubmit}
		oncancel={() => goto(resolve('/app/admin/feature-flags'))}
	/>

	<section class="mt-8 space-y-4 rounded-xl border border-green-200/60 bg-white p-4 shadow-sm">
		<div>
			<h2 class="font-display text-lg font-semibold text-charcoal">Test evaluation</h2>
			<p class="mt-1 text-xs text-charcoal/60">
				Resolves through live flagd, so it reflects the document flagd last synced — save your
				changes and wait for the next poll before testing them.
			</p>
		</div>

		<div class="grid gap-3 sm:grid-cols-3">
			<div>
				<label class="block text-sm font-semibold text-charcoal" for="evaluate-targeting-key">
					targetingKey
				</label>
				<input
					id="evaluate-targeting-key"
					class={inputClass}
					bind:value={targetingKey}
					placeholder="user id"
					autocomplete="off"
				/>
			</div>
			<div>
				<label class="block text-sm font-semibold text-charcoal" for="evaluate-email">email</label>
				<input
					id="evaluate-email"
					class={inputClass}
					bind:value={email}
					placeholder="chef@example.com"
					autocomplete="off"
				/>
			</div>
			<div>
				<label class="block text-sm font-semibold text-charcoal" for="evaluate-role">role</label>
				<input
					id="evaluate-role"
					class={inputClass}
					bind:value={role}
					placeholder="admin"
					autocomplete="off"
				/>
			</div>
		</div>

		<button
			type="button"
			onclick={handleEvaluate}
			disabled={evaluating}
			class="rounded-lg border border-green-300 bg-white px-4 py-2 text-sm font-semibold text-green-700 hover:bg-green-50 disabled:opacity-50"
		>
			{evaluating ? 'Evaluating…' : 'Evaluate'}
		</button>

		{#if evaluationError}
			<p class="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700" role="alert">
				{evaluationError}
			</p>
		{:else if evaluation}
			<p class="rounded-lg border border-green-100 bg-green-50/60 p-3 text-sm text-charcoal">
				Resolves to <span class="font-mono font-semibold">{evaluation.valueJson}</span>
			</p>
		{/if}
	</section>
</div>

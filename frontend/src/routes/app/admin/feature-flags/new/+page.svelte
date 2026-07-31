<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import type { CreateFeatureFlagRequest } from '$lib/api/featureFlagsApi';
	import FeatureFlagForm from '$lib/components/admin/FeatureFlagForm.svelte';

	let submitting = $state(false);
	let errorMessage = $state('');

	async function handleSubmit(payload: CreateFeatureFlagRequest) {
		submitting = true;
		errorMessage = '';

		try {
			const response = await fetch(resolve('/app/admin/feature-flags'), {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify(payload)
			});

			if (!response.ok) {
				const body = await response.json().catch(() => ({}));
				throw new Error(body?.error ?? 'Failed to create the feature flag.');
			}

			await goto(resolve('/app/admin/feature-flags'));
		} catch (err) {
			errorMessage = err instanceof Error ? err.message : 'Failed to create the feature flag.';
		} finally {
			submitting = false;
		}
	}
</script>

<svelte:head>
	<title>New feature flag | Admin | Meal Planner</title>
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
		<h1 class="font-display text-2xl font-bold text-charcoal sm:text-3xl">New feature flag</h1>
		<p class="mt-1 text-charcoal/60">
			flagd picks the flag up on its next sync poll, so no redeploy is needed.
		</p>
	</header>

	<FeatureFlagForm
		mode="create"
		{submitting}
		{errorMessage}
		onsubmit={handleSubmit}
		oncancel={() => goto(resolve('/app/admin/feature-flags'))}
	/>
</div>

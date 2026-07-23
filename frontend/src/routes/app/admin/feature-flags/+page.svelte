<script lang="ts">
	import { enhance } from '$app/forms';
	import type { PageData, ActionData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	// Key currently being toggled, used to disable its control while in flight.
	let pendingKey = $state<string | null>(null);
</script>

<svelte:head>
	<title>Feature flags | Admin | Meal Planner</title>
</svelte:head>

<div class="mx-auto max-w-3xl">
	<header class="mb-8">
		<h1 class="font-display text-2xl font-bold text-charcoal sm:text-3xl">Feature flags</h1>
		<p class="mt-1 text-charcoal/60">
			Toggle flags served by flagd through OpenFeature. Changes are picked up on flagd's next sync
			poll, so allow a few seconds and reload to see the effect.
		</p>
	</header>

	{#if form?.message}
		<p class="mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700">
			{form.message}
		</p>
	{/if}

	{#if data.flags.length === 0}
		<p class="text-charcoal/60">No feature flags are defined.</p>
	{:else}
		<ul class="space-y-3">
			{#each data.flags as flag (flag.key)}
				<li
					class="flex items-center justify-between gap-4 rounded-xl border border-green-200/60 bg-white p-4 shadow-sm"
				>
					<div class="min-w-0">
						<p class="truncate font-display font-semibold text-charcoal">{flag.key}</p>
						{#if flag.description}
							<p class="mt-0.5 text-sm text-charcoal/60">{flag.description}</p>
						{/if}
					</div>

					<form
						method="POST"
						action="?/toggle"
						use:enhance={() => {
							pendingKey = flag.key;
							return async ({ update }) => {
								await update();
								pendingKey = null;
							};
						}}
					>
						<input type="hidden" name="key" value={flag.key} />
						<input type="hidden" name="enabled" value={String(!flag.enabled)} />
						<button
							type="submit"
							disabled={pendingKey === flag.key}
							aria-pressed={flag.enabled}
							class="inline-flex h-7 w-12 items-center rounded-full transition-colors disabled:opacity-50 {flag.enabled
								? 'bg-green-500'
								: 'bg-charcoal/20'}"
							title={flag.enabled ? 'Enabled — click to disable' : 'Disabled — click to enable'}
						>
							<span
								class="inline-block h-5 w-5 transform rounded-full bg-white shadow transition-transform {flag.enabled
									? 'translate-x-6'
									: 'translate-x-1'}"
							></span>
							<span class="sr-only">{flag.enabled ? 'Disable' : 'Enable'} {flag.key}</span>
						</button>
					</form>
				</li>
			{/each}
		</ul>
	{/if}
</div>

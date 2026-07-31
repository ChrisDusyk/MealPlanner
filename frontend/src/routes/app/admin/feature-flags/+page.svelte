<script lang="ts">
	import { enhance } from '$app/forms';
	import { invalidateAll } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { describeResolvedValues } from '$lib/featureFlags/definition';
	import type { PageData, ActionData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	// Key currently being toggled, used to disable its control while in flight.
	let pendingKey = $state<string | null>(null);
	let deletingKey = $state<string | null>(null);
	let deleteError = $state('');

	async function handleDelete(key: string) {
		// Deleting a flag does not break callers — flagd simply stops knowing the
		// key and every caller falls back to its own default, which is easy to
		// miss, so spell it out before removing anything.
		const confirmed = confirm(
			`Delete the flag "${key}"?\n\nAny code still evaluating this key will silently fall back to the default it passes in code.`
		);
		if (!confirmed) return;

		deletingKey = key;
		deleteError = '';

		try {
			const response = await fetch(
				resolve('/app/admin/feature-flags/[key]', { key }),
				{ method: 'DELETE' }
			);

			if (!response.ok) {
				const body = await response.json().catch(() => ({}));
				throw new Error(body?.error ?? `Failed to delete "${key}".`);
			}

			await invalidateAll();
		} catch (err) {
			deleteError = err instanceof Error ? err.message : `Failed to delete "${key}".`;
		} finally {
			deletingKey = null;
		}
	}
</script>

<svelte:head>
	<title>Feature flags | Admin | Meal Planner</title>
</svelte:head>

<div class="mx-auto max-w-3xl">
	<header class="mb-8 flex flex-wrap items-start justify-between gap-4">
		<div>
			<h1 class="font-display text-2xl font-bold text-charcoal sm:text-3xl">Feature flags</h1>
			<p class="mt-1 text-charcoal/60">
				Flags served by flagd through OpenFeature. Changes are picked up on flagd's next sync poll,
				so allow a few seconds and reload to see the effect.
			</p>
		</div>
		<a
			href={resolve('/app/admin/feature-flags/new')}
			class="rounded-lg bg-green-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-green-700"
		>
			New flag
		</a>
	</header>

	{#if form?.message || deleteError}
		<p class="mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700">
			{form?.message || deleteError}
		</p>
	{/if}

	{#if data.flags.length === 0}
		<div
			class="rounded-2xl border-2 border-dashed border-green-300/40 bg-green-50/50 px-6 py-16 text-center"
		>
			<p class="text-charcoal/60">No feature flags are defined.</p>
			<a
				href={resolve('/app/admin/feature-flags/new')}
				class="mt-4 inline-block text-sm font-medium text-green-700 hover:underline"
			>
				Create the first one
			</a>
		</div>
	{:else}
		<ul class="space-y-3">
			{#each data.flags as flag (flag.key)}
				{@const resolved = describeResolvedValues(flag)}
				<li class="rounded-xl border border-green-200/60 bg-white p-4 shadow-sm">
					<div class="flex items-start justify-between gap-4">
						<div class="min-w-0">
							<p class="flex flex-wrap items-center gap-2">
								<span class="truncate font-display font-semibold text-charcoal">{flag.key}</span>
								<span
									class="inline-flex items-center rounded-full bg-green-100 px-2 py-0.5 text-xs font-semibold text-green-800"
								>
									{flag.valueType}
								</span>
							</p>
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
					</div>

					<dl class="mt-3 flex flex-wrap gap-x-6 gap-y-1 text-xs text-charcoal/60">
						<div class="flex gap-1.5">
							<dt>On serves</dt>
							<dd class="font-mono text-charcoal">{resolved.onValue ?? '—'}</dd>
						</div>
						<div class="flex gap-1.5">
							<dt>Off serves</dt>
							<dd class="font-mono text-charcoal">
								{resolved.offValue ?? "each caller's code default"}
							</dd>
						</div>
					</dl>

					<div class="mt-3 flex items-center gap-4 border-t border-green-100 pt-3">
						<a
							href={resolve('/app/admin/feature-flags/[key]', { key: flag.key })}
							class="text-xs font-medium text-green-700 hover:underline"
						>
							Edit
						</a>
						<button
							type="button"
							onclick={() => handleDelete(flag.key)}
							disabled={deletingKey === flag.key}
							class="text-xs font-medium text-red-600 hover:underline disabled:opacity-50"
						>
							{deletingKey === flag.key ? 'Deleting…' : 'Delete'}
						</button>
					</div>
				</li>
			{/each}
		</ul>
	{/if}
</div>

<script lang="ts">
	import { enhance } from '$app/forms';
	import { onMount, untrack } from 'svelte';
	import type { ActionData, PageData } from './$types';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	let submitting = $state(false);
	// Using untrack so svelte-check doesn't warn about only capturing the
	// initial prop values here—that's intentional since the form inputs take
	// over as the source of truth once the component mounts.
	let displayName = $state(
		untrack(() => form?.displayName ?? data.appUser?.displayName ?? data.appUser?.name ?? '')
	);
	let timezone = $state(
		untrack(() => form?.timezone ?? data.appUser?.timezone ?? '')
	);

	onMount(() => {
		// Prefill with the browser's best guess at the user's timezone so most
		// people can hit continue without having to think about it.
		if (!timezone && typeof Intl !== 'undefined') {
			const guess = Intl.DateTimeFormat().resolvedOptions().timeZone;
			if (guess) {
				timezone = guess;
			}
		}
	});
</script>

<svelte:head>
	<title>Welcome · Simple Meal Planner</title>
</svelte:head>

<div class="mx-auto flex min-h-[60vh] max-w-lg items-center justify-center px-4 py-10">
	<div class="w-full rounded-2xl border border-green-100 bg-white p-8 shadow-lg">
		<h1 class="font-display text-2xl font-bold text-green-950">Let's finish setting up</h1>
		<p class="mt-1 text-sm text-green-800/70">
			Tell us what to call you and which timezone to use so your meal plans and grocery lists line up
			with your week.
		</p>

		<form
			method="post"
			class="mt-6 space-y-4"
			use:enhance={() => {
				submitting = true;
				return async ({ update }) => {
					await update();
					submitting = false;
				};
			}}
		>
			<label class="block text-sm font-medium text-green-900">
				Display name
				<input
					type="text"
					name="displayName"
					required
					autocomplete="name"
					bind:value={displayName}
					class="mt-1 block w-full rounded-lg border border-green-200 px-3 py-2 text-sm focus:border-green-500 focus:ring-1 focus:ring-green-500 focus:outline-none"
				/>
			</label>

			<label class="block text-sm font-medium text-green-900">
				Timezone
				<input
					type="text"
					name="timezone"
					placeholder="America/Toronto"
					autocomplete="off"
					bind:value={timezone}
					class="mt-1 block w-full rounded-lg border border-green-200 px-3 py-2 text-sm focus:border-green-500 focus:ring-1 focus:ring-green-500 focus:outline-none"
				/>
				<span class="mt-1 block text-xs text-green-700/60">
					We'll use this to schedule weekly plans. You can change it later in settings.
				</span>
			</label>

			{#if form?.error}
				<p class="text-sm text-red-600" role="alert">{form.error}</p>
			{/if}

			<button
				type="submit"
				disabled={submitting}
				class="w-full rounded-lg bg-green-600 px-4 py-2.5 font-display text-sm font-semibold text-white shadow transition hover:bg-green-500 disabled:opacity-60"
			>
				{submitting ? 'Saving…' : 'Continue to Meal Planner'}
			</button>
		</form>
	</div>
</div>

<script lang="ts">
	import { enhance } from '$app/forms';
	import { getContext } from 'svelte';
	import type { ActionData, PageData } from './$types';
	import { APP_USER_CONTEXT_KEY, type AppUserContextValue } from '$lib/context/appUserContext';

	let { data, form }: { data: PageData; form: ActionData } = $props();

	const appUserContext = getContext<AppUserContextValue | undefined>(APP_USER_CONTEXT_KEY);

	let submitting = $state(false);
	let successMessage = $state('');
	// svelte-ignore state_referenced_locally
	let name = $state(data.appUser?.name ?? data.session?.user?.name ?? '');

	$effect(() => {
		if (form?.user) {
			name = form.user.name;
			appUserContext?.setAppUser(form.user);
			successMessage = 'Account details saved.';
		}
	});

	function handleEnhance() {
		submitting = true;
		successMessage = '';

		return async ({ update }: { update: (options?: { reset?: boolean }) => Promise<void> }) => {
			submitting = false;
			await update({ reset: false });
		};
	}
</script>

<svelte:head>
	<title>Account — Simple Meal Planner</title>
</svelte:head>

<div class="mx-auto max-w-3xl">
	<div class="mb-8">
		<h1 class="font-display text-2xl font-bold text-charcoal sm:text-3xl">Account</h1>
		<p class="mt-1 text-charcoal/70">Manage your profile settings for Simple Meal Planner.</p>
	</div>

	<section class="rounded-2xl border border-green-200/60 bg-white p-6 shadow-sm" aria-labelledby="profile-section-heading">
		<div class="mb-5">
			<h2 id="profile-section-heading" class="font-display text-lg font-semibold text-charcoal">
				Profile
			</h2>
			<p class="mt-1 text-sm text-charcoal/60">Update your display name used across the app.</p>
		</div>

		<form method="POST" use:enhance={handleEnhance} class="space-y-4">
			<div>
				<label for="name" class="mb-1 block text-sm font-medium text-charcoal">Name</label>
				<input
					id="name"
					name="name"
					type="text"
					required
					bind:value={name}
					placeholder="Enter your name"
					class="w-full rounded-lg border border-green-200 bg-white px-3 py-2 text-sm text-charcoal shadow-sm transition-colors focus:border-green-400 focus:ring-1 focus:ring-green-400 focus:outline-none"
				/>
			</div>

			{#if form?.error}
				<p class="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700" role="alert">
					{form.error}
				</p>
			{/if}

			{#if successMessage}
				<p class="rounded-lg border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-700" role="status">
					{successMessage}
				</p>
			{/if}

			<div class="flex justify-end">
				<button
					type="submit"
					disabled={submitting}
					class="rounded-lg bg-green-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-green-700 disabled:cursor-not-allowed disabled:opacity-50"
				>
					{submitting ? 'Saving...' : 'Save'}
				</button>
			</div>
		</form>
	</section>
</div>

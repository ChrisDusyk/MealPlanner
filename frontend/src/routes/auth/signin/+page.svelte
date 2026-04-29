<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { authClient } from '$lib/auth/client';

	let email = $state('');
	let password = $state('');
	let submitting = $state(false);
	let error = $state<string | null>(null);

	async function onSubmit(event: SubmitEvent) {
		event.preventDefault();
		if (submitting) return;
		submitting = true;
		error = null;

		const result = await authClient.signIn.email({
			email: email.trim(),
			password,
			callbackURL: resolve('/app')
		});

		submitting = false;
		if (result.error) {
			error = result.error.message ?? 'Unable to sign in with those credentials.';
			return;
		}
		await goto(resolve('/app'));
	}

	async function onGoogle() {
		if (submitting) return;
		submitting = true;
		error = null;
		const result = await authClient.signIn.social({
			provider: 'google',
			callbackURL: resolve('/app')
		});
		if (result.error) {
			submitting = false;
			error = result.error.message ?? 'Google sign-in failed.';
		}
		// Success path redirects via Google; no further action required.
	}
</script>

<svelte:head>
	<title>Sign In · Simple Meal Planner</title>
</svelte:head>

<div class="mx-auto flex min-h-screen max-w-md items-center justify-center px-6 py-12">
	<div class="w-full rounded-2xl border border-green-100 bg-white p-8 shadow-lg">
		<h1 class="font-display text-2xl font-bold text-green-950">Welcome back</h1>
		<p class="mt-1 text-sm text-green-800/70">
			Sign in to manage your meal plans, recipes, and grocery lists.
		</p>

		<button
			type="button"
			onclick={onGoogle}
			disabled={submitting}
			class="mt-6 flex w-full items-center justify-center gap-2 rounded-lg border border-green-200 bg-white px-4 py-2.5 font-display text-sm font-medium text-green-900 transition hover:bg-green-50 disabled:opacity-60"
		>
			<svg class="h-4 w-4" viewBox="0 0 48 48" aria-hidden="true">
				<path
					fill="#FFC107"
					d="M43.6 20.5H42V20H24v8h11.3c-1.6 4.6-6 8-11.3 8a12 12 0 1 1 0-24c3 0 5.8 1.1 8 3l5.6-5.6A20 20 0 1 0 44 24c0-1.2-.1-2.4-.4-3.5z"
				/>
				<path
					fill="#FF3D00"
					d="m6.3 14.7 6.6 4.8A12 12 0 0 1 24 12c3 0 5.8 1.1 8 3l5.6-5.6A20 20 0 0 0 6.3 14.7z"
				/>
				<path
					fill="#4CAF50"
					d="M24 44c5.2 0 9.9-2 13.4-5.2l-6.2-5.2a12 12 0 0 1-19-5.5l-6.6 5.1A20 20 0 0 0 24 44z"
				/>
				<path
					fill="#1976D2"
					d="M43.6 20.5H42V20H24v8h11.3a12 12 0 0 1-4.1 5.6l6.2 5.2c-.4.4 6.6-4.8 6.6-14.8 0-1.2-.1-2.4-.4-3.5z"
				/>
			</svg>
			Continue with Google
		</button>

		<div class="my-6 flex items-center gap-3">
			<span class="h-px flex-1 bg-green-100"></span>
			<span class="text-xs font-medium tracking-wide text-green-700/60 uppercase">or</span>
			<span class="h-px flex-1 bg-green-100"></span>
		</div>

		<form class="space-y-4" onsubmit={onSubmit}>
			<label class="block text-sm font-medium text-green-900">
				Email
				<input
					type="email"
					required
					autocomplete="email"
					bind:value={email}
					class="mt-1 block w-full rounded-lg border border-green-200 px-3 py-2 text-sm focus:border-green-500 focus:ring-1 focus:ring-green-500 focus:outline-none"
				/>
			</label>
			<label class="block text-sm font-medium text-green-900">
				Password
				<input
					type="password"
					required
					autocomplete="current-password"
					bind:value={password}
					class="mt-1 block w-full rounded-lg border border-green-200 px-3 py-2 text-sm focus:border-green-500 focus:ring-1 focus:ring-green-500 focus:outline-none"
				/>
			</label>

			{#if error}
				<p class="text-sm text-red-600" role="alert">{error}</p>
			{/if}

			<button
				type="submit"
				disabled={submitting}
				class="w-full rounded-lg bg-green-600 px-4 py-2.5 font-display text-sm font-semibold text-white shadow transition hover:bg-green-500 disabled:opacity-60"
			>
				{submitting ? 'Signing in…' : 'Sign In'}
			</button>
		</form>

		<p class="mt-6 text-center text-sm text-green-800/80">
			New here?
			<a class="font-semibold text-green-700 hover:text-green-600" href={resolve('/auth/signup')}>
				Create an account
			</a>
		</p>
	</div>
</div>

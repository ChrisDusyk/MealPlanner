<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { authClient } from '$lib/auth/client';

	let name = $state('');
	let email = $state('');
	let password = $state('');
	let submitting = $state(false);
	let error = $state<string | null>(null);

	async function onSubmit(event: SubmitEvent) {
		event.preventDefault();
		if (submitting) return;
		submitting = true;
		error = null;

		const result = await authClient.signUp.email({
			name: name.trim(),
			email: email.trim(),
			password,
			callbackURL: resolve('/app/onboarding')
		});

		submitting = false;
		if (result.error) {
			error = result.error.message ?? 'Unable to create your account.';
			return;
		}
		await goto(resolve('/app/onboarding'));
	}

	async function onGoogle() {
		if (submitting) return;
		submitting = true;
		error = null;
		const result = await authClient.signIn.social({
			provider: 'google',
			callbackURL: resolve('/app/onboarding')
		});
		if (result.error) {
			submitting = false;
			error = result.error.message ?? 'Google sign-up failed.';
		}
	}
</script>

<svelte:head>
	<title>Create Account · Simple Meal Planner</title>
</svelte:head>

<div class="relative min-h-screen overflow-hidden bg-green-950">
	<div class="absolute inset-0 bg-[radial-gradient(circle_at_top_left,#65a30dcc,transparent_55%)]"></div>
	<div class="absolute inset-0 bg-[radial-gradient(circle_at_bottom_right,#14532dcc,transparent_55%)]"></div>

	<div class="relative mx-auto flex min-h-screen max-w-6xl items-center px-6 py-12 lg:py-16">
		<div class="grid w-full gap-8 lg:grid-cols-[1.1fr_0.9fr]">
			<section
				class="rounded-3xl border border-green-500/20 bg-green-900/60 p-8 shadow-2xl shadow-black/30 backdrop-blur-sm lg:p-10"
			>
				<span
					class="inline-flex items-center rounded-full border border-green-400/30 bg-green-800/60 px-3 py-1 font-display text-[11px] font-semibold tracking-widest text-green-300 uppercase"
				>
					Get More From Meal Planning
				</span>
				<h1 class="mt-5 font-display text-3xl leading-tight font-bold text-white lg:text-4xl">
					Plan meals, generate grocery lists, and share your week with others.
				</h1>
				<p class="mt-4 max-w-xl text-base leading-relaxed text-green-100/80">
					Simple Meal Planner keeps your recipes, meal calendar, and shopping prep in one place so
					you can coordinate faster and spend less time figuring out what is for dinner.
				</p>

				<div class="mt-8 grid gap-4 sm:grid-cols-3">
					<article class="rounded-2xl border border-green-400/20 bg-green-800/40 p-4">
						<h2 class="font-display text-sm font-semibold text-white">Weekly Meal Plans</h2>
						<p class="mt-2 text-sm leading-relaxed text-green-100/75">
							Build your week once, reuse recipes, and stay organized day by day.
						</p>
					</article>
					<article class="rounded-2xl border border-green-400/20 bg-green-800/40 p-4">
						<h2 class="font-display text-sm font-semibold text-white">Generated Grocery Lists</h2>
						<p class="mt-2 text-sm leading-relaxed text-green-100/75">
							Create a consolidated shopping list directly from your planned meals.
						</p>
					</article>
					<article class="rounded-2xl border border-green-400/20 bg-green-800/40 p-4">
						<h2 class="font-display text-sm font-semibold text-white">Share With Others</h2>
						<p class="mt-2 text-sm leading-relaxed text-green-100/75">
							Share meal plans and grocery prep so everyone stays aligned.
						</p>
					</article>
				</div>

				<ul class="mt-7 space-y-3 text-sm text-green-100/80">
					<li class="flex items-start gap-2.5">
						<span class="mt-1.5 h-2 w-2 rounded-full bg-green-300"></span>
						<span>Import and organize favorite recipes so meal planning stays fast.</span>
					</li>
					<li class="flex items-start gap-2.5">
						<span class="mt-1.5 h-2 w-2 rounded-full bg-green-300"></span>
						<span>Turn planned meals into shopping tasks with one click.</span>
					</li>
					<li class="flex items-start gap-2.5">
						<span class="mt-1.5 h-2 w-2 rounded-full bg-green-300"></span>
						<span>Coordinate shared plans and lists across family or roommates.</span>
					</li>
				</ul>
			</section>

			<div class="w-full rounded-2xl border border-green-100 bg-white p-8 shadow-lg">
				<h2 class="font-display text-2xl font-bold text-green-950">Create your account</h2>
				<p class="mt-1 text-sm text-green-800/70">Start free and set up your first plan in minutes.</p>

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
						Name
						<input
							type="text"
							required
							autocomplete="name"
							bind:value={name}
							class="mt-1 block w-full rounded-lg border border-green-200 px-3 py-2 text-sm focus:border-green-500 focus:ring-1 focus:ring-green-500 focus:outline-none"
						/>
					</label>
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
							minlength="8"
							autocomplete="new-password"
							bind:value={password}
							class="mt-1 block w-full rounded-lg border border-green-200 px-3 py-2 text-sm focus:border-green-500 focus:ring-1 focus:ring-green-500 focus:outline-none"
						/>
						<span class="mt-1 block text-xs text-green-700/60">Minimum 8 characters.</span>
					</label>

					{#if error}
						<p class="text-sm text-red-600" role="alert">{error}</p>
					{/if}

					<button
						type="submit"
						disabled={submitting}
						class="w-full rounded-lg bg-green-600 px-4 py-2.5 font-display text-sm font-semibold text-white shadow transition hover:bg-green-500 disabled:opacity-60"
					>
						{submitting ? 'Creating account…' : 'Create Account'}
					</button>
				</form>

				<p class="mt-6 text-center text-sm text-green-800/80">
					Already have an account?
					<a class="font-semibold text-green-700 hover:text-green-600" href={resolve('/auth/signin')}>
						Sign in
					</a>
				</p>
			</div>
		</div>
	</div>
</div>

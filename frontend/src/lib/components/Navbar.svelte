<script lang="ts">
	import { signIn, signOut } from '@auth/sveltekit/client';
	import type { Session } from '@auth/sveltekit';

	let { session }: { session: Session | null } = $props();

	let scrolled = $state(false);
	let mobileOpen = $state(false);

	function handleScroll() {
		scrolled = window.scrollY > 10;
	}

	function handleLogin() {
		signIn('keycloak');
	}

	function handleLogout() {
		signOut();
	}
</script>

<svelte:window onscroll={handleScroll} />

<nav
	class="fixed top-0 right-0 left-0 z-50 transition-all duration-300 {scrolled
		? 'bg-green-900/95 shadow-lg shadow-green-950/20 backdrop-blur-md'
		: 'bg-green-900/80 backdrop-blur-sm'}"
>
	<div class="mx-auto flex max-w-7xl items-center justify-between px-6 py-4">
		<!-- Logo + Nav Links -->
		<div class="flex items-center gap-10">
			<!-- Logo -->
			<a href="/" class="group flex items-center gap-2.5">
				<div
					class="flex h-9 w-9 items-center justify-center rounded-xl bg-green-500 transition-transform duration-300 group-hover:scale-110"
				>
					<svg
						xmlns="http://www.w3.org/2000/svg"
						viewBox="0 0 24 24"
						fill="none"
						stroke="currentColor"
						stroke-width="2"
						stroke-linecap="round"
						stroke-linejoin="round"
						class="h-5 w-5 text-white"
					>
						<path d="M17 8c.7-1 1-2.2 1-3.5C18 2.5 16.5 1 14.5 1S11 2.5 11 4.5c0 1.3.3 2.5 1 3.5" />
						<path d="M7 8c-.7-1-1-2.2-1-3.5C6 2.5 7.5 1 9.5 1S13 2.5 13 4.5c0 1.3-.3 2.5-1 3.5" />
						<path d="M12 8a5 5 0 0 0-4.8 3.6L5 20h14l-2.2-8.4A5 5 0 0 0 12 8Z" />
						<path d="M12 8v2" />
					</svg>
				</div>
				<span class="font-display text-xl font-bold tracking-tight text-white"> MealPlanner </span>
			</a>

			<!-- Desktop Nav -->
			<div class="hidden items-center gap-8 md:flex">
				<a
					href="#features"
					class="font-display text-sm font-medium tracking-wide text-green-200/80 transition-colors hover:text-white"
				>
					Features
				</a>
				<a
					href="#how-it-works"
					class="font-display text-sm font-medium tracking-wide text-green-200/80 transition-colors hover:text-white"
				>
					How It Works
				</a>
				<a
					href="#testimonials"
					class="font-display text-sm font-medium tracking-wide text-green-200/80 transition-colors hover:text-white"
				>
					Testimonials
				</a>
			</div>
		</div>

		<!-- Desktop Right Buttons -->
		<div class="hidden items-center gap-3 md:flex">
			{#if session?.user}
				<span class="font-display text-sm font-medium text-green-200/80">
					{session.user.name ?? session.user.email}
				</span>
				<button
					onclick={handleLogout}
					class="rounded-lg border border-green-400/30 px-5 py-2 font-display text-sm font-medium text-green-100 transition-all hover:border-green-400/60 hover:bg-green-800/50"
				>
					Log Out
				</button>
			{:else}
				<button
					onclick={handleLogin}
					class="rounded-lg border border-green-400/30 px-5 py-2 font-display text-sm font-medium text-green-100 transition-all hover:border-green-400/60 hover:bg-green-800/50"
				>
					Log In
				</button>
				<button
					onclick={handleLogin}
					class="rounded-lg bg-green-500 px-5 py-2 font-display text-sm font-semibold text-white shadow-md shadow-green-900/30 transition-all hover:bg-green-400 hover:shadow-lg hover:shadow-green-900/40"
				>
					Get Started
				</button>
			{/if}
		</div>

		<!-- Mobile Hamburger -->
		<button
			class="flex h-10 w-10 items-center justify-center rounded-lg text-green-200 transition-colors hover:bg-green-800/50 md:hidden"
			onclick={() => (mobileOpen = !mobileOpen)}
			aria-label="Toggle menu"
		>
			{#if mobileOpen}
				<svg
					xmlns="http://www.w3.org/2000/svg"
					class="h-6 w-6"
					fill="none"
					viewBox="0 0 24 24"
					stroke="currentColor"
					stroke-width="2"
				>
					<path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
				</svg>
			{:else}
				<svg
					xmlns="http://www.w3.org/2000/svg"
					class="h-6 w-6"
					fill="none"
					viewBox="0 0 24 24"
					stroke="currentColor"
					stroke-width="2"
				>
					<path stroke-linecap="round" stroke-linejoin="round" d="M4 6h16M4 12h16M4 18h16" />
				</svg>
			{/if}
		</button>
	</div>

	<!-- Mobile Menu -->
	{#if mobileOpen}
		<div
			class="border-t border-green-700/40 bg-green-900/95 px-6 pt-4 pb-6 backdrop-blur-md md:hidden"
		>
			<div class="flex flex-col gap-3">
				<a
					href="#features"
					class="rounded-lg px-3 py-2.5 font-display text-sm font-medium text-green-200/90 transition-colors hover:bg-green-800/50 hover:text-white"
					onclick={() => (mobileOpen = false)}
				>
					Features
				</a>
				<a
					href="#how-it-works"
					class="rounded-lg px-3 py-2.5 font-display text-sm font-medium text-green-200/90 transition-colors hover:bg-green-800/50 hover:text-white"
					onclick={() => (mobileOpen = false)}
				>
					How It Works
				</a>
				<a
					href="#testimonials"
					class="rounded-lg px-3 py-2.5 font-display text-sm font-medium text-green-200/90 transition-colors hover:bg-green-800/50 hover:text-white"
					onclick={() => (mobileOpen = false)}
				>
					Testimonials
				</a>
				<hr class="border-green-700/40" />
				{#if session?.user}
					<span
						class="rounded-lg px-3 py-2.5 font-display text-sm font-medium text-green-200/90"
					>
						{session.user.name ?? session.user.email}
					</span>
					<button
						onclick={() => {
							mobileOpen = false;
							handleLogout();
						}}
						class="rounded-lg border border-green-400/30 px-3 py-2.5 text-center font-display text-sm font-medium text-green-100 transition-all hover:border-green-400/60 hover:bg-green-800/50"
					>
						Log Out
					</button>
				{:else}
					<button
						onclick={() => {
							mobileOpen = false;
							handleLogin();
						}}
						class="rounded-lg border border-green-400/30 px-3 py-2.5 text-center font-display text-sm font-medium text-green-100 transition-all hover:border-green-400/60 hover:bg-green-800/50"
					>
						Log In
					</button>
					<button
						onclick={() => {
							mobileOpen = false;
							handleLogin();
						}}
						class="rounded-lg bg-green-500 px-3 py-2.5 text-center font-display text-sm font-semibold text-white shadow-md transition-all hover:bg-green-400"
					>
						Get Started
					</button>
				{/if}
			</div>
		</div>
	{/if}
</nav>

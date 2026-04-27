<script lang="ts">
	import { getContext } from 'svelte';
	import { goto, invalidateAll } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { authClient } from '$lib/auth/client';
	import type { AppSession } from '$lib/auth/session';
	import { APP_USER_CONTEXT_KEY, type AppUserContextValue } from '$lib/context/appUserContext';
	import { APP_ROLES, hasRole } from '$lib/auth/roles';

	let { session }: { session: AppSession | null } = $props();
	const appUserContext = getContext<AppUserContextValue | undefined>(APP_USER_CONTEXT_KEY);

	let displayName = $derived(
		appUserContext?.appUserState.current?.name?.trim() ||
			session?.user?.name?.trim() ||
			session?.user?.email ||
			'User'
	);

	let scrolled = $state(false);
	let mobileOpen = $state(false);
	let dropdownOpen = $state(false);
	let isAdmin = $derived(hasRole(session?.roles, APP_ROLES.admin));

	function handleScroll() {
		scrolled = window.scrollY > 10;
	}

	function handleLogin() {
		void goto(resolve('/auth/signin'));
	}

	function handleSignup() {
		void goto(resolve('/auth/signup'));
	}

	async function handleLogout() {
		dropdownOpen = false;
		await authClient.signOut();
		await invalidateAll();
		await goto(resolve('/'));
	}

	function toggleDropdown() {
		dropdownOpen = !dropdownOpen;
	}

	function closeDropdown() {
		dropdownOpen = false;
	}

	function handleKeydown(event: KeyboardEvent) {
		if (event.key === 'Escape' && dropdownOpen) {
			closeDropdown();
		}
	}
</script>

<svelte:window onscroll={handleScroll} />
<svelte:body onkeydown={handleKeydown} />

<nav
	aria-label="Primary"
	class="fixed top-0 right-0 left-0 z-50 transition-all duration-300 {scrolled
		? 'bg-green-900/95 shadow-lg shadow-green-950/20 backdrop-blur-md'
		: 'bg-green-900/80 backdrop-blur-sm'}"
>
	<div class="mx-auto flex max-w-7xl items-center justify-between px-6 py-4">
		<!-- Logo + Nav Links -->
		<div class="flex items-center gap-10">
			<!-- Logo -->
			<a href={resolve('/')} class="group flex items-center gap-2.5">
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
				<span class="font-display text-xl font-bold tracking-tight text-white">
					Simple Meal Planner
				</span>
			</a>

			<!-- Desktop Nav -->
			<div class="hidden items-center gap-8 md:flex">
				<a
					href="#features"
					class="font-display text-sm font-medium tracking-wide text-green-200/95 transition-colors hover:text-white"
				>
					Features
				</a>
				<a
					href="#how-it-works"
					class="font-display text-sm font-medium tracking-wide text-green-200/95 transition-colors hover:text-white"
				>
					How It Works
				</a>
				<a
					href="#testimonials"
					class="font-display text-sm font-medium tracking-wide text-green-200/95 transition-colors hover:text-white"
				>
					Testimonials
				</a>
			</div>
		</div>

		<!-- Desktop Right Buttons -->
		<div class="hidden items-center gap-3 md:flex">
			{#if session?.user}
				<!-- User dropdown -->
				<div class="relative">
					<button
						type="button"
						onclick={toggleDropdown}
						class="flex items-center gap-2 rounded-lg px-3 py-2 font-display text-sm font-medium text-green-200/95 transition-all hover:bg-green-800/50 hover:text-white"
						aria-expanded={dropdownOpen}
						aria-haspopup="true"
						aria-controls="user-menu"
					>
						<span>{displayName}</span>
						<svg
							xmlns="http://www.w3.org/2000/svg"
							class="h-4 w-4 transition-transform duration-200 {dropdownOpen ? 'rotate-180' : ''}"
							fill="none"
							viewBox="0 0 24 24"
							stroke="currentColor"
							stroke-width="2"
						>
							<path stroke-linecap="round" stroke-linejoin="round" d="M19 9l-7 7-7-7" />
						</svg>
					</button>

					<!-- Dropdown menu -->
					{#if dropdownOpen}
						<div
							id="user-menu"
							class="absolute top-full right-0 mt-2 w-56 origin-top-right overflow-hidden rounded-lg border border-green-700/40 bg-green-900/95 shadow-lg shadow-green-950/20 backdrop-blur-md"
							aria-label="User menu"
						>
							<ul class="py-1">
								<li>
									<a
										href={resolve('/app')}
										onclick={closeDropdown}
										class="flex items-center gap-3 px-4 py-2.5 font-display text-sm font-medium text-green-200/80 transition-colors hover:bg-green-800/50 hover:text-white"
									>
										<svg
											xmlns="http://www.w3.org/2000/svg"
											class="h-4 w-4"
											fill="none"
											viewBox="0 0 24 24"
											stroke="currentColor"
											stroke-width="2"
										>
											<path
												stroke-linecap="round"
												stroke-linejoin="round"
												d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6"
											/>
										</svg>
										Dashboard
									</a>
								</li>
								<li>
									<a
										href={resolve('/app/account')}
										onclick={closeDropdown}
										class="flex items-center gap-3 px-4 py-2.5 font-display text-sm font-medium text-green-200/80 transition-colors hover:bg-green-800/50 hover:text-white"
									>
										<svg
											xmlns="http://www.w3.org/2000/svg"
											class="h-4 w-4"
											fill="none"
											viewBox="0 0 24 24"
											stroke="currentColor"
											stroke-width="2"
										>
											<path
												stroke-linecap="round"
												stroke-linejoin="round"
												d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"
											/>
										</svg>
										Account
									</a>
								</li>
								{#if isAdmin}
									<li>
										<a
											href={resolve('/app/admin')}
											onclick={closeDropdown}
											class="flex items-center gap-3 px-4 py-2.5 font-display text-sm font-medium text-green-200/80 transition-colors hover:bg-green-800/50 hover:text-white"
										>
											<svg
												xmlns="http://www.w3.org/2000/svg"
												class="h-4 w-4"
												fill="none"
												viewBox="0 0 24 24"
												stroke="currentColor"
												stroke-width="2"
											>
												<path
													stroke-linecap="round"
													stroke-linejoin="round"
													d="M12 3l7.5 4.5v4.5c0 5.1-3.3 8.8-7.5 10.5C7.8 20.8 4.5 17.1 4.5 12V7.5L12 3z"
												/>
											</svg>
											Admin
										</a>
									</li>
								{/if}
								<hr class="my-1 border-green-700/40" />
								<li>
									<button
										type="button"
										onclick={handleLogout}
										class="flex w-full items-center gap-3 px-4 py-2.5 text-left font-display text-sm font-medium text-green-200/80 transition-colors hover:bg-green-800/50 hover:text-white"
									>
										<svg
											xmlns="http://www.w3.org/2000/svg"
											class="h-4 w-4"
											fill="none"
											viewBox="0 0 24 24"
											stroke="currentColor"
											stroke-width="2"
										>
											<path
												stroke-linecap="round"
												stroke-linejoin="round"
												d="M15.75 9V5.25A2.25 2.25 0 0 0 13.5 3h-6a2.25 2.25 0 0 0-2.25 2.25v13.5A2.25 2.25 0 0 0 7.5 21h6a2.25 2.25 0 0 0 2.25-2.25V15m3 0 3-3m0 0-3-3m3 3H9"
											/>
										</svg>
										Log Out
									</button>
								</li>
							</ul>
						</div>

						<!-- Click outside handler -->
						<button
							type="button"
							class="fixed inset-0 z-40"
							onclick={closeDropdown}
							aria-label="Close user menu"
							tabindex="-1"
						></button>
					{/if}
				</div>
			{:else}
				<button
					type="button"
					onclick={handleLogin}
					class="rounded-lg border border-green-400/30 px-5 py-2 font-display text-sm font-medium text-green-100 transition-all hover:border-green-400/60 hover:bg-green-800/50"
				>
					Log In
				</button>
				<button
					type="button"
					onclick={handleSignup}
					class="rounded-lg bg-green-500 px-5 py-2 font-display text-sm font-semibold text-white shadow-md shadow-green-900/30 transition-all hover:bg-green-400 hover:shadow-lg hover:shadow-green-900/40"
				>
					Get Started
				</button>
			{/if}
		</div>

		<!-- Mobile Hamburger -->
		<button
			type="button"
			class="flex h-10 w-10 items-center justify-center rounded-lg text-green-200 transition-colors hover:bg-green-800/50 md:hidden"
			onclick={() => (mobileOpen = !mobileOpen)}
			aria-label="Toggle menu"
			aria-expanded={mobileOpen}
			aria-controls="mobile-menu"
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
			id="mobile-menu"
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
					<span class="rounded-lg px-3 py-2.5 font-display text-sm font-medium text-green-200/90">
						{displayName}
					</span>
					<a
						href={resolve('/app')}
						onclick={() => (mobileOpen = false)}
						class="flex items-center gap-3 rounded-lg px-3 py-2.5 font-display text-sm font-medium text-green-200/90 transition-colors hover:bg-green-800/50 hover:text-white"
					>
						<svg
							xmlns="http://www.w3.org/2000/svg"
							class="h-4 w-4"
							fill="none"
							viewBox="0 0 24 24"
							stroke="currentColor"
							stroke-width="2"
						>
							<path
								stroke-linecap="round"
								stroke-linejoin="round"
								d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6"
							/>
						</svg>
						Dashboard
					</a>
					<a
						href={resolve('/app/account')}
						onclick={() => (mobileOpen = false)}
						class="flex items-center gap-3 rounded-lg px-3 py-2.5 font-display text-sm font-medium text-green-200/90 transition-colors hover:bg-green-800/50 hover:text-white"
					>
						<svg
							xmlns="http://www.w3.org/2000/svg"
							class="h-4 w-4"
							fill="none"
							viewBox="0 0 24 24"
							stroke="currentColor"
							stroke-width="2"
						>
							<path
								stroke-linecap="round"
								stroke-linejoin="round"
								d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"
							/>
						</svg>
						Account
					</a>
					{#if isAdmin}
						<a
							href={resolve('/app/admin')}
							onclick={() => (mobileOpen = false)}
							class="flex items-center gap-3 rounded-lg px-3 py-2.5 font-display text-sm font-medium text-green-200/90 transition-colors hover:bg-green-800/50 hover:text-white"
						>
							<svg
								xmlns="http://www.w3.org/2000/svg"
								class="h-4 w-4"
								fill="none"
								viewBox="0 0 24 24"
								stroke="currentColor"
								stroke-width="2"
							>
								<path
									stroke-linecap="round"
									stroke-linejoin="round"
									d="M12 3l7.5 4.5v4.5c0 5.1-3.3 8.8-7.5 10.5C7.8 20.8 4.5 17.1 4.5 12V7.5L12 3z"
								/>
							</svg>
							Admin
						</a>
					{/if}
					<button
						type="button"
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
						type="button"
						onclick={() => {
							mobileOpen = false;
							handleLogin();
						}}
						class="rounded-lg border border-green-400/30 px-3 py-2.5 text-center font-display text-sm font-medium text-green-100 transition-all hover:border-green-400/60 hover:bg-green-800/50"
					>
						Log In
					</button>
					<button
						type="button"
						onclick={() => {
							mobileOpen = false;
							handleSignup();
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

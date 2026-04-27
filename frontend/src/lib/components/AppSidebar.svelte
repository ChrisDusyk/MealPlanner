<script lang="ts">
	import { page } from '$app/state';
	import { goto, invalidateAll } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { getContext } from 'svelte';
	import { authClient } from '$lib/auth/client';
	import type { AppSession } from '$lib/auth/session';
	import { APP_USER_CONTEXT_KEY, type AppUserContextValue } from '$lib/context/appUserContext';

	let { session }: { session: AppSession | null } = $props();
	const appUserContext = getContext<AppUserContextValue | undefined>(APP_USER_CONTEXT_KEY);

	let displayName = $derived(
		appUserContext?.appUserState.current?.name?.trim() ||
			session?.user?.name?.trim() ||
			session?.user?.email ||
			'User'
	);
	let displayEmail = $derived(
		appUserContext?.appUserState.current?.email || session?.user?.email || ''
	);
	let displayInitial = $derived(displayName.charAt(0).toUpperCase());

	let mobileOpen = $state(false);

	const navItems = [
		{
			label: 'Dashboard',
			href: '/app',
			icon: 'dashboard'
		},
		{
			label: 'Meal Plans',
			href: '/app/meal-plans',
			icon: 'calendar'
		},
		{
			label: 'Recipes',
			href: '/app/recipes',
			icon: 'recipe'
		},
		{
			label: 'Grocery Lists',
			href: '/app/grocery-lists',
			icon: 'grocery'
		}
	] as const;

	function isActive(href: string): boolean {
		if (href === '/app') {
			return page.url.pathname === href;
		}
		return page.url.pathname.startsWith(href);
	}

	async function handleLogout() {
		await authClient.signOut();
		await invalidateAll();
		await goto(resolve('/'));
	}
</script>

<!-- Mobile sidebar toggle -->
<button
	type="button"
	class="fixed top-[78px] left-4 z-40 flex h-10 w-10 items-center justify-center rounded-lg bg-green-900 text-green-200 shadow-lg transition-colors hover:bg-green-800 md:hidden"
	onclick={() => (mobileOpen = !mobileOpen)}
	aria-label="Toggle sidebar"
	aria-expanded={mobileOpen}
	aria-controls="app-sidebar"
>
	<svg
		xmlns="http://www.w3.org/2000/svg"
		class="h-5 w-5"
		fill="none"
		viewBox="0 0 24 24"
		stroke="currentColor"
		stroke-width="2"
	>
		{#if mobileOpen}
			<path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
		{:else}
			<path stroke-linecap="round" stroke-linejoin="round" d="M4 6h16M4 12h8" />
		{/if}
	</svg>
</button>

<!-- Mobile backdrop -->
{#if mobileOpen}
	<button
		type="button"
		class="fixed inset-0 z-30 bg-black/40 backdrop-blur-sm md:hidden"
		onclick={() => (mobileOpen = false)}
		aria-label="Close sidebar"
		tabindex="-1"
	></button>
{/if}

<!-- Sidebar -->
<aside
	id="app-sidebar"
	aria-label="Application sidebar"
	class="fixed top-[72px] bottom-0 left-0 z-40 flex w-64 flex-col border-r border-green-800/30 bg-green-900 transition-transform duration-300 {mobileOpen
		? 'translate-x-0'
		: '-translate-x-full'} md:translate-x-0"
>
	<!-- User section -->
	{#if session?.user}
		<div class="border-b border-green-800/40 px-5 py-5">
			<div class="flex items-center gap-3">
				<div
					class="flex h-10 w-10 items-center justify-center rounded-full bg-green-500 font-display text-sm font-bold text-white"
				>
					{displayInitial}
				</div>
				<div class="min-w-0 flex-1">
					<p class="truncate font-display text-sm font-semibold text-white">
						{displayName}
					</p>
					{#if displayEmail}
						<p class="truncate text-xs text-green-300/90">
							{displayEmail}
						</p>
					{/if}
				</div>
			</div>
		</div>
	{/if}

	<!-- Navigation -->
	<nav class="flex-1 overflow-y-auto px-3 py-4" aria-label="Application">
		<ul class="flex flex-col gap-1">
			{#each navItems as item (item.href)}
				<li>
					<a
						href={resolve(item.href)}
						onclick={() => (mobileOpen = false)}
						class="group flex items-center gap-3 rounded-lg px-3 py-2.5 font-display text-sm font-medium transition-all {isActive(
							item.href
						)
							? 'bg-green-500/20 text-white'
							: 'text-green-200/90 hover:bg-green-800/50 hover:text-white'}"
					>
						<!-- Dashboard icon -->
						{#if item.icon === 'dashboard'}
							<svg
								xmlns="http://www.w3.org/2000/svg"
								class="h-5 w-5 shrink-0 transition-colors {isActive(item.href)
									? 'text-green-400'
									: 'text-green-400/70 group-hover:text-green-300'}"
								fill="none"
								viewBox="0 0 24 24"
								stroke="currentColor"
								stroke-width="1.5"
							>
								<path
									stroke-linecap="round"
									stroke-linejoin="round"
									d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6"
								/>
							</svg>
							<!-- Calendar icon for Meal Plans -->
						{:else if item.icon === 'calendar'}
							<svg
								xmlns="http://www.w3.org/2000/svg"
								class="h-5 w-5 shrink-0 transition-colors {isActive(item.href)
									? 'text-green-400'
									: 'text-green-400/70 group-hover:text-green-300'}"
								fill="none"
								viewBox="0 0 24 24"
								stroke="currentColor"
								stroke-width="1.5"
							>
								<path
									stroke-linecap="round"
									stroke-linejoin="round"
									d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5m-9-6h.008v.008H12v-.008ZM12 15h.008v.008H12V15Zm0 2.25h.008v.008H12v-.008ZM9.75 15h.008v.008H9.75V15Zm0 2.25h.008v.008H9.75v-.008ZM7.5 15h.008v.008H7.5V15Zm0 2.25h.008v.008H7.5v-.008Zm6.75-4.5h.008v.008h-.008v-.008Zm0 2.25h.008v.008h-.008V15Zm0 2.25h.008v.008h-.008v-.008Zm2.25-4.5h.008v.008H16.5v-.008Zm0 2.25h.008v.008H16.5V15Z"
								/>
							</svg>
							<!-- Recipe book icon -->
						{:else if item.icon === 'recipe'}
							<svg
								xmlns="http://www.w3.org/2000/svg"
								class="h-5 w-5 shrink-0 transition-colors {isActive(item.href)
									? 'text-green-400'
									: 'text-green-400/70 group-hover:text-green-300'}"
								fill="none"
								viewBox="0 0 24 24"
								stroke="currentColor"
								stroke-width="1.5"
							>
								<path
									stroke-linecap="round"
									stroke-linejoin="round"
									d="M12 6.042A8.967 8.967 0 0 0 6 3.75c-1.052 0-2.062.18-3 .512v14.25A8.987 8.987 0 0 1 6 18c2.305 0 4.408.867 6 2.292m0-14.25a8.966 8.966 0 0 1 6-2.292c1.052 0 2.062.18 3 .512v14.25A8.987 8.987 0 0 0 18 18a8.967 8.967 0 0 0-6 2.292m0-14.25v14.25"
								/>
							</svg>
							<!-- Shopping cart icon for Grocery Lists -->
						{:else if item.icon === 'grocery'}
							<svg
								xmlns="http://www.w3.org/2000/svg"
								class="h-5 w-5 shrink-0 transition-colors {isActive(item.href)
									? 'text-green-400'
									: 'text-green-400/70 group-hover:text-green-300'}"
								fill="none"
								viewBox="0 0 24 24"
								stroke="currentColor"
								stroke-width="1.5"
							>
								<path
									stroke-linecap="round"
									stroke-linejoin="round"
									d="M2.25 3h1.386c.51 0 .955.343 1.087.835l.383 1.437M7.5 14.25a3 3 0 0 0-3 3h15.75m-12.75-3h11.218c1.121-2.3 2.1-4.684 2.924-7.138a60.114 60.114 0 0 0-16.536-1.84M7.5 14.25 5.106 5.272M6 20.25a.75.75 0 1 1-1.5 0 .75.75 0 0 1 1.5 0Zm12.75 0a.75.75 0 1 1-1.5 0 .75.75 0 0 1 1.5 0Z"
								/>
							</svg>
						{/if}
						{item.label}
						{#if isActive(item.href)}
							<span class="ml-auto h-1.5 w-1.5 rounded-full bg-green-400" aria-hidden="true"></span>
						{/if}
					</a>
				</li>
			{/each}
		</ul>
	</nav>

	<!-- Bottom actions -->
	<div class="border-t border-green-800/40 px-3 py-4">
		<button
			type="button"
			onclick={handleLogout}
			class="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 font-display text-sm font-medium text-green-200/90 transition-all hover:bg-green-800/50 hover:text-white"
		>
			<svg
				xmlns="http://www.w3.org/2000/svg"
				class="h-5 w-5 shrink-0 text-green-400/70"
				fill="none"
				viewBox="0 0 24 24"
				stroke="currentColor"
				stroke-width="1.5"
			>
				<path
					stroke-linecap="round"
					stroke-linejoin="round"
					d="M15.75 9V5.25A2.25 2.25 0 0 0 13.5 3h-6a2.25 2.25 0 0 0-2.25 2.25v13.5A2.25 2.25 0 0 0 7.5 21h6a2.25 2.25 0 0 0 2.25-2.25V15m3 0 3-3m0 0-3-3m3 3H9"
				/>
			</svg>
			Log Out
		</button>
	</div>
</aside>

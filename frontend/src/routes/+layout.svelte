<script lang="ts">
	import './layout.css';
	import { setContext } from 'svelte';
	import favicon from '$lib/assets/favicon.svg';
	import Navbar from '$lib/components/Navbar.svelte';
	import {
		APP_USER_CONTEXT_KEY,
		type AppUserContextValue,
		type AppUserState
	} from '$lib/context/appUserContext';

	let { children, data } = $props();

	// svelte-ignore state_referenced_locally
	let appUserState: AppUserState = $state({
		current: data.appUser ?? null
	});

	function setAppUser(user: AppUserState['current']) {
		appUserState.current = user;
	}

	$effect(() => {
		appUserState.current = data.appUser ?? null;
	});

	setContext<AppUserContextValue>(APP_USER_CONTEXT_KEY, {
		appUserState,
		setAppUser
	});
</script>

<svelte:head>
	<link rel="icon" href={favicon} />
	<title>Simple Meal Planner — Plan meals. Shop smarter. Eat better.</title>
	<meta
		name="description"
		content="Organize your weekly meals, discover recipes, and auto-generate grocery lists. Simple Meal Planner makes healthy eating effortless."
	/>
</svelte:head>

<a
	href="#main-content"
	class="sr-only z-[60] rounded-md bg-green-500 px-4 py-2 font-display text-sm font-semibold text-white focus:not-sr-only focus:fixed focus:top-3 focus:left-3"
>
	Skip to main content
</a>

<Navbar session={data.session} />
<main id="main-content" class="pt-[72px]" tabindex="-1">
	{@render children()}
</main>

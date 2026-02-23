<script lang="ts">
	import './layout.css';
	import favicon from '$lib/assets/favicon.svg';
	import Navbar from '$lib/components/Navbar.svelte';

	let { children, data } = $props();
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

<script lang="ts">
	import './layout.css';
	import favicon from '$lib/assets/favicon.svg';
	import Navbar from '$lib/components/Navbar.svelte';

	let { children } = $props();
</script>

<svelte:head>
	<link rel="icon" href={favicon} />
	<title>MealPlanner — Plan meals. Shop smarter. Eat better.</title>
	<meta
		name="description"
		content="Organize your weekly meals, discover recipes, and auto-generate grocery lists. MealPlanner makes healthy eating effortless."
	/>
</svelte:head>

<Navbar />
<main class="pt-[72px]">
	{@render children()}
</main>

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

<Navbar session={data.session} />
<main class="pt-[72px]">
	{@render children()}
</main>

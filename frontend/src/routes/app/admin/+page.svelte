<svelte:head>
	<title>Admin | Meal Planner</title>
</svelte:head>

<section class="admin-placeholder">
	<h1>Admin Area</h1>
	<p>This placeholder page verifies admin-only route protection end to end.</p>
</section>

<style>
	.admin-placeholder {
		display: grid;
		gap: 0.75rem;
		padding: 2rem 1.25rem;
	}

	h1 {
		margin: 0;
		font-size: 1.8rem;
	}

	p {
		margin: 0;
		max-width: 52ch;
		color: var(--text-muted, #555);
	}
</style>

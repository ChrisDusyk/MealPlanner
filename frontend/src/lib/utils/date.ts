/**
 * Formats a date string into a human-readable format (e.g., "Feb 17, 2026").
 */
export function formatDate(dateString: string): string {
	return new Date(dateString).toLocaleDateString('en-US', {
		month: 'short',
		day: 'numeric',
		year: 'numeric'
	});
}

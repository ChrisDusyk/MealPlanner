import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { toast } from './toast.svelte';

describe('toast store', () => {
	beforeEach(() => {
		vi.useFakeTimers();
		toast.dismiss();
	});

	afterEach(() => {
		vi.useRealTimers();
	});

	it('shows a success toast by default', () => {
		toast.show('Saved!');

		expect(toast.visible).toBe(true);
		expect(toast.message).toBe('Saved!');
		expect(toast.type).toBe('success');
	});

	it('hides the toast after the default duration', () => {
		toast.show('Saved!');

		vi.advanceTimersByTime(2999);
		expect(toast.visible).toBe(true);

		vi.advanceTimersByTime(1);
		expect(toast.visible).toBe(false);
	});

	it('respects a custom duration', () => {
		toast.show('Long-lived', 'success', 5000);

		vi.advanceTimersByTime(3000);
		expect(toast.visible).toBe(true);

		vi.advanceTimersByTime(2000);
		expect(toast.visible).toBe(false);
	});

	it('gives a second toast its full duration (timer-overwrite regression)', () => {
		toast.show('First');
		vi.advanceTimersByTime(1500);

		toast.show('Second');
		expect(toast.message).toBe('Second');

		// 2000ms after the second toast, the first toast's original 3000ms
		// deadline has passed — the second toast must still be visible.
		vi.advanceTimersByTime(2000);
		expect(toast.visible).toBe(true);

		vi.advanceTimersByTime(1000);
		expect(toast.visible).toBe(false);
	});

	it('sets the error type via the error helper', () => {
		toast.error('Something failed');

		expect(toast.visible).toBe(true);
		expect(toast.type).toBe('error');
	});

	it('dismiss hides the toast and cancels the pending timer', () => {
		toast.show('Going away');
		toast.dismiss();

		expect(toast.visible).toBe(false);

		// No stale timer should re-hide a subsequent toast prematurely.
		toast.show('Back again');
		vi.advanceTimersByTime(2999);
		expect(toast.visible).toBe(true);
	});
});

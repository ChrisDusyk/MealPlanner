import { describe, expect, it } from 'vitest';
import { load } from './+page.server';

describe('GET / load', () => {
	it('returns sessionExpired=true when redirected with session=expired', async () => {
		const result = await load({
			url: new URL('http://localhost/?session=expired')
		} as Parameters<typeof load>[0]);

		expect(result).toEqual({ sessionExpired: true });
	});

	it('returns sessionExpired=false for normal visits', async () => {
		const result = await load({
			url: new URL('http://localhost/')
		} as Parameters<typeof load>[0]);

		expect(result).toEqual({ sessionExpired: false });
	});
});

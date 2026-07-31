import { describe, expect, it } from 'vitest';
import {
	buildDefinition,
	describeResolvedValues,
	parseDefinition,
	parseVariantValue
} from './definition';

const BOOLEAN_DEFINITION = '{"variants":{"on":true,"off":false},"defaultVariant":"on"}';

describe('parseDefinition', () => {
	it('splits variants, default, and targeting into editor state', () => {
		const parsed = parseDefinition(
			'{"variants":{"on":true,"off":false},"defaultVariant":"on","targeting":{"if":[true,"on"]}}'
		);

		expect(parsed.variants).toEqual([
			{ name: 'on', valueJson: 'true' },
			{ name: 'off', valueJson: 'false' }
		]);
		expect(parsed.defaultVariant).toBe('on');
		expect(JSON.parse(parsed.targetingJson)).toEqual({ if: [true, 'on'] });
	});

	it('leaves targeting blank when the definition has none', () => {
		expect(parseDefinition(BOOLEAN_DEFINITION).targetingJson).toBe('');
	});

	it.each(['', '   ', 'not-json', '[1,2,3]', '"a string"'])(
		'degrades to empty state for %j rather than throwing',
		(definition) => {
			expect(parseDefinition(definition)).toEqual({
				variants: [],
				defaultVariant: '',
				targetingJson: ''
			});
		}
	);
});

describe('parseVariantValue', () => {
	it.each([
		['boolean', 'true', true],
		['string', '"beta"', 'beta'],
		['number', '2.5', 2.5],
		['object', '{"a":1}', { a: 1 }]
	] as const)('accepts a %s value', (valueType, json, expected) => {
		expect(parseVariantValue(json, valueType)).toEqual({ value: expected });
	});

	it.each([
		['boolean', '"yes"'],
		['string', '42'],
		['number', 'true'],
		['object', '"a"'],
		['object', 'null'],
		['boolean', ''],
		['number', 'not-json']
	] as const)('rejects %s value %j', (valueType, json) => {
		expect(parseVariantValue(json, valueType)).toHaveProperty('error');
	});

	it('nudges toward quotes when a string variant is unquoted', () => {
		const result = parseVariantValue('beta', 'string');

		expect(result).toEqual({ error: 'Enter a value in double quotes, for example "beta".' });
	});
});

describe('buildDefinition', () => {
	const input = {
		variants: [
			{ name: 'on', valueJson: 'true' },
			{ name: 'off', valueJson: 'false' }
		],
		defaultVariant: 'on',
		disabledVariant: 'off',
		targetingJson: '',
		valueType: 'boolean' as const
	};

	it('assembles a flagd body', () => {
		const result = buildDefinition(input);

		expect(result).toEqual({ definitionJson: BOOLEAN_DEFINITION });
	});

	it('round-trips through parseDefinition', () => {
		const result = buildDefinition(input);

		expect('definitionJson' in result).toBe(true);
		if (!('definitionJson' in result)) return;

		const parsed = parseDefinition(result.definitionJson);
		expect(parsed.variants).toEqual(input.variants);
		expect(parsed.defaultVariant).toBe('on');
	});

	it('includes targeting when supplied', () => {
		const result = buildDefinition({ ...input, targetingJson: '{"if":[true,"on"]}' });

		expect('definitionJson' in result).toBe(true);
		if (!('definitionJson' in result)) return;

		expect(JSON.parse(result.definitionJson).targeting).toEqual({ if: [true, 'on'] });
	});

	it('ignores unnamed variant rows', () => {
		const result = buildDefinition({
			...input,
			variants: [...input.variants, { name: '  ', valueJson: 'true' }]
		});

		expect(result).toEqual({ definitionJson: BOOLEAN_DEFINITION });
	});

	it.each([
		['no variants', { variants: [] }, 'Add at least one variant.'],
		[
			'a duplicate variant name',
			{
				variants: [
					{ name: 'on', valueJson: 'true' },
					{ name: 'on', valueJson: 'false' }
				]
			},
			"Variant 'on' is defined more than once."
		],
		[
			'a variant that does not match the value type',
			{ variants: [{ name: 'on', valueJson: '"yes"' }] },
			"Variant 'on': Expected true or false."
		],
		[
			'no default variant',
			{ defaultVariant: '' },
			'Choose the variant to serve while the flag is on.'
		],
		[
			'an unknown default variant',
			{ defaultVariant: 'ghost' },
			"The default variant 'ghost' is not one of the variants."
		],
		[
			'an unknown off variant',
			{ disabledVariant: 'ghost' },
			"The off variant 'ghost' is not one of the variants."
		],
		['invalid targeting JSON', { targetingJson: '{oops' }, 'Targeting rules must be valid JSON.'],
		['array targeting', { targetingJson: '[1,2]' }, 'Targeting rules must be a JSON object.']
	])('rejects %s', (_label, overrides, error) => {
		expect(buildDefinition({ ...input, ...overrides })).toEqual({ error });
	});
});

describe('describeResolvedValues', () => {
	it('reports the value served in each state', () => {
		expect(
			describeResolvedValues({ definitionJson: BOOLEAN_DEFINITION, disabledVariant: 'off' })
		).toEqual({ onValue: 'true', offValue: 'false' });
	});

	it('reports no off value when the flag falls through to the code default', () => {
		expect(
			describeResolvedValues({ definitionJson: BOOLEAN_DEFINITION, disabledVariant: null })
		).toEqual({ onValue: 'true', offValue: null });
	});

	it('reports no value for a variant the definition does not declare', () => {
		expect(
			describeResolvedValues({ definitionJson: '{"variants":{}}', disabledVariant: 'off' })
		).toEqual({ onValue: null, offValue: null });
	});
});

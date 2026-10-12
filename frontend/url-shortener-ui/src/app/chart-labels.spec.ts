import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { utcDayLabel } from './chart-labels';

// Runs the body with the process time zone set to a zone east of UTC, where a
// local-midnight conversion moves a date-only value to the previous UTC day.
describe('utcDayLabel', () => {
  const env = (globalThis as { process?: { env: Record<string, string | undefined> } }).process?.env ?? {};
  let originalZone: string | undefined;

  beforeEach(() => {
    originalZone = env['TZ'];
    env['TZ'] = 'Asia/Kolkata';
  });

  afterEach(() => {
    env['TZ'] = originalZone;
  });

  it('runs in a zone east of UTC so the regression check is meaningful', () => {
    // If the runtime ignores the TZ change, this fails instead of passing silently.
    expect(new Date('2026-10-12T00:00:00').getTimezoneOffset()).toBeLessThan(0);
  });

  it('labels the UTC day, not the previous day, for zones east of UTC', () => {
    expect(utcDayLabel('2026-10-12')).toBe('Oct 12');
  });

  it('formats single-digit days without a leading zero', () => {
    expect(utcDayLabel('2026-01-05')).toBe('Jan 5');
  });
});

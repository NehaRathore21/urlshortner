/**
 * Formats a UTC calendar day ("yyyy-MM-dd") as a short label such as "Oct 12".
 *
 * The date is built as midnight UTC and formatted in UTC. Passing the bare date
 * string to a local-time date conversion would shift it to the previous day for
 * users east of UTC.
 */
export function utcDayLabel(dateUtc: string): string {
  return new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', timeZone: 'UTC' })
    .format(new Date(`${dateUtc}T00:00:00Z`));
}

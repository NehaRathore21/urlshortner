CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

CREATE TABLE short_urls (
    id bigint GENERATED ALWAYS AS IDENTITY,
    code character varying(32) NOT NULL,
    original_url character varying(2048) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    expires_at_utc timestamp with time zone,
    is_active boolean NOT NULL DEFAULT TRUE,
    CONSTRAINT "PK_short_urls" PRIMARY KEY (id)
);

CREATE TABLE click_events (
    id bigint GENERATED ALWAYS AS IDENTITY,
    short_url_id bigint NOT NULL,
    occurred_at_utc timestamp with time zone NOT NULL,
    CONSTRAINT "PK_click_events" PRIMARY KEY (id),
    CONSTRAINT "FK_click_events_short_urls_short_url_id" FOREIGN KEY (short_url_id) REFERENCES short_urls (id) ON DELETE CASCADE
);

CREATE INDEX "IX_click_events_short_url_id_occurred_at_utc" ON click_events (short_url_id, occurred_at_utc);

CREATE UNIQUE INDEX "IX_short_urls_code" ON short_urls (code);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261010044918_InitialCreate', '8.0.24');

COMMIT;

-- Operational queries are prepared for the current PostgreSQL session.
-- Keep these aligned with the LINQ queries in the EF Core repositories.

PREPARE resolve_short_url(character varying, timestamp with time zone) AS
SELECT id, original_url
FROM short_urls
WHERE code = $1
    AND is_active = TRUE
    AND (expires_at_utc IS NULL OR expires_at_utc > $2);

-- List links with their derived click count without duplicating aggregate data.
PREPARE list_short_urls(integer) AS
SELECT
        short_url.id,
        short_url.code,
        short_url.original_url,
        short_url.created_at_utc,
        short_url.expires_at_utc,
        short_url.is_active,
        COUNT(click_event.id) AS click_count
FROM short_urls AS short_url
LEFT JOIN click_events AS click_event ON click_event.short_url_id = short_url.id
GROUP BY short_url.id
ORDER BY short_url.created_at_utc DESC
LIMIT $1;

-- Record an access time for a previously resolved short URL.
PREPARE record_click(bigint, timestamp with time zone) AS
INSERT INTO click_events (short_url_id, occurred_at_utc)
VALUES ($1, $2)
RETURNING id;

-- Aggregate clicks by UTC day for a short URL and a half-open time range.
PREPARE daily_click_counts(bigint, timestamp with time zone, timestamp with time zone) AS
SELECT
        (occurred_at_utc AT TIME ZONE 'UTC')::date AS date_utc,
        COUNT(*) AS clicks
FROM click_events
WHERE short_url_id = $1
    AND occurred_at_utc >= $2
    AND occurred_at_utc < $3
GROUP BY date_utc
ORDER BY date_utc;


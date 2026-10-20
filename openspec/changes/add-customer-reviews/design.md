# Design

## Context

See `proposal.md` and `specs/` for scope and behavior. OrderHub uses a modular monolith, EF Core for writes, Dapper for reads, explicit tenant/unit filters and an existing transactional outbox. Orders do not store `CompletedAt`; completion time is available in immutable order status history. No review module or review UI exists.

## Goals / Non-Goals

**Goals:** keep eligibility anchored to the opaque public order reference; publish the establishment rating and all submitted product ratings atomically; keep public summaries immediately consistent; provide an auditable moderation history; reuse Quasar rating controls.

**Non-Goals:** customer accounts, replies by establishments, editing/deleting a published rating, notification invites, ranking or sorting by reputation, cross-unit averages, third-party automated moderation, and a new review dependency.

## Decisions

1. **New `Reviews` module.** Keep review behavior separate from Orders and Catalog because a review can target either an order or catalog product. Application checks order completion, age and product membership; Domain owns rating/comment/status invariants. The alternative of placing reviews in `Orders` or `Catalog` couples opinion lifecycle to unrelated aggregates.
2. **One review record per order and target.** Use `target_kind` plus a non-null `target_id` (the establishment ID for the establishment target, product ID for a product target) and a tenant-first unique index on `(tenant_id, establishment_id, order_id, target_kind, target_id)`. This avoids nullable uniqueness and keeps duplicate prevention in storage.
3. **Serializable idempotent submission.** Store the idempotency key, canonical payload hash and resulting response in a `review_request` table scoped to tenant and unit. The existing public-order request pattern informs canonical hashing and replay. One transaction creates all reviews, updates summaries and stores the response; same-key/different-payload is a conflict.
4. **Completion window from status history.** Read the `Completed` history event for the order resolved from the public reference. Eligibility ends 30 days after that event. Current status must remain `Completed`.
5. **Synchronous derived summary.** Keep `rating_summary` with count, rating sum and five buckets, updated in the same transaction as publication/moderation. Average is derived from sum/count. Dapper joins this projection in catalog reads to avoid per-product queries. Outbox/eventual consistency was rejected because the requirement is immediate public consistency.
6. **Human review before publication.** Every submission starts `Pending` and is excluded from public reads and summaries. A manager must approve it before it becomes `Published`. Rejection transitions `Pending` to terminal `Rejected`; hiding transitions `Published` to `Hidden`, and only `Hidden` can be restored. Both rejection and hiding require a reason. Store current state on the review and append approval/rejection/hide/restore actions with actor, timestamp and reason. Public reads select only `Published`; administration reads include all states.
7. **Rating control is feature-local.** Use Quasar `q-rating` in public entry, public display and administration filters/display. Keep explicit text labels, keyboard behavior and score text; do not introduce a global wrapper for this single domain feature.
8. **Default product decisions.** Honor the accepted defaults: 30-day window, no edit after submission, anonymous public author and public averages from the first published review. Rating is an integer 1–5; ratings below 3 require a normalized 10–1000-character comment.

## Risks / Trade-offs

- **Public comments may contain personal data or offensive content** → submissions stay pending for authorized human review before publication; authors remain anonymous and all moderation actions are audited. Automated third-party screening remains outside this change.
- **Unique target semantics** → a product appearing on multiple lines in the same order receives one product-level review; verify payloads deduplicate repeated product IDs and reject conflicting duplicate entries.
- **Summary contention on popular products** → update only the affected establishment and product summary rows in the publication transaction; measure before considering asynchronous aggregation.
- **Reference is a bearer secret** → never return it or internal identifiers in public review responses; resolve all writes through the order locator and filter by server-derived tenant/unit.
- **Existing products may be removed after purchase** → allow their order snapshot to prove purchase; avoid requiring the product to remain active for submission, while keeping historical summaries associated with the product ID.

## Migration Plan

Add the `reviews` schema, review/request/moderation/summary tables and tenant-first indexes with a non-destructive EF migration. There is no review backfill. Deploy migration before API binaries; rollback by disabling review routes and retaining data. Do not run destructive `Down` on a database containing reviews.


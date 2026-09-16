# REST API Reference

Browser pages authenticate with the ASP.NET Core Identity cookie and
never need these endpoints directly (they're consumed by `site.js` for
DataTables/notifications/Chart.js). The endpoints below are for
**external integrations** (mobile apps, third-party monitoring systems)
and require a JWT bearer token.

## Authentication

```
POST /api/auth/token
Content-Type: application/json

{ "email": "admin@atmticketing.local", "password": "Admin@12345" }
```

Response:

```json
{ "accessToken": "eyJhbGciOi...", "expiresAtUtc": "2026-09-16T12:00:00Z" }
```

Send the token on every subsequent call: `Authorization: Bearer <accessToken>`.

## Tickets — `/api/tickets`

| Method | Route | Description |
|---|---|---|
| GET | `/api/tickets/{id}` | Full ticket details (same shape as the Ticket Details page) |
| POST | `/api/tickets` | Create a ticket — body matches `TicketCreateDto` |
| POST | `/api/tickets/{id}/status` | Update status — body matches `TicketStatusUpdateDto` |
| POST | `/api/tickets/{id}/assign` | Assign/reassign — body matches `TicketAssignDto` |
| GET | `/api/tickets/my` | Tickets currently assigned to the authenticated engineer |

## ATMs — `/api/atms`

| Method | Route | Description |
|---|---|---|
| GET | `/api/atms/search?term=` | Active-ATM typeahead search (code/name/city) |

## Notifications — `/api/notifications` (cookie-authenticated, browser use)

| Method | Route | Description |
|---|---|---|
| GET | `/api/notifications/unread` | Unread in-app notifications for the current user |
| POST | `/api/notifications/{id}/read` | Mark one notification read |

## Dashboard — `/api/dashboard` (cookie-authenticated, browser use)

`summary`, `engineer-workload`, `region-wise`, `priority-wise`,
`vendor-wise`, `daily-trend?days=`, `monthly-trend?months=` — all GET,
all backing the Dashboard's KPI tiles and Chart.js widgets.

## Error shape

Service-layer failures return the `ServiceResult` envelope:

```json
{ "succeeded": false, "message": null, "errors": ["Ticket not found."] }
```

# FamilyBudget AI API Documentation

Base URL:

```text
http://localhost:5001
```

Production base URL:

```text
https://<your-api-app>.azurewebsites.net
```

Swagger:

- Development: `/swagger`
- Production: available only when `ApiDocs__EnableSwagger=true`

Authentication uses JWT bearer tokens:

```text
Authorization: Bearer <token>
```

## Health

### GET /health

Returns `200 OK` when the API process is healthy.

Use this endpoint for hosting platform health probes and deployment smoke tests.

## Auth

### POST /api/auth/register

Request:

```json
{
  "username": "demo",
  "firstName": "Demo",
  "lastName": "User",
  "email": "demo@example.com",
  "password": "StrongPassword123!"
}
```

Response: `200 OK`

```json
{
  "token": "<jwt>",
  "expiresAt": "2026-06-26T12:00:00Z",
  "user": {
    "id": 1,
    "username": "demo",
    "firstName": "Demo",
    "lastName": "User",
    "email": "demo@example.com",
    "createdAt": "2026-06-26T11:00:00Z"
  }
}
```

### POST /api/auth/login

Request:

```json
{
  "username": "demo",
  "password": "StrongPassword123!"
}
```

Response: `200 OK` with the same shape as register.

### GET /api/auth/me

Requires authorization.

Response: `200 OK`

```json
{
  "id": 1,
  "username": "demo",
  "firstName": "Demo",
  "lastName": "User",
  "email": "demo@example.com",
  "createdAt": "2026-06-26T11:00:00Z"
}
```

## Subscriptions

All subscription endpoints require authorization.

### GET /api/subscriptions

Returns all subscriptions for the authenticated user.

### GET /api/subscriptions/{id}

Returns one subscription or `404 Not Found`.

### POST /api/subscriptions

Request:

```json
{
  "name": "Spotify",
  "cost": 119,
  "billingFrequency": "Monthly",
  "renewalDate": "2026-07-15",
  "category": "Music"
}
```

Response: `201 Created`

### PUT /api/subscriptions/{id}

Uses the same request shape as create. Returns the updated subscription or `404 Not Found`.

### DELETE /api/subscriptions/{id}

Returns `204 No Content` or `404 Not Found`.

## Dashboard

### GET /api/dashboard/summary

Requires authorization. Returns spending totals, upcoming renewals, category totals, and financial health summary data.

## AI Conversations

All AI endpoints require authorization.

### GET /api/ai/conversations

Returns conversation summaries.

### POST /api/ai/conversations

Request:

```json
{
  "title": "Budget review"
}
```

The title is optional.

### GET /api/ai/conversations/{id}

Returns a conversation with messages or `404 Not Found`.

### POST /api/ai/conversations/{id}/messages

Request:

```json
{
  "content": "Which subscriptions renew this week?"
}
```

Returns the saved user message and assistant response.

### DELETE /api/ai/conversations/{id}

Returns `204 No Content` or `404 Not Found`.

## Notifications

All notification endpoints require authorization.

### GET /api/notifications

Returns renewal reminders and other notifications for the authenticated user.

### POST /api/notifications/mark-read/{id}

Marks a notification as read. Returns `204 No Content` or `404 Not Found`.

## Bank Statements

All bank statement endpoints require authorization.

The backend processes uploaded PDFs immediately and stores extracted transaction data. It does not store the original PDF file.

### POST /api/bank-statements/upload

Request:

```text
multipart/form-data
file: statement.pdf
```

Limits:

```text
PDF only
Max file size: 10 MB
```

Response: `200 OK`

```json
{
  "statementId": 1,
  "transactionCount": 84,
  "needsReviewCount": 7,
  "recurringCandidateCount": 3
}
```

The first parser supports generic statement text rows that contain a date, description, and trailing amount.

### GET /api/bank-statements

Returns imported statement metadata for the authenticated user.

### GET /api/bank-statements/{id}/transactions

Returns extracted transactions for one imported statement.

## Transactions

All transaction endpoints require authorization.

### PUT /api/transactions/{id}/category

Updates one transaction category. When `rememberRule` is `true`, the backend stores a user-specific category rule for similar future descriptions.

Request:

```json
{
  "category": "Groceries",
  "rememberRule": true
}
```

### GET /api/transactions/summary

Optional query:

```text
from=2026-06-01
to=2026-06-30
```

Returns:

```text
income
expenses
netSavings
savingsRate
categoryTotals
largestTransactions
recurringPaymentTotal
```

### GET /api/transactions/recurring-candidates

Returns possible recurring payments detected from repeated merchant and amount patterns.

## Error Format

Validation and server errors use `application/problem+json`.

Example validation response:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Request validation failed.",
  "status": 400,
  "errors": {
    "Email": [
      "The Email field is not a valid e-mail address."
    ]
  },
  "traceId": "00-..."
}
```

Production `500` responses hide exception details but include `traceId` for log correlation.

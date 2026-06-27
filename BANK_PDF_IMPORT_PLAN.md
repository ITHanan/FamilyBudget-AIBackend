# Bank PDF Import Feature Plan

This document describes the planned feature for importing bank statement PDFs, extracting transactions, categorizing spending, detecting recurring payments, and showing analysis in the FamilyBudget AI app.

## Goal

Allow a signed-in user to upload a bank statement PDF. The backend reads the PDF, extracts clean transaction data, saves the extracted transactions, and provides summaries and analysis to the frontend.

The app should save extracted financial data, not the original PDF file.

## Recommended User Flow

1. User opens `Statements` or `Bank Import` from the app navigation.
2. User selects a PDF bank statement.
3. Frontend uploads the PDF to the backend.
4. Backend validates the file, extracts text, parses transactions, categorizes them, and saves the results.
5. Frontend shows import result:
   - number of transactions imported
   - number of categories detected
   - possible subscriptions found
   - transactions needing review
6. User reviews transactions and fixes incorrect categories.
7. User views spending summary and category breakdown.
8. User can add detected recurring payments to the existing subscriptions module.
9. User can ask AI for budget analysis based on the extracted summary.

## Frontend Screens

### Statements Page

Purpose: entry point for uploaded statements.

Expected UI:

```text
Imported Statements
- June 2026 statement
- May 2026 statement

[Upload new statement]
```

Actions:

```text
View statement
Delete imported statement
Re-analyze statement
Upload new PDF
```

### Upload Statement Page

Purpose: upload a bank statement PDF.

Expected UI:

```text
Upload bank statement
[ Choose PDF file ]
[ Import transactions ]
```

States:

```text
No file selected
File selected
Uploading
Extracting transactions
Categorizing spending
Import complete
Import failed
```

Success example:

```text
Imported 84 transactions
12 categories detected
3 possible subscriptions found

[Review transactions]
```

Failure example:

```text
We could not read this PDF.
Try downloading the statement again from your bank.
```

### Review Transactions Page

Purpose: let the user inspect and correct imported transactions.

Table columns:

```text
Date
Description
Category
Amount
Recurring?
Review status
```

Controls:

```text
Search
Filter by month
Filter by category
Filter by needs review
Change category
Mark as internal transfer
Delete transaction
```

Important behavior:

When a user changes a category, offer to remember the choice for similar future transactions.

### Summary Page

Purpose: show calculated financial summary from imported transactions.

Metrics:

```text
Total income
Total expenses
Net savings
Savings rate
Top spending category
Largest transaction
Recurring payment total
```

Charts:

```text
Spending by category
Monthly cashflow
Income vs expenses
Largest merchants
```

### Detected Subscriptions Page/Section

Purpose: connect imported bank data to the existing subscription tracker.

Example:

```text
Possible subscriptions found

Spotify     119 kr/month
Netflix     149 kr/month
Microsoft   999 kr/year
```

Actions:

```text
Add to subscriptions
Ignore
Not a subscription
```

### AI Analysis Page/Section

Purpose: explain the spending data in plain language.

Prompt buttons:

```text
Explain my spending
Find ways to save
Find unusual transactions
Compare this month to last month
Find subscriptions I may not need
```

The AI should receive calculated summaries, not the original PDF.

## Backend Data Model

### BankStatement

Stores metadata about an imported statement.

Suggested fields:

```text
Id
UserId
OriginalFileName
BankName nullable
StatementPeriodStart nullable
StatementPeriodEnd nullable
UploadedAt
ImportedAt
TransactionCount
ImportStatus
ImportError nullable
```

Do not store the original PDF by default.

### BankTransaction

Stores extracted transaction data.

Suggested fields:

```text
Id
UserId
BankStatementId
TransactionDate
Description
NormalizedDescription
Amount
Currency
Category
IsIncome
IsInternalTransfer
IsRecurringCandidate
NeedsReview
CreatedAt
```

### TransactionCategoryRule

Stores default and user-created categorization rules.

Suggested fields:

```text
Id
UserId nullable
MatchText
Category
Priority
CreatedAt
```

Examples:

```text
ICA -> Groceries
WILLYS -> Groceries
SPOTIFY -> Subscriptions
NETFLIX -> Subscriptions
SL -> Transport
```

### RecurringPaymentCandidate

Optional table if recurring detection becomes complex.

Suggested fields:

```text
Id
UserId
MerchantName
AverageAmount
BillingFrequency
Confidence
FirstSeenDate
LastSeenDate
SuggestedCategory
Status
```

## Backend Endpoints

### Upload Statement

```text
POST /api/bank-statements/upload
```

Request:

```text
multipart/form-data
file: statement.pdf
```

Response:

```json
{
  "statementId": 1,
  "transactionCount": 84,
  "needsReviewCount": 7,
  "recurringCandidateCount": 3
}
```

### List Statements

```text
GET /api/bank-statements
```

Returns imported statement metadata for the signed-in user.

### Get Statement Transactions

```text
GET /api/bank-statements/{id}/transactions
```

Supports query filters later:

```text
category
from
to
needsReview
search
```

### Update Transaction Category

```text
PUT /api/transactions/{id}/category
```

Request:

```json
{
  "category": "Groceries",
  "rememberRule": true
}
```

### Get Transaction Summary

```text
GET /api/transactions/summary?from=2026-06-01&to=2026-06-30
```

Response should include:

```text
income
expenses
netSavings
savingsRate
categoryTotals
largestTransactions
recurringPaymentTotal
```

### Get Recurring Candidates

```text
GET /api/transactions/recurring-candidates
```

Used to suggest subscriptions.

## Categories

Start with a compact category list:

```text
Income
Housing
Groceries
Transport
Subscriptions
Utilities
Shopping
Restaurants
Healthcare
Savings
Entertainment
Other
```

Later optional categories:

```text
Fuel
Insurance
Debt & Loans
Children & Family
Education
Travel
Transfers
Cash Withdrawals
Fees & Bank Charges
Taxes
```

## PDF Processing

Recommended package:

```text
UglyToad.PdfPig
```

First implementation should support one known bank PDF format. Do not try to support every bank format at once.

Processing steps:

```text
1. Validate file type and size.
2. Read PDF text.
3. Normalize text lines.
4. Parse transaction rows.
5. Detect date, description, amount, and currency.
6. Categorize transactions.
7. Detect recurring candidates.
8. Save statement metadata and transactions.
9. Return import result.
```

## Parsing Strategy

Bank PDFs differ by bank and sometimes by account type. Use bank-specific parsers behind a shared interface.

Suggested interfaces:

```text
IBankStatementImportService
IPdfTextExtractor
IBankStatementParser
ITransactionCategorizer
IRecurringPaymentDetector
```

Start simple:

```text
PdfPigPdfTextExtractor
GenericBankStatementParser
RuleBasedTransactionCategorizer
BasicRecurringPaymentDetector
```

Later add:

```text
SwedbankStatementParser
NordeaStatementParser
SEBStatementParser
HandelsbankenStatementParser
RevolutStatementParser
```

## Categorization Rules

Use deterministic rules first.

Example rules:

```text
description contains "ICA" -> Groceries
description contains "WILLYS" -> Groceries
description contains "COOP" -> Groceries
description contains "SPOTIFY" -> Subscriptions
description contains "NETFLIX" -> Subscriptions
description contains "DISNEY" -> Subscriptions
description contains "SL" -> Transport
description contains "UBER" -> Transport
description contains "LÖN" -> Income
amount > 0 -> Income
otherwise -> Other
```

AI can be used later for uncertain transactions, but normal rules should do the first pass.

## Recurring Payment Detection

Detect recurring candidates by grouping transactions with:

```text
same or similar normalized description
same or similar amount
monthly or yearly pattern
at least 2-3 occurrences
```

Examples:

```text
Spotify 119 kr every month -> subscription candidate
Netflix 149 kr every month -> subscription candidate
Microsoft 999 kr yearly -> subscription candidate
```

## AI Analysis

Do not send the raw PDF to AI by default.

Instead send clean calculated summaries:

```text
Income: 32500
Expenses: 24200
Savings: 8300
Savings rate: 25.5%
Top categories:
- Groceries: 4320
- Restaurants: 3150
- Subscriptions: 1245
Possible subscriptions:
- Spotify: 119/month
- Netflix: 149/month
```

AI should explain and recommend, not perform critical calculations.

## Privacy And Security

Bank statements are sensitive.

Rules:

```text
Do not store original PDFs unless absolutely necessary.
Delete temporary files after processing.
Store only extracted transaction data.
Never log full PDF text or full transaction lists.
Only return transactions belonging to the authenticated user.
Add file size limits.
Accept only PDF MIME type and .pdf extension.
Consider malware scanning if storing files later.
Allow users to delete imported statements and transactions.
```

Avoid storing:

```text
Full account numbers
Raw PDF text
Original PDF files
Bank login credentials
Unnecessary personal identifiers
```

## Implementation Phases

### Phase 1: Basic Import

Backend:

```text
Add BankStatement and BankTransaction entities
Add EF migration
Add PDF upload endpoint
Extract PDF text
Parse one known PDF format
Save transactions
```

Frontend:

```text
Add Statements navigation item
Add upload page
Show import success/failure
Show imported transactions table
```

### Phase 2: Categories And Review

Backend:

```text
Add category rules
Add update category endpoint
Add needs review flag
```

Frontend:

```text
Add category filters
Add category editor
Add needs review view
Add remember rule option
```

### Phase 3: Summaries

Backend:

```text
Add transaction summary service
Add category totals endpoint
Add monthly trend calculations
```

Frontend:

```text
Add spending summary
Add category chart
Add income vs expenses chart
```

### Phase 4: Subscription Detection

Backend:

```text
Add recurring payment detector
Add recurring candidates endpoint
Add endpoint to convert candidate to subscription
```

Frontend:

```text
Show possible subscriptions
Add to subscriptions
Ignore candidate
```

### Phase 5: AI Insights

Backend:

```text
Create AI analysis prompt from summary data
Add transaction analysis endpoint
Do not send raw PDF
```

Frontend:

```text
Add AI analysis panel
Add prompt buttons
Show savings suggestions and unusual spending
```

## Testing Plan

Backend unit tests:

```text
PDF text normalization
Transaction parser
Category rules
Recurring detection
Summary calculations
User ownership checks
```

Backend integration tests:

```text
Upload PDF as authenticated user
Reject unauthenticated upload
Reject non-PDF upload
Reject oversized file
List only current user's statements
Update transaction category
```

Frontend tests/manual QA:

```text
Upload flow
Import failure state
Transactions table
Category editing
Summary rendering
Detected subscriptions actions
```

## First Files To Create Later

Suggested backend files:

```text
src/Domain/Entities/BankStatement.cs
src/Domain/Entities/BankTransaction.cs
src/Domain/Entities/TransactionCategoryRule.cs
src/Application/DTOs/BankStatementDtos.cs
src/Application/Interfaces/IBankStatementImportService.cs
src/Application/Interfaces/ITransactionSummaryService.cs
src/Infrastructure/Services/BankStatementImportService.cs
src/Infrastructure/Services/PdfTextExtractor.cs
src/Infrastructure/Services/TransactionCategorizer.cs
src/Infrastructure/Services/RecurringPaymentDetector.cs
src/Api/Controllers/BankStatementsController.cs
src/Api/Controllers/TransactionsController.cs
```

Suggested frontend files:

```text
src/features/statements/StatementsPage.tsx
src/features/statements/UploadStatementPage.tsx
src/features/statements/TransactionsPage.tsx
src/features/statements/StatementSummaryPage.tsx
src/features/statements/DetectedSubscriptions.tsx
```

## Open Questions

Before implementation, decide:

```text
Which bank PDF format should be supported first?
Should CSV upload be supported as an easier first option?
What max PDF file size should be allowed?
Should original PDFs ever be stored?
Which currency should be default, SEK only or multi-currency?
Should users be able to delete all imported data?
```

Recommended answers for version 1:

```text
Support one bank PDF first.
Add CSV later or support it alongside PDF if the bank offers it.
Max file size: 10 MB.
Do not store original PDFs.
Default currency: SEK.
Allow statement and transaction deletion.
```

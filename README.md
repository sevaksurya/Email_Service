# EmailService

A serverless, reliable email delivery and scheduling microservice built on **Azure Functions v4 (.NET 10 Isolated Worker)**, **Azure Table Storage**, and **MailKit (Gmail SMTP)**.

---

## Features

- **Dual Dispatch Modes**:
  - **Immediate Dispatch**: Sends emails instantly via Gmail SMTP over TLS (Port 587).
  - **Scheduled Delivery**: Future-dated emails are stored in Azure Table Storage and automatically dispatched when their scheduled time arrives.
- **Automated Timer Dispatcher**: Background worker periodically polls for due emails using configurable NCRONTAB expressions.
- **Fault Tolerance & Retries**: Automatic retry handling (up to 3 retries) with isolation of failed jobs into a `Failed` partition.
- **Minimal Storage Footprint**: Scheduled emails are deleted from Table Storage immediately upon successful delivery.
- **Observability**: Integrated OpenTelemetry and Azure Application Insights support.

---

## Architecture

```
[ Client Application ]
         │
         ▼  POST /api/emails
┌──────────────────────────────────────────────┐
│       SendOrScheduleEmail Function           │
└──────────────────────┬───────────────────────┘
                       │
         ┌─────────────┴─────────────┐
         ▼                           ▼
[ Scheduled in Future ]       [ Immediate / Past ]
         │                           │
         ▼                           ▼
┌──────────────────┐        ┌──────────────────┐
│ Azure Table      │        │ GmailSmtpService │
│ Storage          │        │ (MailKit / TLS)  │
│ (Pending)        │        └────────┬─────────┘
└────────┬─────────┘                 │
         │ (Timer Trigger)           │
         ▼                           ▼
┌──────────────────┐          smtp.gmail.com
│ ScheduledEmail-  │                 │
│ Dispatcher       │                 ▼
└────────┬─────────┘          [ Recipient Inbox ]
         │
         ▼ (Dispatched & Cleaned)
  [ Delete from Table ]
```

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (local Azure Storage emulator) or an Azure Storage Account
- **Gmail Account with App Password**:
  1. Enable 2-Step Verification on your Google Account.
  2. Navigate to **Google Account** > **Security** > **2-Step Verification** > **App passwords**.
  3. Generate a 16-character App Password for this service.

---

## Getting Started

### 1. Clone & Configure

1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd Email_Service
   ```

2. Copy the example settings file:
   ```bash
   cp EmailService/local.settings.json.example EmailService/local.settings.json
   ```

3. Update `EmailService/local.settings.json` with your credentials:
   ```json
   {
     "IsEncrypted": false,
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
       "DispatcherSchedule": "0 */1 * * * *",
       "Smtp:SenderEmail": "your-email@gmail.com",
       "Smtp:SenderName": "Notification Service",
       "Smtp:AppPassword": "your-16-character-app-password",
       "APPLICATIONINSIGHTS_CONNECTION_STRING": ""
     }
   }
   ```

### 2. Run Locally

#### Option A: Visual Studio 2022
1. Open `EmailService.slnx`.
2. Ensure `EmailService` is set as the Startup Project.
3. Press **F5**. Visual Studio starts Azurite and the Functions host automatically.

#### Option B: VS Code
1. Start Azurite (`Ctrl+Shift+P` > `Azurite: Start`).
2. Press **F5** (uses preconfigured `.vscode/launch.json`).

#### Option C: Terminal / CLI
1. Start Azurite in one terminal:
   ```powershell
   npx azurite
   ```
2. In a second terminal, run:
   ```powershell
   cd EmailService
   func start
   ```

---

## API Reference

### `POST /api/emails`

Sends an email immediately or schedules it for future delivery.

#### Request Headers
| Header | Value | Description |
| :--- | :--- | :--- |
| `Content-Type` | `application/json` | Required |
| `x-functions-key` | `<api-key>` | Required in Azure environment (`Function` auth level) |

#### Request Body
| Field | Type | Required | Description |
| :--- | :--- | :--- | :--- |
| `to` | string | **Yes** | Destination email address. |
| `subject` | string | **Yes** | Subject line of the email. |
| `body` | string | No | Content of the message (plain text or HTML). |
| `isHtml` | boolean | No | Set to `true` (default) for HTML content; `false` for plain text. |
| `sourceApp` | string | No | Identifier of the calling service (e.g., `"AuthService"`). |
| `scheduledTimeUtc` | string (ISO 8601) | No | UTC timestamp. If future, the email is scheduled; if omitted or in the past, sent immediately. |

#### Example 1: Immediate Send
```http
POST /api/emails
Content-Type: application/json

{
  "to": "user@example.com",
  "subject": "Welcome to Our Platform",
  "body": "<h1>Welcome!</h1><p>Your account is ready.</p>",
  "isHtml": true,
  "sourceApp": "UserService"
}
```
**Response (`200 OK`):**
```json
{
  "status": "SentImmediately",
  "sentAtUtc": "2026-11-13T19:00:00Z"
}
```

#### Example 2: Scheduled Send
```http
POST /api/emails
Content-Type: application/json

{
  "to": "user@example.com",
  "subject": "Your Subscription Renewal Reminder",
  "body": "<p>Your subscription renews tomorrow.</p>",
  "isHtml": true,
  "sourceApp": "BillingService",
  "scheduledTimeUtc": "2026-11-14T08:00:00Z"
}
```
**Response (`202 Accepted`):**
```json
{
  "status": "Scheduled",
  "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "scheduledTimeUtc": "2026-11-14T08:00:00Z"
}
```

---

## Configuration Reference

| Setting | Type | Description |
| :--- | :--- | :--- |
| `AzureWebJobsStorage` | Connection String | Azure Table & Storage account connection (`UseDevelopmentStorage=true` locally). |
| `DispatcherSchedule` | NCRONTAB | Schedule for timer dispatcher (e.g., `0 */1 * * * *` = every minute). |
| `Smtp:SenderEmail` | Email Address | Gmail address sending the emails. |
| `Smtp:AppPassword` | String | 16-character Google App Password. |
| `Smtp:SenderName` | String | Optional sender display name (defaults to `"Notification Service"`). |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | String | Optional Application Insights telemetry connection. |

---

## License

This project is licensed under the [MIT License](LICENSE.txt).

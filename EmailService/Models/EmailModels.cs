using Azure;
using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.Text;

namespace EmailService.Models
{
    // 1. Incoming payload sent by calling applications
    public class EmailRequest
    {
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsHtml { get; set; } = true;
        public string? SourceApp { get; set; }

        // If null or in the past, the email sends immediately.
        // If in the future (UTC), it is stored for the Timer Trigger.
        public DateTimeOffset? ScheduledTimeUtc { get; set; }
    }

    // 2. Database row stored in Azure Table Storage
    public class ScheduledEmailEntity : ITableEntity
    {
        // PartitionKey groups records by status ("Pending", "Sent", "Failed")
        public string PartitionKey { get; set; } = "Pending";

        // RowKey is the unique ID for the email job
        public string RowKey { get; set; } = Guid.NewGuid().ToString();

        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsHtml { get; set; } = true;
        public string SourceApp { get; set; } = "Unknown";
        public DateTimeOffset ScheduledTimeUtc { get; set; }
        public int RetryCount { get; set; } = 0;
        public string? LastError { get; set; }

        // Required by ITableEntity for optimistic concurrency and system tracking
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}

using Azure.Data.Tables;
using EmailService.Models;
using EmailService.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace EmailService.Functions
{
    public class ScheduleEmailFunction
    {
        private readonly IEmailSender _emailSender;
        private readonly TableClient _tableClient;
        private readonly ILogger<ScheduleEmailFunction> _logger;
        private const int MaxRetries = 3;

        public ScheduleEmailFunction(
            IEmailSender emailSender,
            TableServiceClient tableServiceClient,
            ILogger<ScheduleEmailFunction> logger)
        {
            _emailSender = emailSender;
            _logger = logger;
            _tableClient = tableServiceClient.GetTableClient("ScheduledEmails");
        }

        // NCRONTAB expression: "{second} {minute} {hour} {day} {month} {day-of-week}"
        // "%DispatcherSchedule%" reads the cron schedule from configuration settings
        [Function("ScheduledEmailDispatcher")]
        public async Task RunAsync(
            [TimerTrigger("%DispatcherSchedule%")] TimerInfo myTimer,
            CancellationToken cancellationToken)
        {
            await _tableClient.CreateIfNotExistsAsync(cancellationToken);
            var nowUtc = DateTimeOffset.UtcNow;

            // Query only "Pending" partition where ScheduledTimeUtc has arrived
            var dueEmails = _tableClient.QueryAsync<ScheduledEmailEntity>(
                e => e.PartitionKey == "Pending" && e.ScheduledTimeUtc <= nowUtc,
                cancellationToken: cancellationToken);

            int processedCount = 0;

            await foreach (var emailJob in dueEmails)
            {
                try
                {
                    await _emailSender.SendEmailAsync(
                        emailJob.To,
                        emailJob.Subject,
                        emailJob.Body,
                        emailJob.IsHtml,
                        cancellationToken);

                    // Delete from table upon successful send to keep storage footprint near zero
                    await _tableClient.DeleteEntityAsync(emailJob.PartitionKey, emailJob.RowKey, emailJob.ETag, cancellationToken);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send scheduled email {RowKey} to {Recipient}.", emailJob.RowKey, emailJob.To);

                    emailJob.RetryCount++;
                    emailJob.LastError = ex.Message;

                    if (emailJob.RetryCount >= MaxRetries)
                    {
                        // Move to Failed partition so the timer stops retrying broken addresses forever
                        await _tableClient.DeleteEntityAsync(emailJob.PartitionKey, emailJob.RowKey, emailJob.ETag, cancellationToken);
                        emailJob.PartitionKey = "Failed";
                        await _tableClient.AddEntityAsync(emailJob, cancellationToken);
                    }
                    else
                    {
                        await _tableClient.UpdateEntityAsync(emailJob, emailJob.ETag, TableUpdateMode.Replace, cancellationToken);
                    }
                }
            }

            if (processedCount > 0)
            {
                _logger.LogInformation("Timer dispatcher completed. Sent {Count} scheduled emails.", processedCount);
            }
        }
    }
}

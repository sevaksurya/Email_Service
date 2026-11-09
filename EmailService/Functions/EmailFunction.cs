using Azure.Data.Tables;
using EmailService.Models;
using EmailService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace EmailService.Functions;

public class EmailFunction
{
    private readonly IEmailSender _emailSender;
    private readonly TableClient _tableClient;
    private readonly ILogger<EmailFunction> _logger;

    public EmailFunction(
        IEmailSender emailSender,
        TableServiceClient tableServiceClient,
        ILogger<EmailFunction> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
        _tableClient = tableServiceClient.GetTableClient("ScheduledEmails");
    }

    [Function("SendOrScheduleEmail")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "emails")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var payload = await req.ReadFromJsonAsync<EmailRequest>(cancellationToken);

        if (payload == null || string.IsNullOrWhiteSpace(payload.To) || string.IsNullOrWhiteSpace(payload.Subject))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteAsJsonAsync(new { error = "'To' and 'Subject' fields are required." }, cancellationToken);
            return badResponse;
        }

        var nowUtc = DateTimeOffset.UtcNow;

        // Path A: Scheduled in the future -> Persist to Azure Table Storage
        if (payload.ScheduledTimeUtc.HasValue && payload.ScheduledTimeUtc.Value > nowUtc)
        {
            await _tableClient.CreateIfNotExistsAsync(cancellationToken);

            var entity = new ScheduledEmailEntity
            {
                PartitionKey = "Pending",
                RowKey = Guid.NewGuid().ToString(),
                To = payload.To,
                Subject = payload.Subject,
                Body = payload.Body,
                IsHtml = payload.IsHtml,
                SourceApp = payload.SourceApp ?? "ExternalApp",
                ScheduledTimeUtc = payload.ScheduledTimeUtc.Value.ToUniversalTime()
            };

            await _tableClient.AddEntityAsync(entity, cancellationToken);
            _logger.LogInformation("Scheduled email {Id} for {Recipient} at {ScheduledTimeUtc}.",
                entity.RowKey, entity.To, entity.ScheduledTimeUtc);

            var acceptedResponse = req.CreateResponse(HttpStatusCode.Accepted);
            await acceptedResponse.WriteAsJsonAsync(new
            {
                status = "Scheduled",
                jobId = entity.RowKey,
                scheduledTimeUtc = entity.ScheduledTimeUtc
            }, cancellationToken);
            return acceptedResponse;
        }

        // Path B: Immediate send -> Dispatch right away via Gmail SMTP
        await _emailSender.SendEmailAsync(payload.To, payload.Subject, payload.Body, payload.IsHtml, cancellationToken);

        var okResponse = req.CreateResponse(HttpStatusCode.OK);
        await okResponse.WriteAsJsonAsync(new
        {
            status = "SentImmediately",
            sentAtUtc = nowUtc
        }, cancellationToken);
        return okResponse;
    }
}
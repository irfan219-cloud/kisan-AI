using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using KisanMitraAI.Core.Advisory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace KisanMitraAI.Infrastructure.Advisory;

public class ExpertEscalationHandler : IExpertEscalationHandler
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExpertEscalationHandler> _logger;
    private readonly string _escalationTableName;
    private readonly string _expertNotificationTopicArn;

    public ExpertEscalationHandler(
        IAmazonDynamoDB dynamoClient,
        IAmazonSimpleNotificationService snsClient,
        IConfiguration configuration,
        ILogger<ExpertEscalationHandler> logger)
    {
        _dynamoClient = dynamoClient;
        _snsClient = snsClient;
        _configuration = configuration;
        _logger = logger;
        
        _escalationTableName = configuration["AWS:DynamoDB:EscalationTableName"] 
            ?? "KisanMitra-Escalations";
        _expertNotificationTopicArn = configuration["AWS:SNS:ExpertNotificationTopicArn"] 
            ?? throw new InvalidOperationException("Expert notification topic ARN not configured");
    }

    public async Task<string> EscalateQuestionAsync(
        AdvisoryQuestion question,
        float confidenceScore,
        CancellationToken cancellationToken)
    {
        try
        {
            var escalationId = Guid.NewGuid().ToString();
            var now = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Escalating question to expert for farmer {FarmerId} with confidence {Confidence}",
                question.FarmerId,
                confidenceScore);

            // Store escalation in DynamoDB
            var item = new Dictionary<string, AttributeValue>
            {
                ["EscalationId"] = new AttributeValue { S = escalationId },
                ["FarmerId"] = new AttributeValue { S = question.FarmerId },
                ["Question"] = new AttributeValue { S = question.QuestionText },
                ["Context"] = new AttributeValue { S = question.Context },
                ["ConversationHistory"] = new AttributeValue { S = JsonSerializer.Serialize(question.ConversationHistory) },
                ["ConfidenceScore"] = new AttributeValue { N = confidenceScore.ToString("F2") },
                ["Status"] = new AttributeValue { S = "Pending" },
                ["EscalatedAt"] = new AttributeValue { S = now.ToString("o") },
                ["TTL"] = new AttributeValue { N = now.AddDays(30).ToUnixTimeSeconds().ToString() }
            };

            var putRequest = new PutItemRequest
            {
                TableName = _escalationTableName,
                Item = item
            };

            await _dynamoClient.PutItemAsync(putRequest, cancellationToken);

            // Notify experts via SNS
            await NotifyExpertsAsync(escalationId, question, confidenceScore, cancellationToken);

            _logger.LogInformation("Question escalated with ID {EscalationId}", escalationId);

            return escalationId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error escalating question to expert");
            throw;
        }
    }

    public async Task<EscalationStatus> GetEscalationStatusAsync(
        string escalationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new GetItemRequest
            {
                TableName = _escalationTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["EscalationId"] = new AttributeValue { S = escalationId }
                }
            };

            var response = await _dynamoClient.GetItemAsync(request, cancellationToken);

            if (!response.IsItemSet)
            {
                throw new KeyNotFoundException($"Escalation {escalationId} not found");
            }

            var item = response.Item;
            
            DateTimeOffset? respondedAt = null;
            if (item.ContainsKey("RespondedAt"))
            {
                respondedAt = DateTimeOffset.Parse(item["RespondedAt"].S);
            }

            string? expertResponse = null;
            if (item.ContainsKey("ExpertResponse"))
            {
                expertResponse = item["ExpertResponse"].S;
            }

            return new EscalationStatus(
                EscalationId: escalationId,
                Status: item["Status"].S,
                EscalatedAt: DateTimeOffset.Parse(item["EscalatedAt"].S),
                RespondedAt: respondedAt,
                ExpertResponse: expertResponse
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting escalation status for {EscalationId}", escalationId);
            throw;
        }
    }

    private async Task NotifyExpertsAsync(
        string escalationId,
        AdvisoryQuestion question,
        float confidenceScore,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = $@"
New Question Escalation - Kisan Mitra AI

Escalation ID: {escalationId}
Farmer ID: {question.FarmerId}
Confidence Score: {confidenceScore:F2}
Escalated At: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}

Question:
{question.QuestionText}

Context:
{question.Context}

Target Response Time: 24 hours

Please review and respond via the Expert Portal.
";

            var publishRequest = new PublishRequest
            {
                TopicArn = _expertNotificationTopicArn,
                Subject = $"Question Escalation: {escalationId}",
                Message = message,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["EscalationId"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = escalationId
                    },
                    ["FarmerId"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = question.FarmerId
                    },
                    ["ConfidenceScore"] = new MessageAttributeValue
                    {
                        DataType = "Number",
                        StringValue = confidenceScore.ToString("F2")
                    }
                }
            };

            await _snsClient.PublishAsync(publishRequest, cancellationToken);

            _logger.LogInformation("Expert notification sent for escalation {EscalationId}", escalationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error sending expert notification, escalation still recorded");
        }
    }
}

using System.Text.Json;
using KisanMitraAI.Core.Workflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KisanMitraAI.Infrastructure.Workflows;

/// <summary>
/// Implementation of workflow orchestrator for module-specific workflows
/// </summary>
public class WorkflowOrchestrator : IWorkflowOrchestrator
{
    private readonly IStepFunctionService _stepFunctionService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WorkflowOrchestrator> _logger;

    // State machine ARNs from configuration
    private string KrishiVaniStateMachineArn => 
        _configuration["AWS:StepFunctions:KrishiVaniStateMachineArn"] 
        ?? throw new InvalidOperationException("KrishiVani state machine ARN not configured");

    private string QualityGraderStateMachineArn => 
        _configuration["AWS:StepFunctions:QualityGraderStateMachineArn"] 
        ?? throw new InvalidOperationException("QualityGrader state machine ARN not configured");

    private string DharaAnalyzerStateMachineArn => 
        _configuration["AWS:StepFunctions:DharaAnalyzerStateMachineArn"] 
        ?? throw new InvalidOperationException("DharaAnalyzer state machine ARN not configured");

    private string SowingOracleStateMachineArn => 
        _configuration["AWS:StepFunctions:SowingOracleStateMachineArn"] 
        ?? throw new InvalidOperationException("SowingOracle state machine ARN not configured");

    public WorkflowOrchestrator(
        IStepFunctionService stepFunctionService,
        IConfiguration configuration,
        ILogger<WorkflowOrchestrator> logger)
    {
        _stepFunctionService = stepFunctionService ?? throw new ArgumentNullException(nameof(stepFunctionService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WorkflowResult> ExecuteKrishiVaniWorkflowAsync(
        KrishiVaniWorkflowInput input,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Executing Krishi-Vani workflow for farmer {FarmerId}",
            input.FarmerId);

        var inputJson = JsonSerializer.Serialize(new
        {
            farmerId = input.FarmerId,
            audioS3Uri = input.AudioS3Uri,
            languageCode = input.LanguageCode,
            dialect = input.Dialect,
            voiceId = input.VoiceId,
            jobName = $"krishi-vani-{input.FarmerId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"
        });

        var executionArn = await _stepFunctionService.StartExecutionAsync(
            KrishiVaniStateMachineArn,
            inputJson,
            $"krishi-vani-{input.FarmerId}-{Guid.NewGuid():N}",
            cancellationToken);

        var result = await _stepFunctionService.WaitForCompletionAsync(
            executionArn,
            timeoutSeconds: 30,
            pollIntervalSeconds: 2,
            cancellationToken);

        return new WorkflowResult(
            ExecutionArn: result.ExecutionArn,
            Status: result.Status,
            Output: result.Output,
            Error: result.Error,
            Duration: result.Duration);
    }

    public async Task<WorkflowResult> ExecuteQualityGraderWorkflowAsync(
        QualityGraderWorkflowInput input,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Executing Quality Grader workflow for farmer {FarmerId}, produce {ProduceType}",
            input.FarmerId,
            input.ProduceType);

        var inputJson = JsonSerializer.Serialize(new
        {
            farmerId = input.FarmerId,
            imageData = input.ImageData,
            produceType = input.ProduceType,
            location = input.Location
        });

        var executionArn = await _stepFunctionService.StartExecutionAsync(
            QualityGraderStateMachineArn,
            inputJson,
            $"quality-grader-{input.FarmerId}-{Guid.NewGuid():N}",
            cancellationToken);

        var result = await _stepFunctionService.WaitForCompletionAsync(
            executionArn,
            timeoutSeconds: 30,
            pollIntervalSeconds: 2,
            cancellationToken);

        return new WorkflowResult(
            ExecutionArn: result.ExecutionArn,
            Status: result.Status,
            Output: result.Output,
            Error: result.Error,
            Duration: result.Duration);
    }

    public async Task<WorkflowResult> ExecuteDharaAnalyzerWorkflowAsync(
        DharaAnalyzerWorkflowInput input,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Executing Dhara-Analyzer workflow for farmer {FarmerId}",
            input.FarmerId);

        var inputJson = JsonSerializer.Serialize(new
        {
            farmerId = input.FarmerId,
            documentData = input.DocumentData,
            preferredLanguage = input.PreferredLanguage
        });

        var executionArn = await _stepFunctionService.StartExecutionAsync(
            DharaAnalyzerStateMachineArn,
            inputJson,
            $"dhara-analyzer-{input.FarmerId}-{Guid.NewGuid():N}",
            cancellationToken);

        var result = await _stepFunctionService.WaitForCompletionAsync(
            executionArn,
            timeoutSeconds: 30,
            pollIntervalSeconds: 2,
            cancellationToken);

        return new WorkflowResult(
            ExecutionArn: result.ExecutionArn,
            Status: result.Status,
            Output: result.Output,
            Error: result.Error,
            Duration: result.Duration);
    }

    public async Task<WorkflowResult> ExecuteSowingOracleWorkflowAsync(
        SowingOracleWorkflowInput input,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Executing Sowing Oracle workflow for farmer {FarmerId}, crop {CropType}",
            input.FarmerId,
            input.CropType);

        var inputJson = JsonSerializer.Serialize(new
        {
            farmerId = input.FarmerId,
            location = input.Location,
            cropType = input.CropType
        });

        var executionArn = await _stepFunctionService.StartExecutionAsync(
            SowingOracleStateMachineArn,
            inputJson,
            $"sowing-oracle-{input.FarmerId}-{Guid.NewGuid():N}",
            cancellationToken);

        var result = await _stepFunctionService.WaitForCompletionAsync(
            executionArn,
            timeoutSeconds: 30,
            pollIntervalSeconds: 2,
            cancellationToken);

        return new WorkflowResult(
            ExecutionArn: result.ExecutionArn,
            Status: result.Status,
            Output: result.Output,
            Error: result.Error,
            Duration: result.Duration);
    }

    public async Task<WorkflowStatusUpdate> GetWorkflowStatusAsync(
        string executionArn,
        CancellationToken cancellationToken = default)
    {
        var status = await _stepFunctionService.GetExecutionStatusAsync(executionArn, cancellationToken);

        // Calculate progress percentage based on execution state
        var progressPercentage = status.Status switch
        {
            ExecutionState.Running => 50, // Approximate mid-point
            ExecutionState.Succeeded => 100,
            ExecutionState.Failed => 100,
            ExecutionState.TimedOut => 100,
            ExecutionState.Aborted => 100,
            _ => 0
        };

        // Extract current step from output or error (simplified)
        var currentStep = status.Status == ExecutionState.Running
            ? "Processing..."
            : status.Status.ToString();

        var message = status.Status switch
        {
            ExecutionState.Running => "Workflow is in progress",
            ExecutionState.Succeeded => "Workflow completed successfully",
            ExecutionState.Failed => $"Workflow failed: {status.Error}",
            ExecutionState.TimedOut => "Workflow timed out",
            ExecutionState.Aborted => "Workflow was aborted",
            _ => "Unknown status"
        };

        return new WorkflowStatusUpdate(
            ExecutionArn: status.ExecutionArn,
            Status: status.Status,
            CurrentStep: currentStep,
            ProgressPercentage: progressPercentage,
            Message: message);
    }
}

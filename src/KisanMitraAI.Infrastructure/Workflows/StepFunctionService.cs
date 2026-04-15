using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using KisanMitraAI.Core.Workflows;
using Microsoft.Extensions.Logging;

namespace KisanMitraAI.Infrastructure.Workflows;

/// <summary>
/// Implementation of Step Function execution service using AWS SDK
/// </summary>
public class StepFunctionService : IStepFunctionService
{
    private readonly IAmazonStepFunctions _stepFunctionsClient;
    private readonly ILogger<StepFunctionService> _logger;

    public StepFunctionService(
        IAmazonStepFunctions stepFunctionsClient,
        ILogger<StepFunctionService> logger)
    {
        _stepFunctionsClient = stepFunctionsClient ?? throw new ArgumentNullException(nameof(stepFunctionsClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> StartExecutionAsync(
        string stateMachineArn,
        string input,
        string? executionName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stateMachineArn))
            throw new ArgumentException("State machine ARN cannot be null or empty", nameof(stateMachineArn));

        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input cannot be null or empty", nameof(input));

        try
        {
            var request = new StartExecutionRequest
            {
                StateMachineArn = stateMachineArn,
                Input = input,
                Name = executionName ?? $"exec-{Guid.NewGuid()}"
            };

            _logger.LogInformation(
                "Starting Step Function execution for state machine {StateMachineArn} with name {ExecutionName}",
                stateMachineArn,
                request.Name);

            var response = await _stepFunctionsClient.StartExecutionAsync(request, cancellationToken);

            _logger.LogInformation(
                "Step Function execution started successfully. ExecutionArn: {ExecutionArn}",
                response.ExecutionArn);

            return response.ExecutionArn;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to start Step Function execution for state machine {StateMachineArn}",
                stateMachineArn);
            throw;
        }
    }

    public async Task<Core.Workflows.ExecutionStatus> GetExecutionStatusAsync(
        string executionArn,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(executionArn))
            throw new ArgumentException("Execution ARN cannot be null or empty", nameof(executionArn));

        try
        {
            var request = new DescribeExecutionRequest
            {
                ExecutionArn = executionArn
            };

            var response = await _stepFunctionsClient.DescribeExecutionAsync(request, cancellationToken);

            var status = MapExecutionStatus(response.Status);

            return new Core.Workflows.ExecutionStatus(
                ExecutionArn: response.ExecutionArn,
                StateMachineArn: response.StateMachineArn,
                Name: response.Name,
                Status: status,
                StartDate: (DateTimeOffset)response.StartDate,
                StopDate: response.StopDate.HasValue ? (DateTimeOffset)response.StopDate.Value : null,
                Input: response.Input,
                Output: response.Output,
                Error: response.Error,
                Cause: response.Cause);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get execution status for {ExecutionArn}",
                executionArn);
            throw;
        }
    }

    public async Task<ExecutionResult> WaitForCompletionAsync(
        string executionArn,
        int timeoutSeconds = 30,
        int pollIntervalSeconds = 2,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(executionArn))
            throw new ArgumentException("Execution ARN cannot be null or empty", nameof(executionArn));

        if (timeoutSeconds <= 0)
            throw new ArgumentException("Timeout must be greater than 0", nameof(timeoutSeconds));

        if (pollIntervalSeconds <= 0)
            throw new ArgumentException("Poll interval must be greater than 0", nameof(pollIntervalSeconds));

        _logger.LogInformation(
            "Waiting for Step Function execution {ExecutionArn} to complete (timeout: {Timeout}s, poll interval: {PollInterval}s)",
            executionArn,
            timeoutSeconds,
            pollIntervalSeconds);

        var startTime = DateTimeOffset.UtcNow;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var pollInterval = TimeSpan.FromSeconds(pollIntervalSeconds);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var elapsed = DateTimeOffset.UtcNow - startTime;
            if (elapsed >= timeout)
            {
                _logger.LogWarning(
                    "Step Function execution {ExecutionArn} timed out after {Elapsed}s",
                    executionArn,
                    elapsed.TotalSeconds);

                throw new TimeoutException(
                    $"Step Function execution timed out after {elapsed.TotalSeconds} seconds");
            }

            var status = await GetExecutionStatusAsync(executionArn, cancellationToken);

            // Check if execution is in a terminal state
            if (status.Status != ExecutionState.Running)
            {
                var duration = status.StopDate.HasValue
                    ? status.StopDate.Value - status.StartDate
                    : DateTimeOffset.UtcNow - status.StartDate;

                _logger.LogInformation(
                    "Step Function execution {ExecutionArn} completed with status {Status} in {Duration}s",
                    executionArn,
                    status.Status,
                    duration.TotalSeconds);

                return new ExecutionResult(
                    ExecutionArn: status.ExecutionArn,
                    Status: status.Status,
                    Output: status.Output,
                    Error: status.Error,
                    Cause: status.Cause,
                    Duration: duration);
            }

            // Log progress
            _logger.LogDebug(
                "Step Function execution {ExecutionArn} still running (elapsed: {Elapsed}s)",
                executionArn,
                elapsed.TotalSeconds);

            // Wait before next poll
            await Task.Delay(pollInterval, cancellationToken);
        }
    }

    public async Task StopExecutionAsync(
        string executionArn,
        string error = "UserRequested",
        string cause = "Execution stopped by user",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(executionArn))
            throw new ArgumentException("Execution ARN cannot be null or empty", nameof(executionArn));

        try
        {
            var request = new StopExecutionRequest
            {
                ExecutionArn = executionArn,
                Error = error,
                Cause = cause
            };

            _logger.LogInformation(
                "Stopping Step Function execution {ExecutionArn}",
                executionArn);

            await _stepFunctionsClient.StopExecutionAsync(request, cancellationToken);

            _logger.LogInformation(
                "Step Function execution {ExecutionArn} stopped successfully",
                executionArn);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to stop Step Function execution {ExecutionArn}",
                executionArn);
            throw;
        }
    }

    private static ExecutionState MapExecutionStatus(Amazon.StepFunctions.ExecutionStatus awsStatus)
    {
        if (awsStatus == Amazon.StepFunctions.ExecutionStatus.RUNNING)
            return ExecutionState.Running;
        if (awsStatus == Amazon.StepFunctions.ExecutionStatus.SUCCEEDED)
            return ExecutionState.Succeeded;
        if (awsStatus == Amazon.StepFunctions.ExecutionStatus.FAILED)
            return ExecutionState.Failed;
        if (awsStatus == Amazon.StepFunctions.ExecutionStatus.TIMED_OUT)
            return ExecutionState.TimedOut;
        if (awsStatus == Amazon.StepFunctions.ExecutionStatus.ABORTED)
            return ExecutionState.Aborted;
        
        throw new ArgumentException($"Unknown execution status: {awsStatus}");
    }
}

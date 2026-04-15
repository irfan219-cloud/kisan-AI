namespace KisanMitraAI.Core.Workflows;

/// <summary>
/// Service for executing and monitoring AWS Step Functions workflows
/// </summary>
public interface IStepFunctionService
{
    /// <summary>
    /// Starts execution of a Step Function workflow
    /// </summary>
    /// <param name="stateMachineArn">ARN of the Step Function state machine</param>
    /// <param name="input">Input data for the workflow as JSON string</param>
    /// <param name="executionName">Optional name for the execution</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution ARN</returns>
    Task<string> StartExecutionAsync(
        string stateMachineArn,
        string input,
        string? executionName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a Step Function execution
    /// </summary>
    /// <param name="executionArn">ARN of the execution</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution status information</returns>
    Task<ExecutionStatus> GetExecutionStatusAsync(
        string executionArn,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for a Step Function execution to complete and returns the result
    /// </summary>
    /// <param name="executionArn">ARN of the execution</param>
    /// <param name="timeoutSeconds">Maximum time to wait in seconds (default: 30)</param>
    /// <param name="pollIntervalSeconds">Interval between status checks in seconds (default: 2)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Final execution result</returns>
    Task<ExecutionResult> WaitForCompletionAsync(
        string executionArn,
        int timeoutSeconds = 30,
        int pollIntervalSeconds = 2,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a running Step Function execution
    /// </summary>
    /// <param name="executionArn">ARN of the execution</param>
    /// <param name="error">Error code</param>
    /// <param name="cause">Error cause</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StopExecutionAsync(
        string executionArn,
        string error = "UserRequested",
        string cause = "Execution stopped by user",
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the status of a Step Function execution
/// </summary>
public record ExecutionStatus(
    string ExecutionArn,
    string StateMachineArn,
    string Name,
    ExecutionState Status,
    DateTimeOffset StartDate,
    DateTimeOffset? StopDate,
    string? Input,
    string? Output,
    string? Error,
    string? Cause);

/// <summary>
/// Represents the final result of a Step Function execution
/// </summary>
public record ExecutionResult(
    string ExecutionArn,
    ExecutionState Status,
    string? Output,
    string? Error,
    string? Cause,
    TimeSpan Duration);

/// <summary>
/// Step Function execution states
/// </summary>
public enum ExecutionState
{
    Running,
    Succeeded,
    Failed,
    TimedOut,
    Aborted
}

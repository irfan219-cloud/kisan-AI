using Amazon.StepFunctions;
using KisanMitraAI.Core.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace KisanMitraAI.Infrastructure.Workflows;

/// <summary>
/// Extension methods for registering workflow services
/// </summary>
public static class WorkflowServiceExtensions
{
    /// <summary>
    /// Adds workflow orchestration services to the service collection
    /// </summary>
    public static IServiceCollection AddWorkflowServices(this IServiceCollection services)
    {
        // Register AWS Step Functions client
        services.AddSingleton<IAmazonStepFunctions>(sp =>
        {
            return new AmazonStepFunctionsClient();
        });

        // Register workflow services
        services.AddScoped<IStepFunctionService, StepFunctionService>();
        services.AddScoped<IWorkflowOrchestrator, WorkflowOrchestrator>();

        return services;
    }
}

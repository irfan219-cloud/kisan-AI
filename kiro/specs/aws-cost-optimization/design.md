# Design Document: AWS Cost Optimization

## Overview

This design specifies the technical implementation for reducing AWS infrastructure costs from $216-225/week to $5-8/week (97% reduction) for the Kisan Mitra AI prototype testing phase. The optimization strategy involves replacing high-cost managed services (RDS Aurora PostgreSQL, OpenSearch Serverless, VPC/NAT Gateway) with low-cost alternatives (DynamoDB, direct Bedrock API calls) while maintaining all 8 core use case functionalities.

The migration follows a surgical approach: remove expensive infrastructure components, add DynamoDB tables for data persistence, replace PostgreSQL repositories with DynamoDB implementations, and replace Knowledge Base RAG with direct Bedrock InvokeModel calls. All Lambda functions will run outside VPC to eliminate NAT Gateway costs.

This design targets a 1-week testing period with 1 user, making pay-per-request pricing optimal. The system will maintain functional parity with the current implementation while dramatically reducing operational costs.

## Architecture

### High-Level Architecture Changes

The cost optimization transforms the architecture from a traditional three-tier model with managed databases to a serverless-first model with NoSQL storage:

**Before (High-Cost Architecture):**
- API Gateway → Lambda (in VPC) → RDS Aurora PostgreSQL
- API Gateway → Lambda (in VPC) → OpenSearch Serverless (via Knowledge Base)
- VPC with NAT Gateway for Lambda internet access
- Total cost: $216-225/week

**After (Optimized Architecture):**
- API Gateway → Lambda (no VPC) → DynamoDB
- API Gateway → Lambda (no VPC) → Bedrock InvokeModel API
- No VPC, no NAT Gateway
- Total cost: $5-8/week

### Component Removal Strategy

1. **RDS Aurora PostgreSQL Removal**
   - Remove DatabaseCluster construct from DataStorageStack
   - Remove database secret from Secrets Manager
   - Remove database security group
   - Remove RDS subnet group
   - Remove RDS-related IAM permissions from Lambda execution role

2. **OpenSearch Serverless Removal**
   - Remove CfnCollection construct from AIServicesStack
   - Remove OpenSearch security policies (encryption, network, data access)
   - Remove Knowledge Base configuration (manual resource)
   - Remove OpenSearch-related IAM permissions from Lambda execution role

3. **VPC/NAT Gateway Removal**
   - Remove Vpc construct from DataStorageStack
   - Remove all subnet configurations (public, private, isolated)
   - Remove VPC configuration from all Lambda functions
   - Remove VPC-related security groups

### DynamoDB Table Design

Three new DynamoDB tables replace PostgreSQL functionality:

**1. FarmProfiles Table**
- Partition Key: FarmerId (String)
- Sort Key: FarmId (String)
- Attributes: AreaInAcres, SoilType, IrrigationType, CurrentCrops (JSON), Latitude, Longitude, CreatedAt, UpdatedAt
- Billing: PAY_PER_REQUEST
- Point-in-time recovery: Enabled
- Access pattern: Query by FarmerId to get all farms for a farmer

**2. RegenerativePlans Table**
- Partition Key: FarmerId (String)
- Sort Key: PlanId (String)
- Attributes: FarmId, SoilData (JSON), Recommendations (JSON), CarbonSequestrationEstimate, ConfidenceScore, CreatedAt, UpdatedAt
- Billing: PAY_PER_REQUEST
- Point-in-time recovery: Enabled
- Access pattern: Query by FarmerId to get all plans for a farmer

**3. AuditLogs Table**
- Partition Key: FarmerId (String)
- Sort Key: Timestamp (Number - Unix epoch milliseconds)
- Attributes: Action, ResourceType, ResourceId, Details, IpAddress, UserAgent, Status
- Billing: PAY_PER_REQUEST
- Point-in-time recovery: Enabled
- Access pattern: Query by FarmerId with optional timestamp range filtering

### Direct Bedrock Integration

Replace Knowledge Base RetrieveAndGenerate API with direct InvokeModel API:

**Before:**
```
Query → Knowledge Base → OpenSearch (vector search) → Bedrock (generation) → Response
```

**After:**
```
Query → Bedrock InvokeModel (with agricultural context in prompt) → Response
```

The new approach embeds agricultural domain knowledge directly in the prompt rather than retrieving it from a vector database. For a 1-week prototype with limited queries, this provides sufficient quality while eliminating $161/week in OpenSearch costs.

### Lambda Execution Environment

All Lambda functions will execute outside VPC:
- Direct access to AWS services (DynamoDB, Bedrock, S3, Timestream) via AWS PrivateLink
- No NAT Gateway required for internet access
- Reduced cold start times (no ENI attachment)
- Simplified IAM permissions (no VPC-related policies)

## Components and Interfaces

### Modified CDK Stacks

**DataStorageStack Changes:**
```csharp
// Remove:
- CreateVPC() method
- CreateRDSCluster() method
- Vpc property
- RDSCluster property
- DatabaseSecret property

// Add:
- CreateDynamoDBTables() method creates FarmProfiles, RegenerativePlans, AuditLogs tables
- All tables use PAY_PER_REQUEST billing mode
- All tables enable point-in-time recovery

// Keep:
- CreateTimestreamDatabase() - unchanged
- CreateS3Buckets() - unchanged
- Existing DynamoDB tables (UserProfiles, SessionData, etc.) - unchanged
```

**AIServicesStack Changes:**
```csharp
// Remove:
- CreateOpenSearchCollection() method
- OpenSearchCollection property
- BedrockServiceRole property (specific to Knowledge Base)
- All OpenSearch security policies

// Modify:
- ConfigureAIServiceAccess() - remove OpenSearch permissions, keep Bedrock InvokeModel permissions

// Keep:
- CreateKnowledgeBaseBucket() - can be retained for future use or removed
- Bedrock model ARN outputs
```

**ComputeStack Changes:**
```csharp
// Modify CreateLambdaFunctions():
- Remove VPC configuration from all Function constructs
- Remove VpcConfig property
- Remove SecurityGroups property
- Remove SubnetSelection property

// Modify CreateIAMRoles():
- Remove RDS-related permissions from LambdaExecutionRole
- Remove OpenSearch-related permissions from LambdaExecutionRole
- Remove VPC-related permissions (ec2:CreateNetworkInterface, etc.)
- Add DynamoDB permissions for FarmProfiles, RegenerativePlans, AuditLogs tables

// Keep:
- All Lambda function definitions
- API Gateway configuration
- Cognito configuration
```

### Repository Implementations

**DynamoDB FarmRepository:**
```csharp
public class DynamoDBFarmRepository : IFarmRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName = "FarmProfiles";

    // CreateAsync: PutItem with FarmerId as PK, FarmId as SK
    // GetByIdAsync: Query with FarmerId and FarmId
    // GetByFarmerIdAsync: Query with FarmerId (returns all farms)
    // UpdateAsync: UpdateItem with FarmerId and FarmId
    // DeleteAsync: DeleteItem with FarmerId and FarmId
}
```

**DynamoDB AuditLogRepository:**
```csharp
public class DynamoAuditLogRepository : IAuditLogRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName = "AuditLogs";

    // LogActionAsync: PutItem with FarmerId as PK, Timestamp as SK
    // GetAuditTrailAsync: Query with FarmerId, optional timestamp range filter
}
```

**Direct Bedrock Knowledge Base Service:**
```csharp
public class DirectBedrockKnowledgeBaseService : IKnowledgeBaseService
{
    private readonly IAmazonBedrockRuntime _bedrockRuntime;
    private const string ModelId = "anthropic.claude-3-haiku-20240307-v1:0"; // Cost-optimized model

    // QueryKnowledgeBaseAsync:
    // 1. Build prompt with agricultural domain context
    // 2. Invoke Bedrock InvokeModel API
    // 3. Parse response and format as KnowledgeBaseResponse
    // 4. Generate synthetic citations (indicate direct AI response)
    // 5. Calculate confidence score based on response quality indicators
}
```

### Dependency Injection Configuration

**Program.cs Changes:**
```csharp
// Remove:
services.AddDbContext<KisanMitraDbContext>(options =>
    options.UseNpgsql(connectionString));
services.AddScoped<IFarmRepository, PostgreSQLFarmRepository>();
services.AddScoped<IAuditLogRepository, PostgreSQLAuditLogRepository>();
services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
services.AddSingleton<IAmazonBedrockAgentRuntime, AmazonBedrockAgentRuntimeClient>();

// Add:
services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
services.AddSingleton<IAmazonBedrockRuntime, AmazonBedrockRuntimeClient>();
services.AddScoped<IFarmRepository, DynamoDBFarmRepository>();
services.AddScoped<IAuditLogRepository, DynamoAuditLogRepository>();
services.AddScoped<IKnowledgeBaseService, DirectBedrockKnowledgeBaseService>();
```

## Data Models

### DynamoDB Item Structures

**FarmProfiles Item:**
```json
{
  "FarmerId": "farmer-123",
  "FarmId": "farm-456",
  "AreaInAcres": 5.5,
  "SoilType": "Loamy",
  "IrrigationType": "Drip",
  "CurrentCrops": "[\"Wheat\", \"Mustard\"]",
  "Latitude": 28.6139,
  "Longitude": 77.2090,
  "CreatedAt": "2024-01-15T10:30:00Z",
  "UpdatedAt": "2024-01-20T14:45:00Z"
}
```

**RegenerativePlans Item:**
```json
{
  "FarmerId": "farmer-123",
  "PlanId": "plan-789",
  "FarmId": "farm-456",
  "SoilData": "{\"pH\": 6.5, \"nitrogen\": 280, \"phosphorus\": 45, \"potassium\": 210, \"organicCarbon\": 0.65}",
  "Recommendations": "[{\"practice\": \"Cover Cropping\", \"details\": \"...\"}]",
  "CarbonSequestrationEstimate": 2.5,
  "ConfidenceScore": 0.85,
  "CreatedAt": "2024-01-20T15:00:00Z",
  "UpdatedAt": "2024-01-20T15:00:00Z"
}
```

**AuditLogs Item:**
```json
{
  "FarmerId": "farmer-123",
  "Timestamp": 1705761600000,
  "Action": "CREATE_FARM",
  "ResourceType": "Farm",
  "ResourceId": "farm-456",
  "Details": "Created new farm profile",
  "IpAddress": "203.0.113.42",
  "UserAgent": "Mozilla/5.0...",
  "Status": "Success"
}
```

### Data Migration Strategy

For the 1-week prototype testing phase with 1 user:
- No data migration required (fresh deployment)
- If existing data needs preservation:
  1. Export PostgreSQL data to JSON
  2. Transform to DynamoDB item format
  3. Use BatchWriteItem to load into DynamoDB tables
  4. Verify data integrity
  5. Switch application to use DynamoDB repositories

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*


### Property Reflection

After analyzing all acceptance criteria, I identified the following redundancies and consolidations:

**Redundancies Identified:**
1. Requirements 8.1-8.5 (voice query workflow) can be consolidated into a single end-to-end workflow property
2. Requirements 9.1-9.5 (quality grading workflow) can be consolidated into a single workflow property
3. Requirements 10.1-10.5 (soil analysis workflow) can be consolidated into a single workflow property
4. Requirements 11.1-11.5 (planting advisory workflow) can be consolidated into a single workflow property
5. Requirements 12.1-12.5 (authentication workflow) can be consolidated into a single workflow property
6. Requirements 13.1-13.5 (historical analytics workflow) can be consolidated into a single workflow property
7. Requirements 14.1-14.5 (offline sync workflow) can be consolidated into a single workflow property
8. Requirements 16.1-16.5 (successful API responses) test the same pattern across different endpoints - can be consolidated
9. Requirements 3.4, 3.5, 3.6 all test properties across all three new DynamoDB tables - these are distinct and valuable
10. Requirements 4.3, 4.4, 4.5 test data storage to correct tables - these can be consolidated into one property about correct table routing
11. Requirements 4.6, 4.7 test correct query operations - these can be consolidated into one property about using Query vs Scan

**Consolidation Decisions:**
- Workflow properties (8.x, 9.x, 10.x, 11.x, 12.x, 13.x, 14.x): Consolidate each workflow into a single comprehensive property that validates the complete sequence
- API response properties (16.1-16.5): Consolidate into one property about successful responses for valid inputs
- Data storage properties (4.3-4.5): Consolidate into one property about correct table routing
- Query operation properties (4.6-4.7): Consolidate into one property about using efficient query operations

**Properties to Keep Separate:**
- Infrastructure configuration properties (3.4, 3.5, 3.6): Each validates a different aspect of table configuration
- Model selection property (5.3): Validates cost optimization through correct model choice
- Prompt context property (5.2): Validates agricultural domain knowledge inclusion
- Response structure property (5.4): Validates backward compatibility

### Property 1: DynamoDB Tables Use Pay-Per-Request Billing

*For all* three new DynamoDB tables (FarmProfiles, RegenerativePlans, AuditLogs), the billing mode should be configured as PAY_PER_REQUEST to minimize costs for low-volume prototype testing.

**Validates: Requirements 3.4**

### Property 2: Lambda Execution Role Has DynamoDB Permissions

*For all* three new DynamoDB tables (FarmProfiles, RegenerativePlans, AuditLogs), the Lambda execution role should have both read and write permissions (GetItem, PutItem, UpdateItem, DeleteItem, Query, Scan) to enable full data access functionality.

**Validates: Requirements 3.5**

### Property 3: DynamoDB Tables Enable Point-in-Time Recovery

*For all* three new DynamoDB tables (FarmProfiles, RegenerativePlans, AuditLogs), point-in-time recovery should be enabled to protect against accidental data loss during prototype testing.

**Validates: Requirements 3.6**

### Property 4: Repository Operations Write to Correct Tables

*For any* data storage operation, the repository should write to the correct DynamoDB table: farm profiles to FarmProfiles, regenerative plans to RegenerativePlans, and audit logs to AuditLogs.

**Validates: Requirements 4.3, 4.4, 4.5**

### Property 5: Repository Queries Use Efficient Operations

*For any* query operation by partition key (with optional sort key condition), the repository should use DynamoDB Query operation rather than Scan operation to minimize read costs and latency.

**Validates: Requirements 4.6, 4.7**

### Property 6: Advisory Prompts Include Agricultural Context

*For any* advisory query processed by the Knowledge Base service, the prompt sent to Bedrock should include agricultural domain context (crop types, regional practices, soil health principles) to compensate for the absence of vector search retrieval.

**Validates: Requirements 5.2**

### Property 7: Advisory Service Uses Cost-Optimized Model

*For any* advisory query processed by the Knowledge Base service, the Bedrock InvokeModel call should use the Claude 3 Haiku model ID (anthropic.claude-3-haiku-20240307-v1:0) to minimize per-request costs while maintaining acceptable response quality.

**Validates: Requirements 5.3**

### Property 8: Advisory Responses Maintain Structure Compatibility

*For any* advisory response generated by the direct Bedrock service, the response structure should contain the same fields (Answer, Citations, ConfidenceScore) as the previous Knowledge Base RAG implementation to maintain backward compatibility with client applications.

**Validates: Requirements 5.4**

### Property 9: Voice Query Workflow Completes Successfully

*For any* valid audio file upload, the voice query workflow should complete all steps in sequence: (1) transcribe audio using Amazon Transcribe, (2) generate response using Bedrock, (3) synthesize audio using Amazon Polly, (4) store query history in DynamoDB, and (5) return audio response URL to the client.

**Validates: Requirements 8.1, 8.2, 8.3, 8.4, 8.5**

### Property 10: Quality Grading Workflow Completes Successfully

*For any* valid produce image upload, the quality grading workflow should complete all steps in sequence: (1) analyze image using Amazon Rekognition, (2) classify quality grade using custom labels, (3) store grading history in Timestream, (4) retrieve historical data for trends, and (5) return quality grade and confidence score to the client.

**Validates: Requirements 9.1, 9.2, 9.3, 9.4, 9.5**

### Property 11: Soil Analysis Workflow Completes Successfully

*For any* valid soil test document upload, the soil analysis workflow should complete all steps in sequence: (1) extract text using Amazon Textract, (2) parse soil parameters using Bedrock, (3) generate regenerative plan using Bedrock, (4) store plan in RegenerativePlans DynamoDB table, and (5) return regenerative plan to the client.

**Validates: Requirements 10.1, 10.2, 10.3, 10.4, 10.5**

### Property 12: Planting Advisory Workflow Completes Successfully

*For any* valid planting advice request, the planting advisory workflow should complete all steps in sequence: (1) retrieve weather data from Timestream, (2) retrieve soil data from DynamoDB, (3) generate recommendations using Bedrock, (4) calculate confidence scores, and (5) return recommendations with confidence scores to the client.

**Validates: Requirements 11.1, 11.2, 11.3, 11.4, 11.5**

### Property 13: Authentication Workflow Completes Successfully

*For any* valid login credentials submission, the authentication workflow should complete all steps in sequence: (1) authenticate using Amazon Cognito, (2) return JWT tokens on success, (3) validate JWT tokens for protected resources, and (4) store session data in DynamoDB.

**Validates: Requirements 12.1, 12.2, 12.3, 12.5**

### Property 14: Historical Analytics Workflow Completes Successfully

*For any* valid historical data request, the analytics workflow should complete all steps in sequence: (1) query Timestream for time-series data, (2) aggregate data by specified time period, (3) calculate statistical insights, (4) format data for chart visualization, and (5) return formatted chart data to the client.

**Validates: Requirements 13.1, 13.2, 13.3, 13.4, 13.5**

### Property 15: Offline Sync Workflow Completes Successfully

*For any* farmer reconnection after offline period, the sync workflow should complete all steps in sequence: (1) retrieve queued operations from DynamoDB, (2) process operations in chronological order, (3) update operation status in DynamoDB for each operation, and (4) return sync status to the client.

**Validates: Requirements 14.1, 14.2, 14.3, 14.5**

### Property 16: Valid API Requests Return Success Responses

*For any* valid API request to any endpoint (voice query, quality grading, soil analysis, planting advisory, historical data) with proper authentication and valid input, the Lambda function should return a 200 status code with the appropriate response payload (audio URL, quality grade, regenerative plan, recommendations, or chart data).

**Validates: Requirements 16.1, 16.2, 16.3, 16.4, 16.5**

## Error Handling

### Infrastructure Deployment Errors

**CDK Synthesis Errors:**
- If CDK stack definitions contain invalid constructs, the `cdk synth` command will fail with descriptive error messages
- Validation: Run `cdk synth` before deployment to catch configuration errors early
- Recovery: Fix stack definitions based on error messages and re-synthesize

**CloudFormation Deployment Errors:**
- If resource creation fails (e.g., table name conflicts, IAM permission issues), CloudFormation will automatically rollback
- Validation: Use CloudFormation change sets to preview changes before deployment
- Recovery: Review CloudFormation error messages, fix issues, and redeploy

**Resource Deletion Errors:**
- If resources have dependencies or deletion protection, removal may fail
- Validation: Check resource dependencies before removal
- Recovery: Manually remove dependencies or disable deletion protection, then retry

### Repository Operation Errors

**DynamoDB Service Errors:**
- ProvisionedThroughputExceededException: Should not occur with PAY_PER_REQUEST billing
- ResourceNotFoundException: Table doesn't exist - indicates deployment issue
- ValidationException: Invalid item structure - indicates code bug
- Recovery: Log error details, return descriptive error to client, alert monitoring system

**Data Consistency Errors:**
- Conditional check failures: Item already exists or version mismatch
- Recovery: Retry with exponential backoff, or return conflict error to client

**Query Performance Issues:**
- Large result sets causing timeouts
- Mitigation: Implement pagination using LastEvaluatedKey
- Recovery: Return partial results with continuation token

### Bedrock API Errors

**Model Invocation Errors:**
- ThrottlingException: Rate limit exceeded
- Recovery: Implement exponential backoff retry with jitter
- ModelTimeoutException: Request took too long
- Recovery: Reduce prompt size or switch to faster model

**Content Filtering Errors:**
- Content policy violations in prompts or responses
- Recovery: Log violation details, return sanitized error message to client

**Model Availability Errors:**
- Model not available in region
- Recovery: Fall back to alternative model or return service unavailable error

### Workflow Orchestration Errors

**Partial Workflow Failures:**
- If step 3 of 5 fails, previous steps may have side effects (stored data, consumed API calls)
- Strategy: Implement idempotent operations where possible
- Recovery: Log failure point, allow retry from failed step, clean up partial state if needed

**Timeout Errors:**
- Lambda function timeout (5-10 minutes)
- Mitigation: Break long workflows into smaller steps using Step Functions
- Recovery: Return timeout error with partial results if available

**External Service Failures:**
- Transcribe, Textract, Rekognition, Polly service errors
- Recovery: Retry with exponential backoff, fall back to degraded functionality, or return service unavailable error

### Authentication and Authorization Errors

**Cognito Errors:**
- UserNotFoundException: User doesn't exist
- NotAuthorizedException: Invalid credentials
- UserNotConfirmedException: Phone number not verified
- Recovery: Return appropriate HTTP status code (401, 403) with descriptive message

**JWT Validation Errors:**
- Token expired: Return 401 with token refresh instructions
- Token invalid: Return 401 with re-authentication requirement
- Token missing: Return 401 with authentication requirement

### Data Migration Errors

**Export Errors:**
- PostgreSQL connection failures during export
- Recovery: Retry connection, use read replica if available

**Transform Errors:**
- Data format incompatibilities between PostgreSQL and DynamoDB
- Recovery: Log problematic records, apply data transformations, validate output

**Import Errors:**
- BatchWriteItem failures due to item size limits (400 KB)
- Recovery: Split large items, store overflow data in S3, reference from DynamoDB

## Testing Strategy

### Dual Testing Approach

This feature requires both unit tests and property-based tests to ensure comprehensive coverage:

**Unit Tests:**
- Verify specific infrastructure configurations (table schemas, IAM policies)
- Test error handling for specific failure scenarios
- Validate data transformation logic (PostgreSQL to DynamoDB format)
- Test DI container configuration
- Verify CloudFormation template structure

**Property-Based Tests:**
- Verify repository operations work correctly for all valid inputs
- Test workflow orchestration with randomized valid data
- Validate query operation efficiency across different data patterns
- Test API responses for various valid request combinations

### Property-Based Testing Configuration

**Framework:** Use FsCheck for C# property-based testing (already used in the project)

**Test Configuration:**
- Minimum 100 iterations per property test
- Each test tagged with: `Feature: aws-cost-optimization, Property {number}: {property_text}`
- Use custom generators for domain objects (FarmProfile, RegenerativePlan, AuditLogEntry)

**Property Test Examples:**

```csharp
[Property(MaxTest = 100)]
[Tag("Feature: aws-cost-optimization, Property 4: Repository Operations Write to Correct Tables")]
public Property RepositoryWritesToCorrectTable()
{
    return Prop.ForAll(
        Arb.Generate<FarmProfile>(),
        async farmProfile =>
        {
            // Arrange
            var repository = new DynamoDBFarmRepository(_dynamoDb, _logger);
            
            // Act
            await repository.CreateAsync(farmProfile);
            
            // Assert
            var item = await GetDynamoDBItem("FarmProfiles", farmProfile.FarmerId, farmProfile.FarmId);
            return item != null && item["FarmId"].S == farmProfile.FarmId;
        });
}

[Property(MaxTest = 100)]
[Tag("Feature: aws-cost-optimization, Property 5: Repository Queries Use Efficient Operations")]
public Property RepositoryUsesQueryNotScan()
{
    return Prop.ForAll(
        Arb.Generate<string>(), // FarmerId
        async farmerId =>
        {
            // Arrange
            var repository = new DynamoDBFarmRepository(_dynamoDb, _logger);
            var mockDynamoDb = new Mock<IAmazonDynamoDB>();
            
            // Act
            await repository.GetByFarmerIdAsync(farmerId);
            
            // Assert
            mockDynamoDb.Verify(db => db.QueryAsync(It.IsAny<QueryRequest>(), default), Times.Once);
            mockDynamoDb.Verify(db => db.ScanAsync(It.IsAny<ScanRequest>(), default), Times.Never);
            return true;
        });
}

[Property(MaxTest = 100)]
[Tag("Feature: aws-cost-optimization, Property 6: Advisory Prompts Include Agricultural Context")]
public Property AdvisoryPromptsIncludeContext()
{
    return Prop.ForAll(
        Arb.Generate<string>(), // Query
        async query =>
        {
            // Arrange
            var service = new DirectBedrockKnowledgeBaseService(_bedrockRuntime, _logger);
            var mockBedrock = new Mock<IAmazonBedrockRuntime>();
            string capturedPrompt = null;
            mockBedrock.Setup(b => b.InvokeModelAsync(It.IsAny<InvokeModelRequest>(), default))
                .Callback<InvokeModelRequest, CancellationToken>((req, ct) => 
                {
                    capturedPrompt = Encoding.UTF8.GetString(req.Body.ToArray());
                });
            
            // Act
            await service.QueryKnowledgeBaseAsync(query, "", 5, default);
            
            // Assert
            return capturedPrompt.Contains("agricultural") || 
                   capturedPrompt.Contains("farming") ||
                   capturedPrompt.Contains("crop");
        });
}
```

### Unit Test Coverage

**Infrastructure Tests:**
- Verify CloudFormation template doesn't contain RDS resources
- Verify CloudFormation template doesn't contain OpenSearch resources
- Verify CloudFormation template doesn't contain VPC resources
- Verify DynamoDB tables have correct schema (partition key, sort key, attributes)
- Verify Lambda functions don't have VPC configuration
- Verify IAM policies contain DynamoDB permissions
- Verify IAM policies don't contain RDS or OpenSearch permissions

**Repository Tests:**
- Test CreateAsync stores item with correct keys
- Test GetByIdAsync retrieves correct item
- Test GetByFarmerIdAsync returns all items for farmer
- Test UpdateAsync modifies existing item
- Test DeleteAsync removes item
- Test error handling for DynamoDB exceptions

**Service Tests:**
- Test DirectBedrockKnowledgeBaseService builds correct prompts
- Test DirectBedrockKnowledgeBaseService uses correct model ID
- Test DirectBedrockKnowledgeBaseService formats responses correctly
- Test error handling for Bedrock exceptions

**Integration Tests:**
- Deploy infrastructure to test AWS account
- Verify all DynamoDB tables are created
- Verify Lambda functions are deployed without VPC
- Test complete workflows end-to-end
- Verify API endpoints return correct responses
- Measure actual AWS costs over test period

### Test Data Management

**Generators:**
- FarmProfile generator: Random FarmerId, FarmId, coordinates, soil types, irrigation types
- RegenerativePlan generator: Random plan data with valid soil parameters
- AuditLogEntry generator: Random actions, timestamps, resource types

**Test Isolation:**
- Use unique table name prefixes for each test run
- Clean up test data after each test
- Use separate AWS account for testing

### Performance Testing

**Load Testing:**
- Not critical for 1-week prototype with 1 user
- If needed, use Artillery or k6 to simulate concurrent requests
- Verify Lambda cold start times are acceptable (<3 seconds)
- Verify DynamoDB query latency is acceptable (<100ms)

**Cost Monitoring:**
- Enable AWS Cost Explorer
- Set up billing alerts for daily costs >$2
- Track costs by service (DynamoDB, Lambda, Bedrock, etc.)
- Verify total weekly cost is within $5-8 target


# Requirements Document

## Introduction

This document specifies requirements for implementing an ultra-low-cost AWS infrastructure configuration for the Kisan Mitra AI prototype testing phase. The system must reduce infrastructure costs from $216-225/week to $5-8/week (97% cost reduction) while maintaining all core use case functionality for 1-week testing with 1 user. This will be achieved by replacing RDS Aurora PostgreSQL and OpenSearch Serverless with DynamoDB tables and direct Bedrock API calls, while removing VPC/NAT Gateway dependencies.

## Glossary

- **Infrastructure_System**: The AWS CDK stack definitions that provision cloud resources
- **Data_Storage_Stack**: CDK stack managing database and storage resources
- **AI_Services_Stack**: CDK stack managing AI/ML service configurations
- **Compute_Stack**: CDK stack managing Lambda functions and execution roles
- **Farm_Repository**: Data access layer for farm profile operations
- **Audit_Log_Repository**: Data access layer for audit log operations
- **Knowledge_Base_Service**: Service providing RAG-based advisory responses
- **Advisory_Service**: Service providing farming guidance using AI
- **DynamoDB_Table**: AWS NoSQL database table for storing application data
- **Lambda_Function**: AWS serverless compute function
- **Bedrock_Service**: AWS managed generative AI service
- **VPC**: Virtual Private Cloud network isolation layer
- **NAT_Gateway**: Network address translation gateway for VPC internet access
- **RDS_Aurora**: AWS managed relational database service
- **OpenSearch_Serverless**: AWS managed search and analytics service
- **Dependency_Injection_Container**: Application service registration and resolution system

## Requirements

### Requirement 1: Remove High-Cost Database Infrastructure

**User Story:** As a cost-conscious developer, I want to remove RDS Aurora PostgreSQL from the infrastructure, so that I can eliminate $42-48/week in database costs.

#### Acceptance Criteria

1. THE Infrastructure_System SHALL remove the RDS Aurora PostgreSQL cluster definition from Data_Storage_Stack
2. THE Infrastructure_System SHALL remove the VPC definition from Data_Storage_Stack
3. THE Infrastructure_System SHALL remove the NAT_Gateway definition from Data_Storage_Stack
4. THE Infrastructure_System SHALL remove RDS connection permissions from Lambda execution roles in Compute_Stack
5. THE Infrastructure_System SHALL remove VPC configuration from all Lambda_Function definitions in Compute_Stack

### Requirement 2: Remove High-Cost Search Infrastructure

**User Story:** As a cost-conscious developer, I want to remove OpenSearch Serverless from the infrastructure, so that I can eliminate $161/week in search service costs.

#### Acceptance Criteria

1. THE Infrastructure_System SHALL remove the OpenSearch Serverless collection definition from AI_Services_Stack
2. THE Infrastructure_System SHALL remove OpenSearch access permissions from Lambda execution roles in Compute_Stack
3. THE Infrastructure_System SHALL remove Knowledge Base for Amazon Bedrock configuration from AI_Services_Stack

### Requirement 3: Add DynamoDB Tables for Data Storage

**User Story:** As a developer, I want to add DynamoDB tables to replace RDS functionality, so that I can store application data at minimal cost.

#### Acceptance Criteria

1. THE Infrastructure_System SHALL create a DynamoDB_Table named "FarmProfiles" with partition key "FarmerId" (String) and sort key "FarmId" (String)
2. THE Infrastructure_System SHALL create a DynamoDB_Table named "RegenerativePlans" with partition key "FarmerId" (String) and sort key "PlanId" (String)
3. THE Infrastructure_System SHALL create a DynamoDB_Table named "AuditLogs" with partition key "FarmerId" (String) and sort key "Timestamp" (Number)
4. THE Infrastructure_System SHALL configure all DynamoDB tables with PAY_PER_REQUEST billing mode
5. THE Infrastructure_System SHALL grant read and write permissions for all three DynamoDB tables to Lambda execution roles in Compute_Stack
6. FOR ALL DynamoDB tables, THE Infrastructure_System SHALL enable point-in-time recovery

### Requirement 4: Replace PostgreSQL Repositories with DynamoDB

**User Story:** As a developer, I want to replace PostgreSQL data access implementations with DynamoDB implementations, so that the application can function without RDS.

#### Acceptance Criteria

1. THE Farm_Repository SHALL implement data access operations using DynamoDB SDK instead of Entity Framework
2. THE Audit_Log_Repository SHALL implement data access operations using DynamoDB SDK instead of Entity Framework
3. WHEN storing farm profile data, THE Farm_Repository SHALL write to the FarmProfiles DynamoDB_Table
4. WHEN storing regenerative plan data, THE Farm_Repository SHALL write to the RegenerativePlans DynamoDB_Table
5. WHEN storing audit log data, THE Audit_Log_Repository SHALL write to the AuditLogs DynamoDB_Table
6. WHEN querying farm data by FarmerId, THE Farm_Repository SHALL use DynamoDB Query operation with partition key
7. WHEN querying audit logs by FarmerId and time range, THE Audit_Log_Repository SHALL use DynamoDB Query operation with partition key and sort key condition

### Requirement 5: Replace Knowledge Base RAG with Direct Bedrock Calls

**User Story:** As a developer, I want to replace OpenSearch-based Knowledge Base RAG with direct Bedrock API calls, so that the advisory service can function without OpenSearch.

#### Acceptance Criteria

1. THE Knowledge_Base_Service SHALL invoke Bedrock_Service InvokeModel API directly instead of using Knowledge Base RetrieveAndGenerate API
2. WHEN generating advisory responses, THE Knowledge_Base_Service SHALL include agricultural domain context in the prompt text
3. WHEN generating advisory responses, THE Knowledge_Base_Service SHALL use the Claude 3 Haiku model for cost optimization
4. THE Knowledge_Base_Service SHALL format responses to maintain the same structure as Knowledge Base RAG responses
5. IF Bedrock_Service returns an error, THEN THE Knowledge_Base_Service SHALL return a descriptive error message

### Requirement 6: Update Dependency Injection Configuration

**User Story:** As a developer, I want to update service registrations in the application startup, so that the new DynamoDB implementations are used instead of PostgreSQL implementations.

#### Acceptance Criteria

1. THE Dependency_Injection_Container SHALL register DynamoDB-based Farm_Repository implementation instead of PostgreSQL-based implementation
2. THE Dependency_Injection_Container SHALL register DynamoDB-based Audit_Log_Repository implementation instead of PostgreSQL-based implementation
3. THE Dependency_Injection_Container SHALL register direct Bedrock-based Knowledge_Base_Service implementation instead of OpenSearch-based implementation
4. THE Dependency_Injection_Container SHALL remove Entity Framework DbContext registration
5. THE Dependency_Injection_Container SHALL register AWS DynamoDB client with appropriate credentials and region configuration

### Requirement 7: Remove Entity Framework Dependencies

**User Story:** As a developer, I want to remove Entity Framework and database migration code, so that the application has no PostgreSQL dependencies.

#### Acceptance Criteria

1. THE Infrastructure_System SHALL remove KisanMitraDbContext class file
2. THE Infrastructure_System SHALL remove all Entity Framework migration files from the Data/Migrations directory
3. THE Infrastructure_System SHALL remove Entity Framework NuGet package references from project files
4. THE Infrastructure_System SHALL remove database connection string configuration from application settings

### Requirement 8: Maintain Voice Query Functionality

**User Story:** As a farmer, I want to submit voice queries and receive audio responses, so that I can get farming advice in my preferred language.

#### Acceptance Criteria

1. WHEN a farmer uploads an audio file, THE Lambda_Function SHALL transcribe it using Amazon Transcribe
2. WHEN transcription completes, THE Lambda_Function SHALL generate a response using Bedrock_Service
3. WHEN response generation completes, THE Lambda_Function SHALL synthesize audio using Amazon Polly
4. THE Lambda_Function SHALL store query history in DynamoDB tables
5. THE Lambda_Function SHALL return the audio response URL to the client

### Requirement 9: Maintain Quality Grading Functionality

**User Story:** As a farmer, I want to upload produce images and receive quality grades, so that I can assess market value.

#### Acceptance Criteria

1. WHEN a farmer uploads a produce image, THE Lambda_Function SHALL analyze it using Amazon Rekognition
2. WHEN image analysis completes, THE Lambda_Function SHALL classify quality grade using custom labels
3. THE Lambda_Function SHALL store grading history in Timestream database
4. THE Lambda_Function SHALL retrieve historical grading data from Timestream for trend analysis
5. THE Lambda_Function SHALL return quality grade and confidence score to the client

### Requirement 10: Maintain Soil Analysis Functionality

**User Story:** As a farmer, I want to upload soil test reports and receive regenerative farming plans, so that I can improve soil health.

#### Acceptance Criteria

1. WHEN a farmer uploads a soil test document, THE Lambda_Function SHALL extract text using Amazon Textract
2. WHEN text extraction completes, THE Lambda_Function SHALL parse soil parameters using Bedrock_Service
3. WHEN soil parameters are parsed, THE Lambda_Function SHALL generate a regenerative plan using Bedrock_Service
4. THE Lambda_Function SHALL store the regenerative plan in the RegenerativePlans DynamoDB_Table
5. THE Lambda_Function SHALL return the regenerative plan to the client

### Requirement 11: Maintain Planting Advisory Functionality

**User Story:** As a farmer, I want to receive planting recommendations based on weather and soil data, so that I can optimize crop yields.

#### Acceptance Criteria

1. WHEN a farmer requests planting advice, THE Lambda_Function SHALL retrieve weather data from Timestream database
2. WHEN weather data is retrieved, THE Lambda_Function SHALL retrieve soil data from DynamoDB tables
3. WHEN all data is collected, THE Lambda_Function SHALL generate planting recommendations using Bedrock_Service
4. THE Lambda_Function SHALL calculate confidence scores for recommendations
5. THE Lambda_Function SHALL return planting recommendations with confidence scores to the client

### Requirement 12: Maintain Authentication Functionality

**User Story:** As a farmer, I want to securely log in to the application, so that my data is protected.

#### Acceptance Criteria

1. WHEN a farmer submits login credentials, THE Lambda_Function SHALL authenticate using Amazon Cognito
2. WHEN authentication succeeds, THE Lambda_Function SHALL return JWT tokens to the client
3. WHEN a farmer requests a protected resource, THE Lambda_Function SHALL validate the JWT token
4. IF the JWT token is invalid or expired, THEN THE Lambda_Function SHALL return an authentication error
5. THE Lambda_Function SHALL store session data in DynamoDB tables

### Requirement 13: Maintain Historical Analytics Functionality

**User Story:** As a farmer, I want to view historical trends for my farm data, so that I can make informed decisions.

#### Acceptance Criteria

1. WHEN a farmer requests historical data, THE Lambda_Function SHALL query Timestream database for time-series data
2. THE Lambda_Function SHALL aggregate data by time period (daily, weekly, monthly)
3. THE Lambda_Function SHALL calculate statistical insights (averages, trends, anomalies)
4. THE Lambda_Function SHALL format data for chart visualization
5. THE Lambda_Function SHALL return formatted chart data to the client

### Requirement 14: Maintain Offline Sync Functionality

**User Story:** As a farmer with intermittent connectivity, I want my offline actions to sync when I reconnect, so that no data is lost.

#### Acceptance Criteria

1. WHEN a farmer reconnects after being offline, THE Lambda_Function SHALL retrieve queued operations from DynamoDB tables
2. THE Lambda_Function SHALL process queued operations in chronological order
3. WHEN processing each operation, THE Lambda_Function SHALL update the operation status in DynamoDB
4. IF an operation fails, THEN THE Lambda_Function SHALL mark it as failed and continue processing remaining operations
5. THE Lambda_Function SHALL return sync status to the client

### Requirement 15: Successful Infrastructure Deployment

**User Story:** As a developer, I want to deploy the infrastructure using CDK, so that all AWS resources are provisioned correctly.

#### Acceptance Criteria

1. WHEN executing CDK deploy command, THE Infrastructure_System SHALL synthesize CloudFormation templates without errors
2. WHEN CloudFormation executes, THE Infrastructure_System SHALL create all DynamoDB tables successfully
3. WHEN CloudFormation executes, THE Infrastructure_System SHALL create all Lambda functions without VPC configuration
4. WHEN CloudFormation executes, THE Infrastructure_System SHALL configure IAM roles with correct DynamoDB and Bedrock permissions
5. WHEN deployment completes, THE Infrastructure_System SHALL output API Gateway endpoint URL

### Requirement 16: API Endpoint Functionality Verification

**User Story:** As a developer, I want to verify all API endpoints return correct responses, so that I can confirm the migration is successful.

#### Acceptance Criteria

1. WHEN invoking the voice query endpoint with valid audio, THE Lambda_Function SHALL return a 200 status code with audio response URL
2. WHEN invoking the quality grading endpoint with valid image, THE Lambda_Function SHALL return a 200 status code with quality grade
3. WHEN invoking the soil analysis endpoint with valid document, THE Lambda_Function SHALL return a 200 status code with regenerative plan
4. WHEN invoking the planting advisory endpoint with valid parameters, THE Lambda_Function SHALL return a 200 status code with recommendations
5. WHEN invoking the historical data endpoint with valid time range, THE Lambda_Function SHALL return a 200 status code with chart data
6. WHEN invoking any endpoint with invalid authentication, THE Lambda_Function SHALL return a 401 status code
7. WHEN invoking any endpoint with invalid input, THE Lambda_Function SHALL return a 400 status code with error details

### Requirement 17: Cost Target Achievement

**User Story:** As a cost-conscious developer, I want to verify the weekly infrastructure cost is within target, so that the prototype testing is affordable.

#### Acceptance Criteria

1. THE Infrastructure_System SHALL incur no charges for RDS Aurora PostgreSQL
2. THE Infrastructure_System SHALL incur no charges for OpenSearch Serverless
3. THE Infrastructure_System SHALL incur no charges for VPC or NAT_Gateway
4. WHEN measuring costs over a 1-week period with 1 user, THE Infrastructure_System SHALL incur total charges between $5 and $8
5. THE Infrastructure_System SHALL use only pay-per-request pricing for DynamoDB tables to minimize costs for low-volume testing

### Requirement 18: Preserve All Use Case Functionality

**User Story:** As a product owner, I want to confirm no use case functionality is lost, so that the prototype testing is valid.

#### Acceptance Criteria

1. THE Infrastructure_System SHALL support voice queries with transcription, AI response generation, and audio synthesis
2. THE Infrastructure_System SHALL support quality grading with image analysis and historical tracking
3. THE Infrastructure_System SHALL support soil analysis with document extraction and regenerative plan generation
4. THE Infrastructure_System SHALL support planting advisory with weather and soil data integration
5. THE Infrastructure_System SHALL support advisory service with AI-generated farming guidance
6. THE Infrastructure_System SHALL support authentication with Cognito user management
7. THE Infrastructure_System SHALL support historical analytics with Timestream time-series queries
8. THE Infrastructure_System SHALL support offline sync with DynamoDB queue processing

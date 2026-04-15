# Design Document: React Frontend Integration

## Overview

This document outlines the design for a comprehensive React-based web frontend that integrates with the existing KisanMitraAI .NET backend API. The frontend will provide farmers with an intuitive, responsive, and accessible interface for soil analysis, quality grading, voice queries, planting advisory, and historical data visualization.

The design emphasizes mobile-first responsive design, offline capabilities, multi-language support, and seamless integration with the existing backend services including authentication via AWS Cognito, file uploads to S3, and real-time data processing.

### Key Design Principles

- **Mobile-First**: Optimized for mobile devices with progressive enhancement for larger screens
- **Accessibility**: WCAG 2.1 AA compliance with screen reader support and keyboard navigation
- **Performance**: Sub-3-second load times on 3G networks with lazy loading and code splitting
- **Offline-First**: Core functionality available without internet connectivity
- **Security**: Secure token management, CSP headers, and data protection
- **Internationalization**: Support for Hindi, English, and regional Indian languages

## Architecture

### High-Level Architecture

The frontend follows a modern React architecture with the following layers:

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                        │
│  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐│
│  │   Components    │ │      Pages      │ │     Layouts     ││
│  │   - UI Elements │ │   - Dashboard   │ │   - AppLayout   ││
│  │   - Forms       │ │   - SoilAnalysis│ │   - AuthLayout  ││
│  │   - Charts      │ │   - Grading     │ │   - MobileNav   ││
│  └─────────────────┘ └─────────────────┘ └─────────────────┘│
└─────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                     State Management                         │
│  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐│
│  │   Redux Store   │ │     Context     │ │   Local State   ││
│  │   - Auth State  │ │   - Theme       │ │   - Form State  ││
│  │   - App State   │ │   - Language    │ │   - UI State    ││
│  │   - Cache       │ │   - Offline     │ │                 ││
│  └─────────────────┘ └─────────────────┘ └─────────────────┘│
└─────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                     Service Layer                            │
│  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐│
│  │   API Client    │ │  File Upload    │ │  Voice Service  ││
│  │   - HTTP Client │ │   - S3 Upload   │ │   - Recording   ││
│  │   - Auth        │ │   - Progress    │ │   - Playback    ││
│  │   - Retry       │ │   - Validation  │ │   - Transcription││
│  └─────────────────┘ └─────────────────┘ └─────────────────┘│
└─────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                     Infrastructure                           │
│  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐│
│  │  Offline Cache  │ │   PWA Service   │ │   Monitoring    ││
│  │   - IndexedDB   │ │   - Worker      │ │   - Analytics   ││
│  │   - Sync Queue  │ │   - Cache       │ │   - Error Track ││
│  │   - Storage     │ │   - Background  │ │   - Performance ││
│  └─────────────────┘ └─────────────────┘ └─────────────────┘│
└─────────────────────────────────────────────────────────────┘
```

### Technology Stack

**Core Framework:**
- React 18 with TypeScript for type safety and developer experience
- Vite for fast development and optimized builds
- React Router v6 for client-side routing

**State Management:**
- Redux Toolkit for global state management
- React Query for server state and caching
- React Context for theme and language preferences

**UI Framework:**
- Tailwind CSS for utility-first styling
- Headless UI for accessible components
- Framer Motion for animations and transitions

**Data Visualization:**
- Chart.js with react-chartjs-2 for interactive charts
- D3.js for custom visualizations

**File Handling:**
- React Dropzone for file uploads
- AWS SDK for direct S3 uploads
- Image compression and validation

**Audio Processing:**
- Web Audio API for voice recording
- MediaRecorder API for audio capture
- Audio format conversion utilities

**Offline & PWA:**
- Workbox for service worker management
- IndexedDB for offline data storage
- Background sync for queued operations

**Internationalization:**
- React i18next for multi-language support
- ICU message format for complex translations
- RTL support for applicable languages

## Components and Interfaces

### Core Component Architecture

#### 1. Authentication Components

```typescript
// AuthProvider - Context provider for authentication state
interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  login: (phoneNumber: string, password: string) => Promise<void>;
  logout: () => void;
  refreshToken: () => Promise<void>;
  isLoading: boolean;
}

// LoginForm - Phone number and password authentication
interface LoginFormProps {
  onSuccess: () => void;
  onError: (error: string) => void;
}

// ProtectedRoute - Route wrapper for authenticated pages
interface ProtectedRouteProps {
  children: React.ReactNode;
  fallback?: React.ReactNode;
}
```

#### 2. File Upload Components

```typescript
// FileUploadZone - Drag and drop file upload with validation
interface FileUploadZoneProps {
  accept: string[];
  maxSize: number;
  maxFiles: number;
  onUpload: (files: File[]) => Promise<void>;
  onProgress: (progress: number) => void;
  onError: (error: string) => void;
}

// ImageCapture - Camera interface for mobile devices
interface ImageCaptureProps {
  onCapture: (imageBlob: Blob) => void;
  onError: (error: string) => void;
  quality: number;
  facingMode: 'user' | 'environment';
}

// UploadProgress - Progress indicator with retry functionality
interface UploadProgressProps {
  progress: number;
  status: 'uploading' | 'processing' | 'complete' | 'error';
  onRetry?: () => void;
  onCancel?: () => void;
}
```

#### 3. Voice Interface Components

```typescript
// VoiceRecorder - Audio recording with waveform visualization
interface VoiceRecorderProps {
  maxDuration: number;
  onRecordingComplete: (audioBlob: Blob) => void;
  onError: (error: string) => void;
  dialect: string;
}

// AudioPlayer - Playback for voice responses
interface AudioPlayerProps {
  audioUrl: string;
  autoPlay?: boolean;
  onPlaybackComplete?: () => void;
}

// DialectSelector - Regional dialect selection
interface DialectSelectorProps {
  selectedDialect: string;
  onDialectChange: (dialect: string) => void;
  availableDialects: Dialect[];
}
```

#### 4. Data Visualization Components

```typescript
// ChartContainer - Responsive chart wrapper
interface ChartContainerProps {
  data: ChartData;
  type: 'line' | 'bar' | 'area' | 'scatter';
  responsive: boolean;
  onDataPointClick?: (point: ChartPoint) => void;
}

// TrendChart - Historical data trends with comparison
interface TrendChartProps {
  data: TrendLineData;
  comparisonPeriods?: PeriodChartData[];
  showAnnotations: boolean;
  timeRange: TimeRange;
}

// MetricsCard - Key performance indicators
interface MetricsCardProps {
  title: string;
  value: string | number;
  change?: number;
  trend: 'up' | 'down' | 'stable';
  icon?: React.ReactNode;
}
```

#### 5. Form Components

```typescript
// SoilDataForm - Manual soil data entry and validation
interface SoilDataFormProps {
  initialData?: SoilHealthData;
  onSubmit: (data: SoilHealthData) => Promise<void>;
  onValidationError: (errors: ValidationError[]) => void;
}

// PlantingAdvisoryForm - Crop and location selection
interface PlantingAdvisoryFormProps {
  onSubmit: (request: PlantingAdvisoryRequest) => Promise<void>;
  availableCrops: CropType[];
  farmerLocation?: Location;
}
```

### API Integration Layer

#### HTTP Client Configuration

```typescript
// API client with authentication and retry logic
class ApiClient {
  private baseURL: string;
  private authToken: string | null;
  private retryConfig: RetryConfig;

  async request<T>(config: RequestConfig): Promise<ApiResponse<T>>;
  async uploadFile(file: File, endpoint: string, onProgress?: (progress: number) => void): Promise<UploadResponse>;
  async refreshToken(): Promise<void>;
  setAuthToken(token: string): void;
  clearAuthToken(): void;
}

// Request/Response types matching backend DTOs
interface LoginRequest {
  phoneNumber: string;
  password: string;
}

interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  idToken: string;
  expiresIn: number;
  tokenType: string;
}

interface SoilHealthCardResponse {
  soilData: SoilHealthData | null;
  isValid: boolean;
  validationErrors: ValidationError[];
  message: string;
  requiresManualVerification: boolean;
}

interface GradingResult {
  recordId: string;
  grade: QualityGrade;
  certifiedPrice: number;
  analysis: ImageAnalysisResult;
  timestamp: string;
}

interface VoiceQueryResponse {
  transcription: string;
  prices: MarketPrice[];
  audioResponseUrl: string;
  confidence: number;
  dialect: string;
}
```

#### Service Layer Interfaces

```typescript
// Authentication service
interface AuthService {
  login(phoneNumber: string, password: string): Promise<LoginResponse>;
  register(phoneNumber: string, password: string, name: string): Promise<RegisterResponse>;
  confirmRegistration(phoneNumber: string, code: string): Promise<void>;
  refreshToken(refreshToken: string): Promise<RefreshResponse>;
  validateToken(token: string): Promise<ValidateResponse>;
  logout(): Promise<void>;
}

// Soil analysis service
interface SoilAnalysisService {
  uploadSoilHealthCard(file: File): Promise<SoilHealthCardResponse>;
  generateRegenerativePlan(request: PlanGenerationRequest): Promise<RegenerativePlan>;
  getSoilHistory(startDate?: Date, endDate?: Date): Promise<SoilHealthData[]>;
}

// Quality grading service
interface QualityGradingService {
  gradeProduct(image: File, produceType: string, location: string): Promise<GradingResult>;
  gradeBatch(images: File[], produceType: string, location: string): Promise<BatchGradingResult>;
  getGradingHistory(startDate?: Date, endDate?: Date): Promise<GradingRecord[]>;
}

// Voice query service
interface VoiceQueryService {
  processVoiceQuery(audioFile: File, dialect: string): Promise<VoiceQueryResponse>;
  getSupportedDialects(): Promise<Dialect[]>;
}
```

## Data Models

### Core Data Types

```typescript
// User and authentication
interface User {
  id: string;
  phoneNumber: string;
  name: string;
  preferredLanguage: string;
  farmProfile?: FarmProfile;
}

interface FarmProfile {
  farmerId: string;
  farmName: string;
  location: Location;
  farmSize: number;
  primaryCrops: string[];
  soilType: string;
}

interface Location {
  state: string;
  district: string;
  block: string;
  village: string;
  coordinates?: {
    latitude: number;
    longitude: number;
  };
}

// Soil health data
interface SoilHealthData {
  farmerId: string;
  sampleId: string;
  collectionDate: Date;
  ph: number;
  organicCarbon: number;
  nitrogen: number;
  phosphorus: number;
  potassium: number;
  sulfur: number;
  zinc: number;
  boron: number;
  iron: number;
  manganese: number;
  copper: number;
  soilTexture: string;
  recommendations: string[];
}

interface ValidationError {
  field: string;
  message: string;
  code: string;
}

// Quality grading
interface ImageAnalysisResult {
  confidenceScore: number;
  detectedObjects: DetectedObject[];
  qualityIndicators: QualityIndicator[];
  imageQuality: ImageQuality;
}

interface DetectedObject {
  label: string;
  confidence: number;
  boundingBox: BoundingBox;
}

interface QualityIndicator {
  name: string;
  value: number;
  threshold: number;
  status: 'good' | 'fair' | 'poor';
}

enum QualityGrade {
  A = 'A',
  B = 'B',
  C = 'C',
  Reject = 'Reject'
}

// Market data
interface MarketPrice {
  commodity: string;
  market: string;
  price: number;
  unit: string;
  date: Date;
  source: string;
}

// Voice processing
interface Dialect {
  code: string;
  name: string;
  nativeName: string;
  region: string;
}

// Chart data (matching backend ChartData model)
interface ChartData {
  title: string;
  xAxisLabel: string;
  yAxisLabel: string;
  series: ChartSeries[];
  annotations: ChartAnnotation[];
}

interface ChartSeries {
  name: string;
  points: ChartPoint[];
  color: string;
  type: ChartSeriesType;
}

interface ChartPoint {
  label: string;
  value: number;
  timestamp: Date;
}
```

### State Management Models

```typescript
// Redux store structure
interface RootState {
  auth: AuthState;
  app: AppState;
  soil: SoilState;
  grading: GradingState;
  voice: VoiceState;
  offline: OfflineState;
}

interface AuthState {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
  tokens: {
    accessToken: string | null;
    refreshToken: string | null;
    expiresAt: number | null;
  };
}

interface AppState {
  language: string;
  theme: 'light' | 'dark';
  isOnline: boolean;
  notifications: Notification[];
  loading: Record<string, boolean>;
}

interface OfflineState {
  queuedRequests: QueuedRequest[];
  cachedData: Record<string, CachedData>;
  syncStatus: 'idle' | 'syncing' | 'error';
  lastSyncTime: Date | null;
}
```

### Offline Data Management

```typescript
// Offline queue for API requests
interface QueuedRequest {
  id: string;
  method: 'GET' | 'POST' | 'PUT' | 'DELETE';
  url: string;
  data?: any;
  files?: File[];
  timestamp: Date;
  retryCount: number;
  priority: 'high' | 'medium' | 'low';
}

// Cached data structure
interface CachedData {
  key: string;
  data: any;
  timestamp: Date;
  expiresAt: Date;
  version: number;
}

// Sync conflict resolution
interface SyncConflict {
  localData: any;
  serverData: any;
  conflictType: 'update' | 'delete' | 'create';
  resolution?: 'local' | 'server' | 'merge';
}
```

## Error Handling

### Error Classification and Response Strategy

The frontend implements a comprehensive error handling strategy that maps to the backend's standardized error codes:

#### Error Categories

1. **Network Errors**
   - Connection timeout
   - No internet connectivity
   - Server unavailable
   - Rate limiting

2. **Validation Errors**
   - File format validation
   - File size limits
   - Required field validation
   - Data format validation

3. **Authentication Errors**
   - Token expiration
   - Invalid credentials
   - Unauthorized access
   - Session timeout

4. **Service Errors**
   - Image processing failures
   - Voice recognition errors
   - External service failures
   - Data processing errors

#### Error Handling Components

```typescript
// Global error boundary
interface ErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
  errorInfo: ErrorInfo | null;
}

// Error display component
interface ErrorDisplayProps {
  error: ApiError;
  onRetry?: () => void;
  onDismiss?: () => void;
  showDetails?: boolean;
}

// Toast notification system
interface ToastNotification {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info';
  title: string;
  message: string;
  duration?: number;
  actions?: ToastAction[];
}

// Retry mechanism
interface RetryConfig {
  maxAttempts: number;
  backoffMultiplier: number;
  initialDelay: number;
  maxDelay: number;
  retryableErrors: string[];
}
```

#### Error Recovery Strategies

1. **Automatic Retry**: For transient network errors with exponential backoff
2. **Offline Queuing**: Queue requests when offline for later synchronization
3. **Graceful Degradation**: Provide limited functionality when services are unavailable
4. **User Guidance**: Clear error messages with actionable suggestions
5. **Fallback Options**: Alternative workflows when primary features fail

## Testing Strategy

### Testing Approach

The frontend testing strategy employs a comprehensive approach combining unit tests, integration tests, and property-based testing to ensure reliability and correctness across all user scenarios.

#### Unit Testing

**Framework**: Jest with React Testing Library
**Coverage Target**: 90% code coverage
**Focus Areas**:
- Component rendering and behavior
- State management logic
- Utility functions
- API client methods
- Error handling paths

**Key Test Categories**:
- Component props and state changes
- User interaction handling
- Form validation logic
- File upload validation
- Audio recording functionality
- Offline queue management

#### Integration Testing

**Framework**: Cypress for end-to-end testing
**Focus Areas**:
- Complete user workflows
- API integration points
- File upload processes
- Voice recording and playback
- Offline/online transitions
- Multi-language functionality

**Critical User Journeys**:
- Login and authentication flow
- Soil Health Card upload and analysis
- Quality grading with image capture
- Voice query recording and response
- Historical data visualization
- Offline data synchronization

#### Property-Based Testing

**Framework**: fast-check for JavaScript property testing
**Configuration**: Minimum 100 iterations per property test
**Test Tagging**: Each property test references its design document property

Property-based testing will validate universal behaviors across randomized inputs, ensuring the frontend handles edge cases and maintains correctness under various conditions.

#### Performance Testing

**Tools**: Lighthouse CI, Web Vitals, Bundle Analyzer
**Metrics**:
- First Contentful Paint < 1.5s
- Largest Contentful Paint < 2.5s
- Cumulative Layout Shift < 0.1
- First Input Delay < 100ms
- Bundle size optimization

#### Accessibility Testing

**Tools**: axe-core, WAVE, manual testing with screen readers
**Standards**: WCAG 2.1 AA compliance
**Focus Areas**:
- Keyboard navigation
- Screen reader compatibility
- Color contrast ratios
- Focus management
- ARIA labels and roles

### Test Data Management

```typescript
// Test data factories for consistent test scenarios
interface TestDataFactory {
  createUser(overrides?: Partial<User>): User;
  createSoilData(overrides?: Partial<SoilHealthData>): SoilHealthData;
  createGradingResult(overrides?: Partial<GradingResult>): GradingResult;
  createVoiceResponse(overrides?: Partial<VoiceQueryResponse>): VoiceQueryResponse;
}

// Mock API responses
interface MockApiClient {
  mockLogin(response: LoginResponse): void;
  mockSoilUpload(response: SoilHealthCardResponse): void;
  mockGrading(response: GradingResult): void;
  mockVoiceQuery(response: VoiceQueryResponse): void;
  mockNetworkError(errorCode: string): void;
}
```
## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Authentication State Management

*For any* authentication operation (login, logout, token refresh), the application should maintain consistent authentication state and properly manage JWT tokens, ensuring secure storage and automatic cleanup on logout.

**Validates: Requirements 1.2, 1.4, 1.5, 1.6, 12.1**

### Property 2: File Upload Validation and Security

*For any* file upload operation, the application should validate file format, size limits, and security constraints before processing, rejecting invalid files with appropriate error messages.

**Validates: Requirements 3.2, 4.2, 5.3, 12.4**

### Property 3: Responsive Design Adaptation

*For any* screen width between 320px and 1920px, the application should adapt its layout appropriately, using mobile-first navigation patterns for widths below 768px and desktop patterns for larger screens.

**Validates: Requirements 2.1, 2.2**

### Property 4: Progress and Loading State Display

*For any* asynchronous operation (file upload, API request, data processing), the application should display appropriate progress indicators or loading states until completion.

**Validates: Requirements 3.3, 4.3, 10.5**

### Property 5: Error Handling and User Guidance

*For any* error condition (API errors, network failures, validation errors), the application should display user-friendly error messages in the selected language with specific guidance and suggested actions.

**Validates: Requirements 1.3, 11.1, 11.2, 11.3, 11.7**

### Property 6: Data Display Completeness

*For any* successful data processing operation (soil analysis, quality grading, planting recommendations), the application should display all required result components including confidence scores, analysis details, and actionable information.

**Validates: Requirements 3.4, 4.4, 4.5, 6.3, 6.4, 6.5**

### Property 7: Navigation Consistency

*For any* user interaction that should trigger navigation (service card selection, successful authentication), the application should navigate to the correct destination page consistently.

**Validates: Requirements 1.4, 2.4**

### Property 8: Offline Functionality and Sync

*For any* offline state, the application should queue requests for later synchronization, provide access to cached data, display offline indicators, and automatically sync when connectivity returns.

**Validates: Requirements 5.7, 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7**

### Property 9: Multi-Language Support Consistency

*For any* language selection, the application should translate all UI elements, persist the language preference across sessions, handle bilingual displays appropriately, and gracefully fallback to English when translations are unavailable.

**Validates: Requirements 8.2, 8.3, 8.4, 8.6, 8.7**

### Property 10: Voice Interface Processing

*For any* voice recording operation, the application should provide visual feedback during recording, support multiple audio formats, allow dialect selection, display transcription for verification, and provide complete results with audio response.

**Validates: Requirements 5.2, 5.3, 5.4, 5.5, 5.6**

### Property 11: Data Validation and Prerequisites

*For any* operation requiring prerequisite data (planting recommendations requiring soil data), the application should validate prerequisites exist before proceeding and provide appropriate guidance when missing.

**Validates: Requirements 6.2, 3.5**

### Property 12: Batch Processing Consistency

*For any* batch operation (multiple image grading), the application should process all valid items, display both individual and aggregated results, and handle partial failures gracefully.

**Validates: Requirements 4.6**

### Property 13: Historical Data Visualization

*For any* historical data display, the application should provide interactive charts with time period filters, support comparisons between periods, handle insufficient data gracefully, and allow data export in multiple formats.

**Validates: Requirements 7.2, 7.3, 7.4, 7.5, 7.7**

### Property 14: Accessibility Compliance

*For any* interactive element or content display, the application should provide proper semantic markup, ARIA labels, alt text for images, keyboard navigation support, and screen reader compatibility.

**Validates: Requirements 10.2, 10.3, 10.4**

### Property 15: Performance Optimization

*For any* content loading scenario, the application should implement lazy loading for non-critical components, work correctly at browser zoom levels up to 200%, and maintain usability without horizontal scrolling.

**Validates: Requirements 10.6, 10.7**

### Property 16: Retry and Recovery Mechanisms

*For any* transient failure, the application should implement appropriate retry mechanisms with exponential backoff, allow resuming failed uploads, and provide recovery options for critical operations.

**Validates: Requirements 11.4, 11.5**

### Property 17: Security and Privacy Protection

*For any* sensitive data handling, the application should encrypt data in transit and at rest where possible, implement CSP headers, automatically log out inactive users, avoid storing sensitive data in insecure storage, and mask personal information appropriately.

**Validates: Requirements 12.2, 12.3, 12.5, 12.6, 12.7**

### Property 18: Error Logging and Privacy

*For any* error that occurs, the application should log sufficient debugging information while protecting user privacy and not exposing sensitive data in logs.

**Validates: Requirements 11.6**
## Error Handling

### Comprehensive Error Management Strategy

The frontend implements a multi-layered error handling approach that provides resilient user experience while maintaining system stability.

#### Error Boundary Implementation

```typescript
// Global error boundary for React component errors
class GlobalErrorBoundary extends React.Component<Props, State> {
  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    // Log error to monitoring service
    this.logError(error, errorInfo);
    
    // Show user-friendly error message
    this.showErrorNotification(error);
  }

  render() {
    if (this.state.hasError) {
      return <ErrorFallback error={this.state.error} onRetry={this.handleRetry} />;
    }
    return this.props.children;
  }
}
```

#### API Error Handling

```typescript
// Centralized API error handler
class ApiErrorHandler {
  handleError(error: ApiError): ErrorResponse {
    const errorMap = {
      [ErrorCodes.NETWORK_UNAVAILABLE]: {
        message: "Network connection unavailable",
        userFriendlyMessage: "नेटवर्क कनेक्शन उपलब्ध नहीं है",
        actions: ["Check internet connection", "Try again later"],
        shouldRetry: true,
        shouldQueue: true
      },
      [ErrorCodes.TOKEN_EXPIRED]: {
        message: "Authentication token expired",
        userFriendlyMessage: "सत्र समाप्त हो गया है",
        actions: ["Please log in again"],
        shouldRetry: false,
        shouldRedirect: "/login"
      },
      [ErrorCodes.FILE_TOO_LARGE]: {
        message: "File size exceeds limit",
        userFriendlyMessage: "फ़ाइल का आकार सीमा से अधिक है",
        actions: ["Reduce file size", "Use a smaller image"],
        shouldRetry: false
      }
    };

    return this.formatError(error, errorMap[error.code] || this.getDefaultError());
  }
}
```

#### Offline Error Handling

```typescript
// Offline-specific error management
class OfflineErrorHandler {
  handleOfflineError(operation: QueuedOperation): void {
    // Queue operation for later retry
    this.offlineQueue.add(operation);
    
    // Show offline notification
    this.showOfflineNotification({
      message: "Operation queued for when connection returns",
      userFriendlyMessage: "कनेक्शन वापस आने पर ऑपरेशन को पूरा किया जाएगा"
    });
  }

  handleSyncConflict(conflict: SyncConflict): Promise<ConflictResolution> {
    // Show conflict resolution dialog
    return this.showConflictDialog(conflict);
  }
}
```

#### Error Recovery Strategies

1. **Automatic Retry**: Exponential backoff for transient errors
2. **Graceful Degradation**: Reduced functionality when services unavailable
3. **Offline Queuing**: Store operations for later execution
4. **User Guidance**: Clear instructions for error resolution
5. **Fallback UI**: Alternative interfaces when primary features fail

## Testing Strategy

### Comprehensive Testing Approach

The testing strategy ensures reliability, performance, and correctness across all user scenarios and edge cases.

#### Unit Testing Configuration

**Framework**: Jest 29+ with React Testing Library
**Coverage Requirements**: 
- Minimum 90% line coverage
- 85% branch coverage
- 80% function coverage

**Test Categories**:

```typescript
// Component testing patterns
describe('FileUploadZone', () => {
  it('should validate file types correctly', () => {
    // Test file type validation property
  });

  it('should handle upload progress updates', () => {
    // Test progress callback property
  });

  it('should display error messages for invalid files', () => {
    // Test error handling property
  });
});

// Service testing patterns
describe('AuthService', () => {
  it('should handle token refresh automatically', () => {
    // Test token refresh property
  });

  it('should clear tokens on logout', () => {
    // Test logout cleanup property
  });
});
```

#### Property-Based Testing Implementation

**Framework**: fast-check for comprehensive input testing
**Configuration**: Minimum 100 iterations per property test

```typescript
// Example property tests
describe('File Upload Properties', () => {
  it('Property 2: File Upload Validation and Security', () => {
    fc.assert(fc.property(
      fc.record({
        name: fc.string(),
        size: fc.integer(0, 50 * 1024 * 1024), // 0 to 50MB
        type: fc.oneof(
          fc.constant('image/jpeg'),
          fc.constant('image/png'),
          fc.constant('application/pdf'),
          fc.constant('text/plain') // Invalid type
        )
      }),
      (file) => {
        const result = validateFile(file);
        
        // Valid files should pass validation
        if (isValidFileType(file.type) && file.size <= MAX_FILE_SIZE) {
          expect(result.isValid).toBe(true);
        } else {
          // Invalid files should be rejected with appropriate error
          expect(result.isValid).toBe(false);
          expect(result.error).toBeDefined();
        }
      }
    ), { numRuns: 100 });
  });

  it('Property 3: Responsive Design Adaptation', () => {
    fc.assert(fc.property(
      fc.integer(320, 1920), // Screen widths
      (screenWidth) => {
        const layout = getLayoutForScreenWidth(screenWidth);
        
        if (screenWidth < 768) {
          expect(layout.navigation).toBe('mobile');
          expect(layout.menuCollapsed).toBe(true);
        } else {
          expect(layout.navigation).toBe('desktop');
          expect(layout.menuCollapsed).toBe(false);
        }
      }
    ), { numRuns: 100 });
  });
});
```

**Property Test Tags**: Each property test includes a comment referencing the design document:
```typescript
// Feature: react-frontend-integration, Property 2: File Upload Validation and Security
```

#### Integration Testing

**Framework**: Cypress for end-to-end testing
**Test Scenarios**:

```typescript
// Critical user journeys
describe('Soil Analysis Workflow', () => {
  it('should complete full soil health card analysis', () => {
    cy.login('farmer@example.com', 'password');
    cy.visit('/soil-analysis');
    cy.uploadFile('soil-health-card.jpg');
    cy.waitForProcessing();
    cy.verifyResults();
    cy.generatePlan();
  });
});

describe('Offline Functionality', () => {
  it('should queue operations when offline', () => {
    cy.goOffline();
    cy.uploadFile('produce-image.jpg');
    cy.verifyQueuedOperation();
    cy.goOnline();
    cy.verifyAutoSync();
  });
});
```

#### Performance Testing

**Tools**: Lighthouse CI, Web Vitals monitoring
**Targets**:
- First Contentful Paint: < 1.5s
- Largest Contentful Paint: < 2.5s
- Cumulative Layout Shift: < 0.1
- First Input Delay: < 100ms

```typescript
// Performance test configuration
const performanceConfig = {
  lighthouse: {
    performance: 90,
    accessibility: 95,
    bestPractices: 90,
    seo: 85
  },
  webVitals: {
    fcp: 1500,
    lcp: 2500,
    cls: 0.1,
    fid: 100
  }
};
```

#### Accessibility Testing

**Tools**: axe-core, WAVE, manual testing
**Standards**: WCAG 2.1 AA compliance

```typescript
// Accessibility test patterns
describe('Accessibility Compliance', () => {
  it('should have no accessibility violations', () => {
    cy.visit('/dashboard');
    cy.injectAxe();
    cy.checkA11y();
  });

  it('should support keyboard navigation', () => {
    cy.visit('/soil-analysis');
    cy.tab(); // Navigate through interactive elements
    cy.verifyFocusOrder();
  });
});
```

### Test Data Management

```typescript
// Test data factories
export const TestDataFactory = {
  createUser: (overrides?: Partial<User>): User => ({
    id: faker.datatype.uuid(),
    phoneNumber: faker.phone.phoneNumber(),
    name: faker.name.fullName(),
    preferredLanguage: 'hi',
    ...overrides
  }),

  createSoilData: (overrides?: Partial<SoilHealthData>): SoilHealthData => ({
    farmerId: faker.datatype.uuid(),
    sampleId: faker.datatype.uuid(),
    collectionDate: faker.date.recent(),
    ph: faker.datatype.float({ min: 4.0, max: 9.0 }),
    organicCarbon: faker.datatype.float({ min: 0.1, max: 2.0 }),
    // ... other soil parameters
    ...overrides
  }),

  createGradingResult: (overrides?: Partial<GradingResult>): GradingResult => ({
    recordId: faker.datatype.uuid(),
    grade: faker.helpers.arrayElement(['A', 'B', 'C', 'Reject']),
    certifiedPrice: faker.datatype.float({ min: 10, max: 1000 }),
    // ... other grading data
    ...overrides
  })
};
```

### Dual Testing Approach

The testing strategy employs both unit tests and property-based tests as complementary approaches:

**Unit Tests Focus**:
- Specific examples and edge cases
- Integration points between components
- Error conditions and boundary cases
- User interaction scenarios

**Property Tests Focus**:
- Universal behaviors across all inputs
- Comprehensive input coverage through randomization
- Invariant validation across state changes
- Cross-browser compatibility verification

**Balance**: Approximately 70% unit tests, 30% property tests, ensuring comprehensive coverage without redundancy while maintaining fast test execution times.
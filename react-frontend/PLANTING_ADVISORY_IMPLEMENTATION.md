# Planting Advisory Feature Implementation

## Overview
This document describes the implementation of the Planting Advisory feature for the KisanMitra AI React frontend, which provides farmers with planting recommendations based on weather and soil data.

## Implementation Date
December 2024

## Components Created

### 1. Service Layer
**File**: `src/services/plantingAdvisoryService.ts`
- Handles API communication with backend PlantingAdvisoryController
- Manages local storage for saved recommendations
- Provides methods for:
  - Getting planting recommendations
  - Saving recommendations locally
  - Retrieving saved recommendations
  - Deleting saved recommendations

### 2. Form Component
**File**: `src/components/planting/PlantingAdvisoryForm.tsx`
- Crop type selection (wheat, rice, cotton, maize, sugarcane, soybean, pulses, vegetables)
- Location input (district/city)
- Forecast period selection (1-90 days, default 90)
- Multi-language support (Hindi/English)
- Form validation and loading states

### 3. Recommendation Display Component
**File**: `src/components/planting/RecommendationDisplay.tsx`
- Displays optimal planting windows with:
  - Start and end dates
  - Confidence scores
  - Rationale for each window
  - Risk factors
- Shows seed variety recommendations with:
  - Variety name and seed company
  - Maturity period
  - Yield potential
  - Suitability reasons
  - Key characteristics
- Data source information (weather and soil data dates)
- Save recommendation functionality

### 4. Saved Recommendations Component
**File**: `src/components/planting/SavedRecommendations.tsx`
- Lists all saved recommendations
- View detailed recommendation
- Delete saved recommendations
- Shows crop type, location, and save date
- Multi-language support

### 5. Main Page Component
**File**: `src/pages/PlantingAdvisoryPage.tsx`
- Tab-based navigation (New Recommendation / Saved Recommendations)
- Prerequisite validation (soil data required)
- Error handling with user-friendly messages
- Loading states
- Integration with notification system
- Handles cases where no suitable planting windows exist

## Features Implemented

### Requirement 6.1: Crop and Location Selection
✅ Form with crop type dropdown and location input
✅ Forecast period selection (1-90 days)
✅ Form validation

### Requirement 6.2: Prerequisite Validation
✅ Displays prerequisite information about soil data requirement
✅ Shows specific error message when soil data is missing
✅ Guides user to upload Soil Health Card first

### Requirement 6.3: Optimal Planting Windows Display
✅ Shows all planting windows with confidence scores
✅ Highlights best window
✅ Displays date ranges in localized format
✅ Shows rationale for each window

### Requirement 6.4: Seed Variety Recommendations
✅ Displays seed varieties with detailed characteristics
✅ Shows maturity period and yield potential
✅ Includes suitability reasons
✅ Lists key characteristics

### Requirement 6.5: Weather Data Display
✅ Shows weather data fetch timestamp
✅ Shows soil data date
✅ Displays data sources in user-friendly format

### Requirement 6.6: Alternative Suggestions
✅ Handles cases with no suitable planting windows
✅ Displays appropriate message
✅ Provides option to try again with different parameters

### Requirement 6.7: Recommendation Tracking
✅ Save recommendations locally
✅ View saved recommendations
✅ Delete saved recommendations
✅ Track crop type, location, and save date

## API Integration

### Endpoint
`POST /api/v1/plantingadvisory/recommend`

### Request Format
```typescript
{
  cropType: string;
  location: string;
  forecastDays?: number;
}
```

### Response Format
```typescript
{
  plantingWindows: PlantingWindow[];
  seedRecommendations: SeedRecommendation[];
  message: string;
  hasRecommendations: boolean;
  weatherFetchedAt?: string;
  soilDataDate?: string;
}
```

## Multi-Language Support
- All UI text supports Hindi and English
- Uses LanguageContext for language switching
- Bilingual labels for crop types
- Localized date formatting

## Error Handling
- Network errors with retry functionality
- Missing soil data with guidance to upload
- Invalid input validation
- User-friendly error messages in selected language
- Integration with notification system

## Local Storage
Saved recommendations are stored in browser localStorage:
- Key: `savedPlantingRecommendations`
- Format: Array of SavedRecommendation objects
- Includes recommendation ID, crop type, location, and save timestamp

## Responsive Design
- Mobile-first approach
- Adapts to screen sizes from 320px to 1920px
- Touch-friendly interface
- Collapsible sections for mobile

## Accessibility
- Semantic HTML structure
- ARIA labels for form controls
- Keyboard navigation support
- Screen reader compatible
- High contrast colors for readability

## Integration Points

### Dashboard
- Planting Advisory card already exists in DashboardPage
- Icon: 📅
- Color: Purple gradient (from-purple-400 to-purple-600)
- Route: `/planting-advisory`

### Navigation
- Route configured in App.tsx
- Protected route requiring authentication
- Lazy loaded for performance

### Dependencies
- AuthContext for authentication
- LanguageContext for multi-language support
- useNotifications hook for user feedback
- LoadingSpinner for loading states
- ErrorDisplay for error handling
- ConfidenceScoreDisplay for confidence visualization

## Testing Recommendations

### Unit Tests
- Form validation logic
- Service API calls
- Local storage operations
- Error handling scenarios

### Integration Tests
- Complete recommendation flow
- Save and retrieve recommendations
- Error handling with missing soil data
- Multi-language switching

### Property-Based Tests
- Form input validation across various inputs
- Date formatting for different locales
- Confidence score display for all valid ranges

## Future Enhancements
1. Export recommendations as PDF
2. Share recommendations via WhatsApp/SMS
3. Set reminders for planting windows
4. Compare multiple crop recommendations
5. Historical recommendation tracking with outcomes
6. Integration with calendar apps
7. Push notifications for optimal planting times

## Notes
- Requires backend PlantingAdvisoryController to be deployed
- Depends on soil data being available in user's account
- Weather data fetched from external service via backend
- Recommendations are AI-generated using Amazon Bedrock

# Multi-Language Support Implementation

This document describes the multi-language support implementation for the KisanMitra AI React frontend.

## Overview

The application supports 8 languages:
- **Hindi (hi)** - हिन्दी
- **English (en)** - English
- **Bengali (bn)** - বাংলা
- **Telugu (te)** - తెలుగు
- **Marathi (mr)** - मराठी
- **Tamil (ta)** - தமிழ்
- **Gujarati (gu)** - ગુજરાતી
- **Kannada (kn)** - ಕನ್ನಡ

## Architecture

### Core Technologies

- **react-i18next**: React integration for i18next internationalization framework
- **i18next**: Core internationalization framework
- **i18next-browser-languagedetector**: Automatic language detection
- **Google Fonts**: Language-specific fonts (Noto Sans family)

### Key Components

1. **i18n Configuration** (`src/i18n/config.ts`)
   - Initializes i18next with all language resources
   - Configures language detection and persistence
   - Sets up fallback to English

2. **LanguageContext** (`src/contexts/LanguageContext.tsx`)
   - Provides language state and switching functionality
   - Integrates with Redux for state management
   - Applies language-specific fonts and text direction
   - Persists language preference to localStorage

3. **Translation Files** (`src/i18n/locales/*/translation.json`)
   - Organized by language code
   - Comprehensive translations for all UI elements
   - Includes error messages, validation text, and user guidance

## Usage

### Using Translations in Components

#### Method 1: Using the useLanguage hook

```typescript
import { useLanguage } from '@/contexts/LanguageContext';

function MyComponent() {
  const { t } = useLanguage();
  
  return (
    <div>
      <h1>{t('app.title')}</h1>
      <p>{t('dashboard.welcome', { name: 'Farmer' })}</p>
    </div>
  );
}
```

#### Method 2: Using the useTranslation hook directly

```typescript
import { useTranslation } from 'react-i18next';

function MyComponent() {
  const { t } = useTranslation();
  
  return <button>{t('app.submit')}</button>;
}
```

### Language Switching

Use the `LanguageSelector` component:

```typescript
import { LanguageSelector } from '@/components/language';

function SettingsPage() {
  return (
    <div>
      <h2>Language Settings</h2>
      <LanguageSelector variant="default" showLabel={true} />
    </div>
  );
}
```

### Bilingual Text Display

For API responses that need to show both local language and English:

```typescript
import { BilingualText, BilingualLabel } from '@/components/language';

function ResultDisplay() {
  return (
    <div>
      {/* Stacked layout (default) */}
      <BilingualText 
        localText="गेहूं" 
        englishText="Wheat" 
      />
      
      {/* Inline layout */}
      <BilingualText 
        localText="गेहूं" 
        englishText="Wheat" 
        layout="inline"
      />
      
      {/* With label */}
      <BilingualLabel
        label="Crop Type"
        value="गेहूं"
        englishValue="Wheat"
      />
    </div>
  );
}
```

### Localized Formatting

Use the `useLocaleFormat` hook for dates, numbers, and currency:

```typescript
import { useLocaleFormat } from '@/hooks/useLocaleFormat';

function DataDisplay() {
  const { formatDate, formatCurrency, formatNumber } = useLocaleFormat();
  
  return (
    <div>
      <p>Date: {formatDate(new Date())}</p>
      <p>Price: {formatCurrency(1500)}</p>
      <p>Quantity: {formatNumber(1234.56)}</p>
    </div>
  );
}
```

### Localized Error Messages

Use the `useLocalizedError` hook:

```typescript
import { useLocalizedError } from '@/hooks/useLocalizedError';
import { ApiErrorHandler } from '@/utils/apiErrorHandler';

function MyComponent() {
  const { getErrorMessage } = useLocalizedError();
  
  const handleError = (error: unknown) => {
    const errorResponse = ApiErrorHandler.handleError(error);
    const message = getErrorMessage(errorResponse);
    // Display the localized error message
    console.error(message);
  };
  
  return <div>...</div>;
}
```

## Translation Keys Structure

Translation keys follow a hierarchical structure:

```
app.*           - Application-wide strings
nav.*           - Navigation items
auth.*          - Authentication related
dashboard.*     - Dashboard page
soilAnalysis.*  - Soil analysis feature
qualityGrading.* - Quality grading feature
voiceQueries.*  - Voice queries feature
plantingAdvisory.* - Planting advisory feature
history.*       - Historical data feature
errors.*        - Error messages
offline.*       - Offline mode messages
settings.*      - Settings page
validation.*    - Form validation messages
dateTime.*      - Date/time related strings
```

## Adding New Translations

### 1. Add to English translation file

Edit `src/i18n/locales/en/translation.json`:

```json
{
  "myFeature": {
    "title": "My Feature",
    "description": "This is my new feature"
  }
}
```

### 2. Add to other language files

Add the same keys to all other language files (`hi`, `bn`, `te`, `mr`, `ta`, `gu`, `kn`).

For languages where you don't have translations yet, you can:
- Use English as a placeholder (will fallback automatically)
- Use translation services
- Work with native speakers

### 3. Use in components

```typescript
const { t } = useLanguage();
return <h1>{t('myFeature.title')}</h1>;
```

## Language-Specific Fonts

The application automatically loads appropriate fonts for each language:

- **Hindi/Marathi**: Noto Sans Devanagari
- **Bengali**: Noto Sans Bengali
- **Telugu**: Noto Sans Telugu
- **Tamil**: Noto Sans Tamil
- **Gujarati**: Noto Sans Gujarati
- **Kannada**: Noto Sans Kannada
- **English**: Inter (system fonts)

Fonts are loaded dynamically when a language is selected and applied to the document root.

## Text Direction Support

The application supports both LTR (Left-to-Right) and RTL (Right-to-Left) text directions. Currently, all supported languages use LTR, but the infrastructure is in place for RTL languages if needed in the future.

## Fallback Strategy

The application implements a graceful fallback strategy:

1. Try to load the selected language
2. If translation key is missing, fall back to English
3. If English translation is missing, display the key itself
4. For fonts, fall back to system fonts if Google Fonts fail to load

## Best Practices

### 1. Always use translation keys

❌ Bad:
```typescript
<button>Submit</button>
```

✅ Good:
```typescript
<button>{t('app.submit')}</button>
```

### 2. Use interpolation for dynamic content

❌ Bad:
```typescript
<p>Welcome, {userName}</p>
```

✅ Good:
```typescript
<p>{t('dashboard.welcome', { name: userName })}</p>
```

### 3. Use BilingualText for API responses

When displaying data from the backend that might be in a local language:

```typescript
<BilingualText 
  localText={apiResponse.localName} 
  englishText={apiResponse.englishName} 
/>
```

### 4. Use locale-aware formatting

❌ Bad:
```typescript
<p>{new Date().toLocaleDateString()}</p>
```

✅ Good:
```typescript
const { formatDate } = useLocaleFormat();
<p>{formatDate(new Date())}</p>
```

### 5. Provide context in translation keys

Use descriptive, hierarchical keys:

❌ Bad: `t('save')`
✅ Good: `t('soilAnalysis.savePlan')`

## Testing Multi-Language Support

### Manual Testing

1. Change language using the LanguageSelector
2. Verify all UI text updates
3. Check that fonts are appropriate
4. Verify date/number formatting
5. Test error messages in different languages
6. Verify language persistence across page reloads

### Automated Testing

```typescript
import { renderWithProviders } from '@/test-utils';
import { screen } from '@testing-library/react';

test('displays text in selected language', () => {
  renderWithProviders(<MyComponent />, {
    preloadedState: {
      app: { language: 'hi' }
    }
  });
  
  expect(screen.getByText('किसान मित्र AI')).toBeInTheDocument();
});
```

## Performance Considerations

1. **Lazy Loading**: Translation files are loaded upfront but could be lazy-loaded per route if needed
2. **Font Loading**: Fonts are loaded on-demand when a language is selected
3. **Caching**: Language preference is cached in localStorage
4. **Bundle Size**: Each translation file adds ~10-15KB to the bundle

## Future Enhancements

1. **Dynamic Translation Loading**: Load translations on-demand to reduce initial bundle size
2. **Translation Management**: Integrate with a translation management system (e.g., Crowdin, Lokalise)
3. **Voice Query Dialect Mapping**: Map voice query dialects to UI languages
4. **Regional Variations**: Support regional variations within languages (e.g., different Hindi dialects)
5. **Pluralization**: Implement proper pluralization rules for each language
6. **Gender-Specific Translations**: Support gender-specific translations where applicable

## Troubleshooting

### Translations not showing

1. Check that the translation key exists in the JSON file
2. Verify the i18n configuration is imported in `main.tsx`
3. Check browser console for i18next errors
4. Ensure LanguageProvider wraps your component tree

### Fonts not loading

1. Check network tab for font loading errors
2. Verify Google Fonts URLs are correct
3. Check Content Security Policy allows Google Fonts
4. Ensure font loading utility is called

### Language not persisting

1. Check localStorage for 'language' key
2. Verify LanguageContext is properly set up
3. Check Redux state for language value
4. Ensure language is set before components render

## Resources

- [react-i18next Documentation](https://react.i18next.com/)
- [i18next Documentation](https://www.i18next.com/)
- [Google Fonts](https://fonts.google.com/)
- [Noto Sans Fonts](https://fonts.google.com/noto/specimen/Noto+Sans)

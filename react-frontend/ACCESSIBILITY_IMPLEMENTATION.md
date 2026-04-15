# Accessibility Implementation Guide

## Overview

This document outlines the accessibility features implemented in the KisanMitra AI React frontend to achieve WCAG 2.1 AA compliance. The implementation focuses on semantic HTML, ARIA attributes, keyboard navigation, screen reader compatibility, and visual accessibility.

## WCAG 2.1 AA Compliance

### Success Criteria Addressed

#### 1. Perceivable

**1.1.1 Non-text Content (Level A)**
- All images include appropriate `alt` text
- Decorative images use `aria-hidden="true"`
- Icons include descriptive labels via `aria-label`

**1.3.1 Info and Relationships (Level A)**
- Semantic HTML5 elements (`<header>`, `<nav>`, `<main>`, `<footer>`)
- Proper heading hierarchy (h1-h6)
- Form labels associated with inputs
- ARIA landmarks for page regions

**1.4.3 Contrast (Minimum) (Level AA)**
- Text contrast ratio: 4.5:1 for normal text
- Large text contrast ratio: 3:1
- Tailwind CSS color palette validated for contrast
- Dark mode support with appropriate contrast

**1.4.11 Non-text Contrast (Level AA)**
- Interactive elements have 3:1 contrast ratio
- Focus indicators clearly visible
- Button and form control borders meet contrast requirements

#### 2. Operable

**2.1.1 Keyboard (Level A)**
- All interactive elements keyboard accessible
- Logical tab order throughout application
- No keyboard traps

**2.1.2 No Keyboard Trap (Level A)**
- Modal dialogs include focus trap with escape key support
- Mobile menu can be closed with Escape key
- Focus returns to trigger element on close

**2.4.1 Bypass Blocks (Level A)**
- Skip link to main content
- Visible on keyboard focus
- Smooth scroll to target

**2.4.3 Focus Order (Level A)**
- Logical tab order follows visual layout
- Focus moves sequentially through interactive elements

**2.4.7 Focus Visible (Level AA)**
- Clear focus indicators on all interactive elements
- Focus ring with 2px outline and offset
- High contrast focus states

#### 3. Understandable

**3.1.1 Language of Page (Level A)**
- `lang` attribute on HTML element
- Language changes announced to screen readers

**3.2.1 On Focus (Level A)**
- No unexpected context changes on focus
- Predictable navigation behavior

**3.3.1 Error Identification (Level A)**
- Form errors clearly identified
- Error messages associated with fields via `aria-describedby`
- Visual and programmatic error indication

**3.3.2 Labels or Instructions (Level A)**
- All form inputs have associated labels
- Required fields indicated
- Input format instructions provided

#### 4. Robust

**4.1.2 Name, Role, Value (Level A)**
- All interactive elements have accessible names
- ARIA roles used appropriately
- State changes announced to screen readers

**4.1.3 Status Messages (Level AA)**
- ARIA live regions for dynamic content
- Loading states announced
- Success/error messages announced

## Implementation Details

### 1. Semantic HTML Structure

```typescript
// App Layout with proper landmarks
<div className="min-h-screen">
  <header role="banner">
    {/* Site header with navigation */}
  </header>
  
  <main id="main-content" role="main" aria-label="Main content">
    {/* Page content */}
  </main>
  
  <footer role="contentinfo">
    {/* Site footer */}
  </footer>
</div>
```

### 2. Skip Links

Skip links allow keyboard users to bypass repetitive navigation:

```typescript
<SkipLink targetId="main-content">
  Skip to main content
</SkipLink>
```

Features:
- Visible only when focused
- Smooth scroll to target
- Sets focus on target element

### 3. ARIA Landmarks and Labels

All major page regions include appropriate ARIA attributes:

```typescript
// Header with navigation
<header role="banner">
  <nav role="navigation" aria-label="Main navigation">
    {/* Navigation links */}
  </nav>
</header>

// Main content area
<main id="main-content" role="main" aria-label="Main content">
  {/* Page content */}
</main>

// Footer
<footer role="contentinfo">
  {/* Footer content */}
</footer>
```

### 4. Keyboard Navigation

#### Focus Management

All interactive elements support keyboard navigation:

```typescript
// Button with focus styles
<button
  className="focus:outline-none focus:ring-2 focus:ring-primary-500 focus:ring-offset-2"
  aria-label="Descriptive label"
>
  Button Text
</button>

// Link with focus styles
<Link
  to="/path"
  className="focus:outline-none focus:ring-2 focus:ring-primary-500 rounded"
>
  Link Text
</Link>
```

#### Modal Focus Trap

Modal dialogs trap focus within the modal:

```typescript
import { FocusTrap } from '@/utils/accessibility';

const focusTrap = new FocusTrap(modalElement);
focusTrap.activate(); // Trap focus in modal
// ... user interaction ...
focusTrap.deactivate(); // Return focus to trigger
```

#### Escape Key Support

Mobile menu and modals support Escape key:

```typescript
useEffect(() => {
  const handleEscape = (event: KeyboardEvent) => {
    if (event.key === 'Escape' && isOpen) {
      onClose();
    }
  };
  document.addEventListener('keydown', handleEscape);
  return () => document.removeEventListener('keydown', handleEscape);
}, [isOpen, onClose]);
```

### 5. Screen Reader Support

#### Live Regions

Dynamic content changes announced to screen readers:

```typescript
<LiveRegion 
  message="Upload complete" 
  priority="polite" 
/>
```

#### Visually Hidden Content

Content visible only to screen readers:

```typescript
<VisuallyHidden>
  Additional context for screen readers
</VisuallyHidden>
```

#### ARIA Labels and Descriptions

Interactive elements include descriptive labels:

```typescript
// Button with aria-label
<button aria-label="Close dialog">
  <XIcon aria-hidden="true" />
</button>

// Input with aria-describedby
<input
  id="email"
  aria-describedby="email-error email-hint"
/>
<span id="email-hint">Enter your email address</span>
<span id="email-error" role="alert">Invalid email format</span>
```

### 6. Form Accessibility

#### Label Association

All form inputs have associated labels:

```typescript
<label htmlFor="phone-number" className="block text-sm font-medium">
  Phone Number
</label>
<input
  id="phone-number"
  type="tel"
  aria-required="true"
  aria-invalid={hasError}
  aria-describedby={hasError ? "phone-error" : undefined}
/>
{hasError && (
  <span id="phone-error" role="alert" className="text-red-600">
    {errorMessage}
  </span>
)}
```

#### Error Handling

Form errors are clearly identified and announced:

```typescript
// Error message with role="alert"
<div role="alert" aria-live="assertive">
  <p>{errorMessage}</p>
</div>

// Field-level error
<input
  aria-invalid={hasError}
  aria-describedby="field-error"
/>
<span id="field-error" role="alert">
  {errorMessage}
</span>
```

### 7. Image Accessibility

#### Alt Text

All images include descriptive alt text:

```typescript
<img 
  src={imageUrl} 
  alt="Soil health card showing pH level of 6.5" 
/>

// Decorative images
<img 
  src={decorativeImage} 
  alt="" 
  aria-hidden="true" 
/>
```

#### Progressive Images

Loading states announced to screen readers:

```typescript
<LazyImage
  src={imageUrl}
  alt="Produce quality grading result"
  loading="lazy"
/>
```

### 8. Loading States

Loading indicators include screen reader announcements:

```typescript
<div 
  role="status" 
  aria-live="polite" 
  aria-label="Loading content"
>
  <LoadingSpinner />
  <span className="sr-only">Loading, please wait...</span>
</div>
```

### 9. Navigation

#### Current Page Indication

Current page indicated with `aria-current`:

```typescript
<Link
  to="/dashboard"
  aria-current={isActive ? 'page' : undefined}
  className={isActive ? 'active-class' : 'default-class'}
>
  Dashboard
</Link>
```

#### Mobile Menu

Mobile navigation includes proper ARIA attributes:

```typescript
<button
  aria-expanded={isMobileMenuOpen}
  aria-controls="mobile-navigation"
  aria-label={isMobileMenuOpen ? 'Close menu' : 'Open menu'}
>
  <MenuIcon aria-hidden="true" />
</button>

<nav
  id="mobile-navigation"
  aria-hidden={!isMobileMenuOpen}
  aria-label="Mobile navigation"
>
  {/* Navigation items */}
</nav>
```

### 10. Color and Contrast

#### Contrast Validation

Utility function to validate contrast ratios:

```typescript
import { meetsContrastRequirement } from '@/utils/accessibility';

const isAccessible = meetsContrastRequirement(
  'rgb(59, 130, 246)', // foreground
  'rgb(255, 255, 255)', // background
  false // isLargeText
);
```

#### Color Independence

Information not conveyed by color alone:

```typescript
// Error state with icon and text
<div className="text-red-600">
  <XCircleIcon aria-hidden="true" />
  <span>Error: Invalid input</span>
</div>

// Success state with icon and text
<div className="text-green-600">
  <CheckCircleIcon aria-hidden="true" />
  <span>Success: Upload complete</span>
</div>
```

### 11. Responsive Design

#### Zoom Support

Application supports browser zoom up to 200%:

```css
/* Relative units for sizing */
.container {
  max-width: 100%;
  padding: 1rem; /* rem units scale with zoom */
}

/* Flexible layouts */
.grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
}
```

#### Touch Targets

Interactive elements meet minimum size requirements:

```css
/* Minimum 44x44px touch target */
.button {
  min-height: 44px;
  min-width: 44px;
  padding: 0.75rem 1rem;
}
```

## Accessibility Utilities

### Available Utilities

Located in `src/utils/accessibility.ts`:

1. **generateAriaId**: Generate unique IDs for ARIA attributes
2. **announceToScreenReader**: Announce messages to screen readers
3. **FocusTrap**: Manage focus within modal dialogs
4. **getContrastRatio**: Calculate color contrast ratios
5. **meetsContrastRequirement**: Validate WCAG contrast requirements
6. **handleArrowKeyNavigation**: Implement arrow key navigation
7. **formatFileSizeForScreenReader**: Format file sizes for screen readers
8. **formatPercentageForScreenReader**: Format percentages for screen readers
9. **announceLoadingState**: Announce loading state changes

### Usage Examples

```typescript
import {
  announceToScreenReader,
  FocusTrap,
  meetsContrastRequirement,
  formatFileSizeForScreenReader
} from '@/utils/accessibility';

// Announce to screen reader
announceToScreenReader('Upload complete', 'polite');

// Focus trap for modal
const focusTrap = new FocusTrap(modalRef.current);
focusTrap.activate();

// Validate contrast
const isAccessible = meetsContrastRequirement('#3B82F6', '#FFFFFF');

// Format file size
const sizeText = formatFileSizeForScreenReader(1024000); // "1 megabytes"
```

## Testing Accessibility

### Manual Testing

1. **Keyboard Navigation**
   - Tab through all interactive elements
   - Verify focus indicators are visible
   - Test Escape key in modals and menus
   - Verify no keyboard traps

2. **Screen Reader Testing**
   - Test with NVDA (Windows)
   - Test with JAWS (Windows)
   - Test with VoiceOver (macOS/iOS)
   - Test with TalkBack (Android)

3. **Zoom Testing**
   - Test at 100%, 150%, and 200% zoom
   - Verify no horizontal scrolling
   - Verify content remains readable

4. **Color Contrast**
   - Use browser DevTools contrast checker
   - Test in light and dark modes
   - Verify all text meets 4.5:1 ratio

### Automated Testing

```typescript
// Accessibility tests with jest-axe
import { axe, toHaveNoViolations } from 'jest-axe';

expect.extend(toHaveNoViolations);

test('should have no accessibility violations', async () => {
  const { container } = render(<Component />);
  const results = await axe(container);
  expect(results).toHaveNoViolations();
});
```

### Browser Extensions

Recommended tools for testing:
- **axe DevTools**: Automated accessibility testing
- **WAVE**: Visual accessibility evaluation
- **Lighthouse**: Performance and accessibility audit
- **Color Contrast Analyzer**: Contrast ratio checking

## Common Patterns

### Accessible Button

```typescript
<button
  type="button"
  onClick={handleClick}
  className="focus:outline-none focus:ring-2 focus:ring-primary-500"
  aria-label="Descriptive action"
  disabled={isDisabled}
  aria-disabled={isDisabled}
>
  <Icon aria-hidden="true" />
  <span>Button Text</span>
</button>
```

### Accessible Form Field

```typescript
<div>
  <label htmlFor="field-id" className="block text-sm font-medium">
    Field Label {required && <span aria-label="required">*</span>}
  </label>
  <input
    id="field-id"
    type="text"
    aria-required={required}
    aria-invalid={hasError}
    aria-describedby={hasError ? "field-error" : "field-hint"}
    className="focus:ring-2 focus:ring-primary-500"
  />
  <span id="field-hint" className="text-sm text-gray-600">
    Helpful hint text
  </span>
  {hasError && (
    <span id="field-error" role="alert" className="text-red-600">
      {errorMessage}
    </span>
  )}
</div>
```

### Accessible Modal

```typescript
<Dialog
  open={isOpen}
  onClose={onClose}
  aria-labelledby="dialog-title"
  aria-describedby="dialog-description"
>
  <Dialog.Title id="dialog-title">
    Modal Title
  </Dialog.Title>
  <Dialog.Description id="dialog-description">
    Modal description text
  </Dialog.Description>
  {/* Modal content */}
  <button onClick={onClose} aria-label="Close dialog">
    <XIcon aria-hidden="true" />
  </button>
</Dialog>
```

## Checklist

### Before Deployment

- [ ] All images have alt text
- [ ] All interactive elements keyboard accessible
- [ ] Focus indicators visible on all elements
- [ ] Skip link implemented and functional
- [ ] ARIA landmarks on all major regions
- [ ] Form labels associated with inputs
- [ ] Error messages announced to screen readers
- [ ] Loading states announced
- [ ] Color contrast meets WCAG AA standards
- [ ] Application works at 200% zoom
- [ ] No keyboard traps
- [ ] Escape key closes modals/menus
- [ ] Current page indicated in navigation
- [ ] Screen reader testing completed
- [ ] Automated accessibility tests passing

## Resources

### WCAG Guidelines
- [WCAG 2.1 Quick Reference](https://www.w3.org/WAI/WCAG21/quickref/)
- [Understanding WCAG 2.1](https://www.w3.org/WAI/WCAG21/Understanding/)

### ARIA Practices
- [ARIA Authoring Practices Guide](https://www.w3.org/WAI/ARIA/apg/)
- [ARIA in HTML](https://www.w3.org/TR/html-aria/)

### Testing Tools
- [axe DevTools](https://www.deque.com/axe/devtools/)
- [WAVE Browser Extension](https://wave.webaim.org/extension/)
- [Lighthouse](https://developers.google.com/web/tools/lighthouse)

### Screen Readers
- [NVDA](https://www.nvaccess.org/) (Windows, Free)
- [JAWS](https://www.freedomscientific.com/products/software/jaws/) (Windows)
- VoiceOver (macOS/iOS, Built-in)
- TalkBack (Android, Built-in)

## Maintenance

### Regular Audits

Schedule regular accessibility audits:
- Run automated tests with each deployment
- Manual keyboard testing monthly
- Screen reader testing quarterly
- Full WCAG audit annually

### Continuous Improvement

- Monitor user feedback for accessibility issues
- Stay updated with WCAG guidelines
- Test with real assistive technology users
- Update documentation as patterns evolve

## Support

For accessibility questions or issues:
- Review this documentation
- Check WCAG 2.1 guidelines
- Test with accessibility tools
- Consult with accessibility specialists

---

**Last Updated**: December 2024
**WCAG Version**: 2.1 Level AA
**Compliance Status**: In Progress

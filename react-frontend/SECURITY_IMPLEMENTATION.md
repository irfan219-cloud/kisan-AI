# Security Implementation Guide

## Overview

This document outlines the security measures implemented in the KisanMitraAI React frontend application to protect user data and prevent common web vulnerabilities.

## Security Features

### 1. Content Security Policy (CSP)

**Location**: `src/utils/security.ts`, `src/config/csp.ts`

**Implementation**:
- Strict CSP directives to prevent XSS attacks
- Whitelist-based approach for allowed sources
- Automatic CSP meta tag injection
- Environment-specific configurations

**Usage**:
```typescript
import { initializeSecurity } from './config/csp';

// Initialize on app startup
initializeSecurity();
```

**Directives**:
- `default-src 'self'` - Only allow resources from same origin
- `script-src 'self'` - Restrict script execution
- `connect-src` - Whitelist API endpoints
- `frame-ancestors 'none'` - Prevent clickjacking
- `upgrade-insecure-requests` - Force HTTPS

### 2. File Upload Security

**Location**: `src/utils/security.ts`, `src/utils/malwareScanning.ts`, `src/utils/fileValidation.ts`

**Features**:
- File type validation (MIME type and extension)
- File size limits enforcement
- Suspicious file extension detection
- Magic byte verification
- Client-side malware signature scanning
- Path traversal prevention
- Filename sanitization

**Usage**:
```typescript
import { validateFile } from './utils/fileValidation';
import { performSecurityScan } from './utils/malwareScanning';

// Validate file
const validation = await validateFile(file, options);

// Perform security scan
const scan = await performSecurityScan(file);
```

**Protected Against**:
- Executable file uploads (.exe, .bat, .sh, etc.)
- MIME type spoofing
- Path traversal attacks
- Malicious file signatures
- Embedded scripts in files

### 3. Data Masking

**Location**: `src/utils/security.ts`

**Features**:
- Phone number masking
- Email address masking
- Aadhaar number masking
- Bank account masking
- Automatic sensitive data detection

**Usage**:
```typescript
import { maskPhoneNumber, maskEmail, maskAadhaar } from './utils/security';

const masked = maskPhoneNumber('9876543210'); // "98****10"
const maskedEmail = maskEmail('user@example.com'); // "us****@example.com"
```

### 4. Secure Data Storage

**Location**: `src/utils/security.ts`

**Features**:
- Prefixed storage keys
- Sensitive data detection before storage
- Never store tokens in localStorage
- Automatic storage availability checks
- Secure cleanup on logout

**Usage**:
```typescript
import { secureLocalStorage } from './utils/security';

const storage = secureLocalStorage();
storage.setItem('preferences', JSON.stringify(prefs));
```

**Important**: Never store sensitive data like tokens, passwords, or personal information in localStorage.

### 5. Client-Side Encryption

**Location**: `src/utils/encryption.ts`

**Features**:
- AES-GCM encryption
- PBKDF2 key derivation
- Secure random IV and salt generation
- SHA-256 hashing
- Encrypted form data transmission

**Usage**:
```typescript
import { encryptData, decryptData } from './utils/encryption';

// Encrypt sensitive data
const { encrypted, salt, iv } = await encryptData(data, password);

// Decrypt data
const decrypted = await decryptData(encrypted, password, salt, iv);
```

**Use Cases**:
- Encrypting sensitive form data before transmission
- Protecting cached sensitive information
- Secure temporary data storage

### 6. Privacy-Compliant Logging

**Location**: `src/utils/privacyCompliantLogging.ts`

**Features**:
- Automatic sensitive data redaction
- Field-based filtering (password, token, etc.)
- Pattern-based detection
- Privacy-safe error reports
- Development vs production modes

**Usage**:
```typescript
import { logger, logApiError, logUserAction } from './utils/privacyCompliantLogging';

// Log with automatic redaction
logger.info('User logged in', { userId: '123', token: 'secret' });
// Output: { userId: '123', token: '[REDACTED]' }

// Log API errors
logApiError('/api/endpoint', error, requestData);

// Log user actions
logUserAction('file_upload', { fileType: 'image/jpeg' });
```

**Redacted Fields**:
- password, token, accessToken, refreshToken
- secret, apiKey
- phoneNumber, email, aadhaar
- accountNumber, cardNumber, cvv, pin, otp

### 7. Data Retention and Cleanup

**Location**: `src/utils/dataRetention.ts`

**Features**:
- Automatic data expiration
- Scheduled cleanup tasks
- GDPR-compliant data deletion
- User data export
- Retention period enforcement

**Retention Periods**:
- User preferences: 365 days
- Cached data: 30 days
- Offline queue: 7 days
- Error logs: 7 days
- Session data: 1 day
- Temporary files: 1 day

**Usage**:
```typescript
import { 
  retentionManager, 
  cleanupScheduler,
  deleteAllUserData,
  exportUserData 
} from './utils/dataRetention';

// Store with retention
retentionManager.setWithRetention('key', value, 30); // 30 days

// Start automatic cleanup
cleanupScheduler.start(24); // Every 24 hours

// GDPR compliance
await deleteAllUserData(); // Delete all user data
const exported = exportUserData(); // Export user data
```

### 8. XSS Prevention

**Location**: `src/utils/security.ts`

**Features**:
- HTML sanitization
- URL validation for redirects
- Input validation
- Output encoding

**Usage**:
```typescript
import { sanitizeHTML, isValidRedirectURL } from './utils/security';

// Sanitize HTML content
const safe = sanitizeHTML(userInput);

// Validate redirect URLs
if (isValidRedirectURL(url)) {
  window.location.href = url;
}
```

### 9. Rate Limiting

**Location**: `src/utils/security.ts`

**Features**:
- Client-side rate limiting
- Configurable attempt limits
- Time window management
- Per-key tracking

**Usage**:
```typescript
import { RateLimiter } from './utils/security';

const limiter = new RateLimiter(5, 60000); // 5 attempts per minute

if (limiter.isAllowed('login')) {
  // Proceed with login
} else {
  // Show rate limit error
}
```

### 10. Security Headers Validation

**Location**: `src/utils/security.ts`

**Features**:
- Response header validation
- Missing header detection
- Security posture monitoring

**Expected Headers**:
- X-Content-Type-Options: nosniff
- X-Frame-Options: DENY
- X-XSS-Protection: 1; mode=block
- Strict-Transport-Security: max-age=31536000

## Integration with Backend

### Malware Scanning

The frontend performs basic client-side validation, but relies on the backend `MalwareScanningService` for comprehensive malware detection:

```typescript
// Backend integration point
import { requestBackendScan } from './utils/malwareScanning';

// After file upload to S3
const scanResult = await requestBackendScan(fileKey);
if (!scanResult.isClean) {
  // Handle infected file
}
```

### Encryption Service

The backend `EncryptionService` provides server-side encryption for data at rest. The frontend encryption is supplementary for data in transit and temporary storage.

### Data Deletion

The backend `DataDeletionService` handles permanent data deletion. The frontend cleanup removes local cached data only.

## Security Best Practices

### DO:
✅ Use HTTPS in production
✅ Validate all user inputs
✅ Sanitize data before display
✅ Use CSP headers
✅ Implement rate limiting
✅ Log security events
✅ Encrypt sensitive data in transit
✅ Use secure random tokens
✅ Implement automatic session timeout
✅ Clear sensitive data on logout

### DON'T:
❌ Store tokens in localStorage
❌ Store passwords anywhere
❌ Trust client-side validation alone
❌ Log sensitive information
❌ Use eval() or innerHTML with user data
❌ Disable security features for convenience
❌ Hardcode secrets or API keys
❌ Allow arbitrary file uploads
❌ Trust MIME types without verification
❌ Keep data longer than necessary

## Initialization

Add security initialization to your app entry point:

```typescript
// src/main.tsx
import { initializeSecurity } from './config/csp';
import { initializeDataRetention } from './utils/dataRetention';

// Initialize security
initializeSecurity();
initializeDataRetention();

// Then render app
ReactDOM.createRoot(document.getElementById('root')!).render(<App />);
```

## Testing Security Features

### File Upload Security
```typescript
// Test malicious file detection
const maliciousFile = new File([...], 'malware.exe');
const scan = await performSecurityScan(maliciousFile);
expect(scan.isSafe).toBe(false);
```

### Data Masking
```typescript
// Test phone masking
const masked = maskPhoneNumber('9876543210');
expect(masked).toBe('98****10');
```

### Encryption
```typescript
// Test encryption/decryption
const { encrypted, salt, iv } = await encryptData('secret', 'password');
const decrypted = await decryptData(encrypted, 'password', salt, iv);
expect(decrypted).toBe('secret');
```

## Compliance

### GDPR
- Right to access: `exportUserData()`
- Right to deletion: `deleteAllUserData()`
- Data minimization: Automatic retention policies
- Privacy by design: Logging redaction

### Security Standards
- OWASP Top 10 protection
- CSP Level 3 compliance
- Secure coding practices
- Regular security audits

## Monitoring and Alerts

Security events are logged and can be monitored:

```typescript
import { logSecurityEvent } from './utils/privacyCompliantLogging';

// Log security events
logSecurityEvent('suspicious_file_upload', 'high', {
  filename: 'malware.exe',
  reason: 'Executable file detected'
});
```

## Updates and Maintenance

- Review security policies quarterly
- Update CSP directives as needed
- Monitor for new vulnerabilities
- Keep dependencies updated
- Conduct security audits
- Train team on security practices

## Support

For security concerns or to report vulnerabilities, contact the security team.

**Note**: This is a living document. Update as security features evolve.

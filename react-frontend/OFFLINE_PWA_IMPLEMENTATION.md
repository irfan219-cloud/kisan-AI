# Offline Capabilities and PWA Features Implementation

## Overview

This document describes the implementation of offline capabilities and Progressive Web App (PWA) features for the KisanMitra AI React frontend application.

## Features Implemented

### 1. Offline Data Management

#### IndexedDB Storage Service
- **File**: `src/services/offlineStorageService.ts`
- **Purpose**: Provides persistent storage for offline data using IndexedDB
- **Features**:
  - Cache data with TTL (time-to-live)
  - Queue API requests for later synchronization
  - Store files for offline upload
  - Automatic cleanup of expired cache entries
  - Storage statistics tracking

#### Key Functions:
```typescript
- cacheData(key, data, ttlMinutes): Cache data with expiration
- getCachedData<T>(key): Retrieve cached data
- queueRequest(method, url, data, files, priority): Queue API request
- getQueuedRequests(): Get all queued requests sorted by priority
- storeFile(file): Store file for offline upload
- clearExpiredCache(): Remove expired cache entries
```

### 2. Synchronization and Conflict Resolution

#### Sync Service
- **File**: `src/services/syncService.ts`
- **Purpose**: Handles automatic synchronization when connectivity returns
- **Features**:
  - Automatic sync on reconnection
  - Retry mechanism with exponential backoff
  - Conflict detection and resolution
  - Sync status tracking
  - Event listeners for sync completion

#### Conflict Resolution
- **File**: `src/components/offline/ConflictResolutionDialog.tsx`
- **Purpose**: UI for resolving data conflicts
- **Options**:
  - Use local changes
  - Use server version
  - Merge both versions

#### Sync Status Component
- **File**: `src/components/offline/SyncStatus.tsx`
- **Purpose**: Display sync progress and status
- **Features**:
  - Real-time sync status
  - Success/failure indicators
  - Detailed error reporting
  - Last sync time display

### 3. Connectivity Monitoring

#### Connectivity Service
- **File**: `src/services/connectivityService.ts`
- **Purpose**: Monitor online/offline status and trigger sync
- **Features**:
  - Online/offline event listeners
  - Connection quality detection (slow/fast)
  - Periodic connectivity checks
  - Automatic sync trigger on reconnection
  - Status change notifications

#### Offline Indicator
- **File**: `src/components/offline/OfflineIndicator.tsx`
- **Purpose**: Visual indicator of connectivity status
- **Features**:
  - Online/offline status display
  - Queued items count
  - Sync progress indicator
  - Manual sync trigger
  - Connection quality warnings

### 4. PWA Features

#### Service Worker Configuration
- **File**: `vite.config.ts`
- **Plugin**: `vite-plugin-pwa`
- **Features**:
  - Automatic service worker generation
  - Workbox integration for caching strategies
  - Runtime caching for API and assets
  - Background sync support
  - Auto-update on new version

#### Caching Strategies:
1. **API Cache**: NetworkFirst strategy with 24-hour expiration
2. **S3 Assets**: CacheFirst strategy with 7-day expiration
3. **Images**: CacheFirst strategy with 30-day expiration

#### App Manifest
- **File**: `public/manifest.json`
- **Features**:
  - App name and description
  - Icons for various sizes (72x72 to 512x512)
  - Theme and background colors
  - Display mode: standalone
  - App shortcuts for quick access
  - Screenshots for app stores

#### PWA Service
- **File**: `src/services/pwaService.ts`
- **Purpose**: Manage PWA installation and updates
- **Features**:
  - Install prompt handling
  - Update notifications
  - Installation status checking
  - Event listeners for install/update

#### Install Prompt Component
- **File**: `src/components/pwa/InstallPrompt.tsx`
- **Purpose**: Prompt users to install the app
- **Features**:
  - Dismissible prompt
  - One-time display (can be dismissed permanently)
  - Installation progress indicator

#### Update Notification Component
- **File**: `src/components/pwa/UpdateNotification.tsx`
- **Purpose**: Notify users of available updates
- **Features**:
  - Update now or later options
  - Automatic reload on update
  - Update progress indicator

#### Offline Page
- **File**: `public/offline.html`
- **Purpose**: Fallback page when offline
- **Features**:
  - Friendly offline message
  - List of available offline features
  - Automatic reconnection detection
  - Retry button

## Integration

### App.tsx Updates
The main App component has been updated to initialize offline and PWA services:

```typescript
import { OfflineIndicator } from '@/components/offline/OfflineIndicator'
import { InstallPrompt } from '@/components/pwa/InstallPrompt'
import { UpdateNotification } from '@/components/pwa/UpdateNotification'
import { connectivityService } from '@/services/connectivityService'
import { pwaService } from '@/services/pwaService'
import { useOnlineStatus } from '@/hooks/useOnlineStatus'

// Initialize services on app mount
useEffect(() => {
  connectivityService.init();
  pwaService.init();
  return () => connectivityService.cleanup();
}, []);
```

### Redux Store Integration
The offline slice in Redux manages:
- Queued requests
- Cached data
- Sync status
- Last sync time

## Usage Examples

### Caching Data for Offline Access
```typescript
import { offlineStorageService } from '@/services/offlineStorageService';

// Cache soil analysis results
await offlineStorageService.cacheData(
  'soil-analysis-123',
  soilData,
  60 // Cache for 60 minutes
);

// Retrieve cached data
const cachedData = await offlineStorageService.getCachedData('soil-analysis-123');
```

### Queuing Requests When Offline
```typescript
import { offlineStorageService } from '@/services/offlineStorageService';

// Queue API request
const requestId = await offlineStorageService.queueRequest(
  'POST',
  '/api/soil-analysis',
  { farmerId: '123', data: soilData },
  [imageFile],
  'high' // Priority
);
```

### Monitoring Connectivity
```typescript
import { connectivityService } from '@/services/connectivityService';

// Subscribe to status changes
const unsubscribe = connectivityService.onStatusChange((status) => {
  console.log('Connectivity status:', status); // 'online' | 'offline' | 'slow'
});

// Check current status
const status = connectivityService.getStatus();
const info = connectivityService.getInfo();
```

### Triggering Manual Sync
```typescript
import { syncService } from '@/services/syncService';

// Start sync manually
const result = await syncService.startSync();
console.log(`Synced: ${result.syncedCount}, Failed: ${result.failedCount}`);

// Subscribe to sync completion
const unsubscribe = syncService.onSyncComplete((result) => {
  console.log('Sync completed:', result);
});
```

### PWA Installation
```typescript
import { pwaService } from '@/services/pwaService';

// Check if app can be installed
if (pwaService.canInstall()) {
  // Prompt user to install
  const accepted = await pwaService.promptInstall();
}

// Check if already installed
const isInstalled = pwaService.isInstalled();
```

## Dependencies Added

```json
{
  "dependencies": {
    "idb": "^8.0.0",
    "workbox-window": "^7.0.0"
  },
  "devDependencies": {
    "vite-plugin-pwa": "^0.19.0",
    "workbox-precaching": "^7.0.0",
    "workbox-routing": "^7.0.0",
    "workbox-strategies": "^7.0.0",
    "workbox-core": "^7.0.0"
  }
}
```

## Translations

Offline-related translations have been added to both English and Hindi translation files:

### English (`src/i18n/locales/en/translation.json`)
- offline.online: "Online"
- offline.offline: "Offline"
- offline.syncing: "Syncing..."
- offline.offlineMessage: "You're offline. Changes will sync when connection returns."
- offline.queuedItems: "{{count}} items queued for sync"
- And more...

### Hindi (`src/i18n/locales/hi/translation.json`)
- offline.online: "ऑनलाइन"
- offline.offline: "ऑफ़लाइन"
- offline.syncing: "सिंक हो रहा है..."
- And more...

## Testing

### Manual Testing Checklist

1. **Offline Mode**:
   - [ ] Disconnect from internet
   - [ ] Verify offline indicator appears
   - [ ] Try uploading a file (should queue)
   - [ ] Try making API request (should queue)
   - [ ] Reconnect to internet
   - [ ] Verify automatic sync starts
   - [ ] Verify queued items are processed

2. **PWA Installation**:
   - [ ] Visit app in Chrome/Edge
   - [ ] Verify install prompt appears
   - [ ] Click install
   - [ ] Verify app installs successfully
   - [ ] Launch installed app
   - [ ] Verify standalone mode

3. **Service Worker**:
   - [ ] Build app for production
   - [ ] Serve production build
   - [ ] Verify service worker registers
   - [ ] Check cached assets in DevTools
   - [ ] Test offline functionality

4. **Caching**:
   - [ ] Load a page with data
   - [ ] Go offline
   - [ ] Reload page
   - [ ] Verify cached data displays
   - [ ] Verify appropriate offline message

5. **Sync Conflicts**:
   - [ ] Make changes offline
   - [ ] Make conflicting changes on server
   - [ ] Reconnect
   - [ ] Verify conflict dialog appears
   - [ ] Test each resolution option

## Performance Considerations

1. **IndexedDB**: Asynchronous operations don't block UI
2. **Service Worker**: Runs in background thread
3. **Caching**: Reduces network requests and improves load times
4. **Lazy Loading**: Components loaded on demand
5. **Code Splitting**: Vendor chunks separated for better caching

## Browser Support

- Chrome/Edge: Full support
- Firefox: Full support
- Safari: Partial support (no install prompt)
- Mobile browsers: Full support on Android, partial on iOS

## Future Enhancements

1. Background sync for queued operations
2. Push notifications for sync completion
3. Advanced conflict resolution strategies
4. Offline analytics tracking
5. Periodic background sync
6. Share target API integration
7. File system access API for downloads

## Troubleshooting

### Service Worker Not Registering
- Check browser console for errors
- Verify HTTPS (required for service workers)
- Clear browser cache and reload
- Check vite.config.ts configuration

### Offline Data Not Syncing
- Check network connectivity
- Verify sync service is initialized
- Check browser console for sync errors
- Verify authentication tokens are valid

### Install Prompt Not Showing
- Verify manifest.json is accessible
- Check PWA criteria in Lighthouse
- Ensure HTTPS is enabled
- Clear browser data and retry

## Resources

- [Workbox Documentation](https://developers.google.com/web/tools/workbox)
- [PWA Best Practices](https://web.dev/pwa-checklist/)
- [IndexedDB API](https://developer.mozilla.org/en-US/docs/Web/API/IndexedDB_API)
- [Service Worker API](https://developer.mozilla.org/en-US/docs/Web/API/Service_Worker_API)

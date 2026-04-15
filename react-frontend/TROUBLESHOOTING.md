# React Frontend Troubleshooting Guide

## Black Screen Issue

If you're seeing a black screen when running the frontend, follow these steps:

### Step 1: Check Browser Console

1. Open Developer Tools (F12)
2. Go to Console tab
3. Look for error messages

### Common Issues and Solutions

#### Issue 1: Secure Context Error
**Error**: "Application must run in a secure context (HTTPS)"
**Solution**: This has been fixed to only show a warning. Restart the dev server.

#### Issue 2: Module Not Found
**Error**: "Cannot find module '@/components/...'"
**Solution**: 
```bash
cd react-frontend
npm install
npm run dev
```

#### Issue 3: Missing Dependencies
**Error**: Various import errors
**Solution**:
```bash
cd react-frontend
rm -rf node_modules package-lock.json
npm install
npm run dev
```

#### Issue 4: Port Already in Use
**Error**: "Port 3000 is already in use"
**Solution**:
- Kill the process using port 3000
- Or change the port in `vite.config.ts`

#### Issue 5: TypeScript Errors
**Error**: Type errors in console
**Solution**:
```bash
npm run type-check
```

### Step 2: Verify Backend Connection

1. Check backend is running: http://localhost:5001/swagger
2. Check CORS is enabled (should be by default)
3. Verify `.env` file has correct API URL:
   ```
   VITE_API_BASE_URL=http://localhost:5001/api
   ```

### Step 3: Check Network Tab

1. Open Developer Tools (F12)
2. Go to Network tab
3. Refresh the page
4. Look for failed requests (red status codes)

### Step 4: Clear Cache

Sometimes browser cache causes issues:

1. Open Developer Tools (F12)
2. Right-click the refresh button
3. Select "Empty Cache and Hard Reload"

### Step 5: Check for Missing Files

Run this command to verify all required files exist:

```bash
cd react-frontend
npm run type-check
```

## Quick Fixes

### Fix 1: Restart Dev Server

```bash
# Stop the current server (Ctrl+C)
cd react-frontend
npm run dev
```

### Fix 2: Reinstall Dependencies

```bash
cd react-frontend
rm -rf node_modules
npm install
npm run dev
```

### Fix 3: Check Node Version

Ensure you're using Node.js 18 or higher:

```bash
node --version
```

If not, update Node.js.

## Testing Without Backend

To test if the issue is frontend-only:

1. Comment out API calls temporarily
2. Check if the UI loads
3. If it loads, the issue is with backend connection

## Getting Help

If none of these solutions work:

1. Share the exact error message from browser console
2. Share the terminal output from `npm run dev`
3. Share your Node.js version: `node --version`
4. Share your npm version: `npm --version`

## Common Error Messages

### "Failed to fetch"
- Backend is not running
- CORS issue
- Wrong API URL in `.env`

### "Cannot read property of undefined"
- Missing data from API
- Component expecting data that doesn't exist
- Check browser console for specific component

### "Hydration failed"
- React version mismatch
- Server/client rendering mismatch
- Usually fixed by clearing cache

### "Module not found"
- Missing dependency
- Run `npm install`
- Check import paths

## Development Mode Issues

### Hot Module Replacement (HMR) Not Working

```bash
# Restart dev server
npm run dev
```

### Slow Performance

```bash
# Clear Vite cache
rm -rf node_modules/.vite
npm run dev
```

## Production Build Issues

### Build Fails

```bash
# Check for TypeScript errors
npm run type-check

# Check for linting errors
npm run lint

# Try building
npm run build
```

### Build Succeeds but App Doesn't Work

```bash
# Preview the production build
npm run preview
```

## Environment Variables

Ensure your `.env` file exists and has these variables:

```env
VITE_API_BASE_URL=http://localhost:5001/api
VITE_API_TIMEOUT=30000
VITE_AWS_REGION=ap-south-1
VITE_AWS_S3_BUCKET=kisan-mitra-uploads
VITE_COGNITO_USER_POOL_ID=ap-south-1_xxxxxxxxx
VITE_COGNITO_CLIENT_ID=xxxxxxxxxxxxxxxxxxxxxxxxxx
VITE_COGNITO_DOMAIN=kisan-mitra-auth
VITE_APP_NAME=KisanMitra AI
VITE_APP_VERSION=1.0.0
VITE_DEFAULT_LANGUAGE=hi
VITE_ENABLE_OFFLINE_MODE=true
VITE_ENABLE_VOICE_QUERIES=true
VITE_ENABLE_ANALYTICS=false
VITE_ENABLE_DEVTOOLS=true
VITE_LOG_LEVEL=info
```

## Still Having Issues?

1. Check if the issue persists in incognito/private mode
2. Try a different browser
3. Check firewall settings
4. Verify antivirus isn't blocking the connection

## Success Indicators

When everything is working, you should see:

1. ✅ Frontend loads at http://localhost:3000
2. ✅ No errors in browser console
3. ✅ Network tab shows successful requests
4. ✅ Login page or dashboard appears
5. ✅ Navigation works

## Next Steps After Fixing

Once the frontend loads:

1. Test navigation between pages
2. Test form interactions
3. Check responsive design (resize browser)
4. Test accessibility features (Tab key navigation)
5. Check browser console for warnings

---

**Last Updated**: December 2024


## Recent Issues and Fixes

### Issue: process.env is not defined

**Problem**: Browser console shows "Uncaught ReferenceError: process is not defined"

**Root Cause**: Vite uses `import.meta.env` instead of `process.env` for environment variables.

**Files Fixed**:
- `src/utils/security.ts` (line 18)
- `src/store/index.ts` (line 19)

**Solution Applied**:
1. Replaced `process.env.NODE_ENV` with `import.meta.env.DEV`
2. Added process.env compatibility shim in vite.config.ts

**Status**: ✅ Fixed

### Issue: AuthContext Error - "useAuth must be used within an AuthProvider"

**Problem**: Application throws error "useAuth must be used within an AuthProvider" even though AuthProvider is present in the component tree.

**Root Cause**: Race condition during Redux store initialization. The AuthContext was trying to use Redux hooks before the store was fully initialized, causing the context value to be undefined temporarily.

**Files Fixed**:
- `src/contexts/AuthContext.tsx`

**Solution Applied**:
1. Added fallback values when destructuring Redux state in AuthProvider
2. Changed from direct destructuring to safe destructuring with defaults:
   ```typescript
   const authState = useAppSelector((state) => state.auth);
   const { 
     user = null, 
     isAuthenticated = false, 
     isLoading = false, 
     error = null, 
     tokens = { accessToken: null, refreshToken: null, expiresAt: null }
   } = authState || {};
   ```
3. This ensures the context value is always defined, even during initialization

**Status**: ✅ Fixed

### Testing Credentials

For UI/UX testing without AWS services:
- Phone: `1234567890`
- Password: `password`

These hardcoded credentials are configured in `AuthContext.tsx` for development testing.


### Issue: Duplicate /api in API URLs

**Problem**: API requests show duplicate `/api` in the URL path (e.g., `http://localhost:5001/api/api/v1/...`)

**Root Cause**: The base URL in `.env` file included `/api`, but the service endpoints also add `/api/v1/...`, causing duplication.

**Files Fixed**:
- `react-frontend/.env`

**Solution Applied**:
1. Changed `VITE_API_BASE_URL` from `http://localhost:5001/api` to `http://localhost:5001`
2. Services now correctly construct URLs like `http://localhost:5001/api/v1/quality-grading/grade`

**Status**: ✅ Fixed

**Note**: After changing the `.env` file, you need to restart the Vite dev server for the changes to take effect.

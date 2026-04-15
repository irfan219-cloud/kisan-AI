import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App.tsx'
import './index.css'
import './i18n/config'
import { initPerformanceMonitoring } from './utils/performanceMonitoring'
import { initializeSecurity, validateSecureContext } from './config/csp'
import { initializeDataRetention } from './utils/dataRetention'

// Validate secure context (only warn in development, don't block)
if (!validateSecureContext()) {
  // Running in insecure context - some features may be limited
}

// Initialize security features (wrapped in try-catch to prevent blocking)
try {
  initializeSecurity();
  initializeDataRetention();
} catch (error) {
  // Security initialization failed - continue anyway
}

// Initialize performance monitoring in development
if (import.meta.env.DEV) {
  initPerformanceMonitoring();
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)
import React, { useState } from 'react';
import { useAuth } from '@/contexts/AuthContext';
import { useLanguage } from '@/contexts/LanguageContext';

interface LoginFormProps {
  onSuccess?: () => void;
  onError?: (error: string) => void;
}

export const LoginForm: React.FC<LoginFormProps> = ({ onSuccess, onError }) => {
  const { login, isLoading, error: authError } = useAuth();
  const { t } = useLanguage();
  
  const [phoneNumber, setPhoneNumber] = useState('');
  const [password, setPassword] = useState('');
  const [validationErrors, setValidationErrors] = useState<{
    phoneNumber?: string;
    password?: string;
  }>({});

  const validateForm = (): boolean => {
    const errors: { phoneNumber?: string; password?: string } = {};

    // Phone number validation (Indian format)
    // Accepts: +919876543210, 919876543210, 9876543210 (real numbers starting with 6-9)
    // Also accepts: +911234567890 (test number for development)
    const phoneWithoutSpaces = phoneNumber.replace(/\s/g, '');
    const realPhoneRegex = /^(\+91|91)?[6-9]\d{9}$/;
    const testPhoneRegex = /^(\+91)?1234567890$/; // Test user phone
    
    if (!phoneNumber.trim()) {
      errors.phoneNumber = t('auth.errors.phoneRequired', 'Phone number is required');
    } else if (!realPhoneRegex.test(phoneWithoutSpaces) && !testPhoneRegex.test(phoneWithoutSpaces)) {
      errors.phoneNumber = t('auth.errors.phoneInvalid', 'Please enter a valid phone number');
    }

    // Password validation
    if (!password) {
      errors.password = t('auth.errors.passwordRequired', 'Password is required');
    } else if (password.length < 8) {
      errors.password = t('auth.errors.passwordTooShort', 'Password must be at least 8 characters');
    }

    setValidationErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!validateForm()) {
      return;
    }

    try {
      // Format phone number to E.164 format if needed
      let formattedPhone = phoneNumber.replace(/\s/g, '');
      if (!formattedPhone.startsWith('+')) {
        formattedPhone = formattedPhone.startsWith('91') 
          ? `+${formattedPhone}` 
          : `+91${formattedPhone}`;
      }

      await login(formattedPhone, password);
      onSuccess?.();
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Login failed';
      onError?.(errorMessage);
    }
  };

  const handlePhoneChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setPhoneNumber(e.target.value);
    if (validationErrors.phoneNumber) {
      setValidationErrors({ ...validationErrors, phoneNumber: undefined });
    }
  };

  const handlePasswordChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setPassword(e.target.value);
    if (validationErrors.password) {
      setValidationErrors({ ...validationErrors, password: undefined });
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      <div>
        <label
          htmlFor="phoneNumber"
          className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2"
        >
          {t('auth.phoneNumber', 'Phone Number')}
        </label>
        <input
          id="phoneNumber"
          name="phoneNumber"
          type="tel"
          autoComplete="tel"
          required
          value={phoneNumber}
          onChange={handlePhoneChange}
          className={`
            w-full px-4 py-2 border rounded-lg
            focus:ring-2 focus:ring-primary-500 focus:border-transparent
            bg-white dark:bg-gray-800 text-gray-900 dark:text-white dark:border-gray-600
            ${validationErrors.phoneNumber ? 'border-red-500' : 'border-gray-300'}
          `}
          placeholder={t('auth.phonePlaceholder', '+91 9876543210')}
          disabled={isLoading}
        />
        {validationErrors.phoneNumber && (
          <p className="mt-1 text-sm text-red-600 dark:text-red-400">
            {validationErrors.phoneNumber}
          </p>
        )}
      </div>

      <div>
        <label
          htmlFor="password"
          className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2"
        >
          {t('auth.password', 'Password')}
        </label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="current-password"
          required
          value={password}
          onChange={handlePasswordChange}
          className={`
            w-full px-4 py-2 border rounded-lg
            focus:ring-2 focus:ring-primary-500 focus:border-transparent
            bg-white dark:bg-gray-800 text-gray-900 dark:text-white dark:border-gray-600
            ${validationErrors.password ? 'border-red-500' : 'border-gray-300'}
          `}
          placeholder={t('auth.passwordPlaceholder', 'Enter your password')}
          disabled={isLoading}
        />
        {validationErrors.password && (
          <p className="mt-1 text-sm text-red-600 dark:text-red-400">
            {validationErrors.password}
          </p>
        )}
      </div>

      {authError && (
        <div className="p-4 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
          <p className="text-sm text-red-600 dark:text-red-400">
            {t(`auth.errors.${authError}`, authError)}
          </p>
        </div>
      )}

      <button
        type="submit"
        disabled={isLoading}
        className={`
          w-full py-3 px-4 rounded-lg font-medium text-white
          transition-colors duration-200
          ${
            isLoading
              ? 'bg-gray-400 cursor-not-allowed'
              : 'bg-primary-600 hover:bg-primary-700 active:bg-primary-800'
          }
          focus:outline-none focus:ring-2 focus:ring-primary-500 focus:ring-offset-2
        `}
      >
        {isLoading ? (
          <span className="flex items-center justify-center">
            <svg
              className="animate-spin -ml-1 mr-3 h-5 w-5 text-white"
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
            >
              <circle
                className="opacity-25"
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                strokeWidth="4"
              />
              <path
                className="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
              />
            </svg>
            {t('auth.loggingIn', 'Logging in...')}
          </span>
        ) : (
          t('auth.login', 'Login')
        )}
      </button>
    </form>
  );
};

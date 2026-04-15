import React, { useState } from 'react';
import { useLanguage } from '@/contexts/LanguageContext';
import { authService } from '@/services/authService';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { useRecaptcha } from '@/hooks/useRecaptcha';

interface RegisterFormProps {
  onSuccess: () => void;
  onError: (error: string) => void;
}

export const RegisterForm: React.FC<RegisterFormProps> = ({ onSuccess, onError }) => {
  const { t } = useLanguage();
  const recaptchaSiteKey = import.meta.env.VITE_RECAPTCHA_SITE_KEY || '';
  const { isReady: isRecaptchaReady, executeRecaptcha } = useRecaptcha(recaptchaSiteKey);
  
  const [formData, setFormData] = useState({
    name: '',
    phoneNumber: '',
    password: '',
    confirmPassword: ''
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [isLoading, setIsLoading] = useState(false);
  const [showConfirmation, setShowConfirmation] = useState(false);
  const [confirmationCode, setConfirmationCode] = useState('');

  const validatePhoneNumber = (phone: string): boolean => {
    // Accept Indian mobile numbers (10 digits starting with 6-9) or test number
    const indianMobileRegex = /^(\+91|91)?[6-9]\d{9}$/;
    const testNumberRegex = /^(\+91|91)?1234567890$/;
    return indianMobileRegex.test(phone) || testNumberRegex.test(phone);
  };

  const validatePassword = (password: string): boolean => {
    // At least 8 characters, 1 uppercase, 1 lowercase, 1 number, 1 special char
    const passwordRegex = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$/;
    return passwordRegex.test(password);
  };

  const validateForm = (): boolean => {
    const newErrors: Record<string, string> = {};

    if (!formData.name.trim()) {
      newErrors.name = t('auth.nameRequired', 'Name is required');
    }

    if (!formData.phoneNumber.trim()) {
      newErrors.phoneNumber = t('auth.phoneRequired', 'Phone number is required');
    } else if (!validatePhoneNumber(formData.phoneNumber)) {
      newErrors.phoneNumber = t('auth.invalidPhone', 'Please enter a valid phone number');
    }

    if (!formData.password) {
      newErrors.password = t('auth.passwordRequired', 'Password is required');
    } else if (!validatePassword(formData.password)) {
      newErrors.password = t(
        'auth.passwordRequirements',
        'Password must be at least 8 characters with uppercase, lowercase, number, and special character'
      );
    }

    if (!formData.confirmPassword) {
      newErrors.confirmPassword = t('auth.confirmPasswordRequired', 'Please confirm your password');
    } else if (formData.password !== formData.confirmPassword) {
      newErrors.confirmPassword = t('auth.passwordMismatch', 'Passwords do not match');
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
    // Clear error for this field
    if (errors[name]) {
      setErrors(prev => ({ ...prev, [name]: '' }));
    }
  };

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!validateForm()) {
      return;
    }

    setIsLoading(true);

    try {
      // Execute reCAPTCHA
      const recaptchaToken = await executeRecaptcha('register');
      if (!recaptchaToken) {
        throw new Error('CAPTCHA verification failed. Please try again.');
      }

      // Normalize phone number (add +91 if not present)
      let normalizedPhone = formData.phoneNumber.trim();
      if (!normalizedPhone.startsWith('+')) {
        if (normalizedPhone.startsWith('91')) {
          normalizedPhone = '+' + normalizedPhone;
        } else {
          normalizedPhone = '+91' + normalizedPhone;
        }
      }

      const response = await authService.register(
        normalizedPhone,
        formData.password,
        formData.name.trim(),
        recaptchaToken
      );

      if (response.requiresConfirmation) {
        // Show OTP confirmation form
        setShowConfirmation(true);
      } else if (response.accessToken && response.refreshToken && response.idToken && response.expiresIn) {
        // User was auto-confirmed and auto-logged in
        // Store tokens and user info
        authService.setTokens(response.accessToken, response.refreshToken, response.expiresIn);
        
        // Decode ID token to get user claims
        const idTokenPayload = authService.decodeIdToken(response.idToken);
        
        // Validate access token to get user info
        const validateResponse = await authService.validateToken(response.accessToken);
        
        const user = {
          id: validateResponse.userId,
          phoneNumber: validateResponse.phoneNumber || normalizedPhone,
          name: idTokenPayload?.name || formData.name.trim(),
          preferredLanguage: idTokenPayload?.['custom:preferred_language'] || 'hi',
        };
        
        authService.setUser(user);
        
        // Call onSuccess to trigger navigation to dashboard
        onSuccess();
      } else {
        // User was auto-confirmed but not auto-logged in
        onSuccess();
      }
    } catch (error: any) {
      const errorMessage = error.userFriendlyMessage || error.message || 'Registration failed';
      onError(errorMessage);
      setErrors({ submit: errorMessage });
    } finally {
      setIsLoading(false);
    }
  };

  const handleConfirmation = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!confirmationCode.trim()) {
      setErrors({ confirmation: t('auth.codeRequired', 'Confirmation code is required') });
      return;
    }

    setIsLoading(true);

    try {
      // Normalize phone number
      let normalizedPhone = formData.phoneNumber.trim();
      if (!normalizedPhone.startsWith('+')) {
        if (normalizedPhone.startsWith('91')) {
          normalizedPhone = '+' + normalizedPhone;
        } else {
          normalizedPhone = '+91' + normalizedPhone;
        }
      }

      await authService.confirmRegistration(normalizedPhone, confirmationCode.trim());
      onSuccess();
    } catch (error: any) {
      const errorMessage = error.userFriendlyMessage || error.message || 'Confirmation failed';
      onError(errorMessage);
      setErrors({ confirmation: errorMessage });
    } finally {
      setIsLoading(false);
    }
  };

  if (showConfirmation) {
    return (
      <form onSubmit={handleConfirmation} className="space-y-6">
        <div>
          <h3 className="text-lg font-medium text-gray-900 dark:text-gray-100 mb-2">
            {t('auth.confirmRegistration', 'Confirm Registration')}
          </h3>
          <p className="text-sm text-gray-600 dark:text-gray-400">
            {t('auth.confirmationPrompt', 'Enter the confirmation code sent to your phone')}
          </p>
        </div>

        <div>
          <label htmlFor="confirmationCode" className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            {t('auth.confirmationCode', 'Confirmation Code')}
          </label>
          <input
            id="confirmationCode"
            type="text"
            value={confirmationCode}
            onChange={(e) => {
              setConfirmationCode(e.target.value);
              if (errors.confirmation) {
                setErrors(prev => ({ ...prev, confirmation: '' }));
              }
            }}
            className={`w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent bg-white text-gray-900 dark:bg-gray-700 dark:border-gray-600 dark:text-white ${
              errors.confirmation ? 'border-red-500' : 'border-gray-300'
            }`}
            placeholder={t('auth.enterCode', 'Enter code')}
            disabled={isLoading}
          />
          {errors.confirmation && (
            <p className="mt-1 text-sm text-red-600 dark:text-red-400">{errors.confirmation}</p>
          )}
        </div>

        <button
          type="submit"
          disabled={isLoading}
          className="w-full flex justify-center items-center py-3 px-4 border border-transparent rounded-lg shadow-sm text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {isLoading ? <LoadingSpinner size="sm" /> : t('auth.confirm', 'Confirm')}
        </button>
      </form>
    );
  }

  return (
    <form onSubmit={handleRegister} className="space-y-6">
      <div>
        <label htmlFor="name" className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
          {t('auth.name', 'Full Name')}
        </label>
        <input
          id="name"
          name="name"
          type="text"
          value={formData.name}
          onChange={handleInputChange}
          className={`w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent bg-white text-gray-900 dark:bg-gray-700 dark:border-gray-600 dark:text-white ${
            errors.name ? 'border-red-500' : 'border-gray-300'
          }`}
          placeholder={t('auth.enterName', 'Enter your full name')}
          disabled={isLoading}
        />
        {errors.name && (
          <p className="mt-1 text-sm text-red-600 dark:text-red-400">{errors.name}</p>
        )}
      </div>

      <div>
        <label htmlFor="phoneNumber" className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
          {t('auth.phoneNumber', 'Phone Number')}
        </label>
        <input
          id="phoneNumber"
          name="phoneNumber"
          type="tel"
          value={formData.phoneNumber}
          onChange={handleInputChange}
          className={`w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent bg-white text-gray-900 dark:bg-gray-700 dark:border-gray-600 dark:text-white ${
            errors.phoneNumber ? 'border-red-500' : 'border-gray-300'
          }`}
          placeholder="+919876543210"
          disabled={isLoading}
        />
        {errors.phoneNumber && (
          <p className="mt-1 text-sm text-red-600 dark:text-red-400">{errors.phoneNumber}</p>
        )}
      </div>

      <div>
        <label htmlFor="password" className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
          {t('auth.password', 'Password')}
        </label>
        <input
          id="password"
          name="password"
          type="password"
          value={formData.password}
          onChange={handleInputChange}
          className={`w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent bg-white text-gray-900 dark:bg-gray-700 dark:border-gray-600 dark:text-white ${
            errors.password ? 'border-red-500' : 'border-gray-300'
          }`}
          placeholder={t('auth.enterPassword', 'Enter password')}
          disabled={isLoading}
        />
        {errors.password && (
          <p className="mt-1 text-sm text-red-600 dark:text-red-400">{errors.password}</p>
        )}
      </div>

      <div>
        <label htmlFor="confirmPassword" className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
          {t('auth.confirmPassword', 'Confirm Password')}
        </label>
        <input
          id="confirmPassword"
          name="confirmPassword"
          type="password"
          value={formData.confirmPassword}
          onChange={handleInputChange}
          className={`w-full px-4 py-2 border rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent bg-white text-gray-900 dark:bg-gray-700 dark:border-gray-600 dark:text-white ${
            errors.confirmPassword ? 'border-red-500' : 'border-gray-300'
          }`}
          placeholder={t('auth.confirmPasswordPlaceholder', 'Re-enter password')}
          disabled={isLoading}
        />
        {errors.confirmPassword && (
          <p className="mt-1 text-sm text-red-600 dark:text-red-400">{errors.confirmPassword}</p>
        )}
      </div>

      {errors.submit && (
        <div className="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
          <p className="text-sm text-red-600 dark:text-red-400">{errors.submit}</p>
        </div>
      )}

      <button
        type="submit"
        disabled={isLoading || !isRecaptchaReady}
        className="w-full flex justify-center items-center py-3 px-4 border border-transparent rounded-lg shadow-sm text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-primary-500 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        {isLoading ? <LoadingSpinner size="sm" /> : t('auth.register', 'Register')}
      </button>

      <p className="text-xs text-gray-500 dark:text-gray-400 text-center">
        {t('auth.recaptchaNotice', 'This site is protected by reCAPTCHA and the Google')}{' '}
        <a href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer" className="text-primary-600 hover:underline">
          {t('auth.privacyPolicy', 'Privacy Policy')}
        </a>{' '}
        {t('auth.and', 'and')}{' '}
        <a href="https://policies.google.com/terms" target="_blank" rel="noopener noreferrer" className="text-primary-600 hover:underline">
          {t('auth.termsOfService', 'Terms of Service')}
        </a>{' '}
        {t('auth.apply', 'apply')}.
      </p>
    </form>
  );
};

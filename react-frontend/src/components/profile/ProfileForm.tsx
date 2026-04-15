import React, { useState, useEffect } from 'react';
import { useAuth } from '@/contexts/AuthContext';
import { useLanguage } from '@/contexts/LanguageContext';
import { useNotifications } from '@/hooks/useNotifications';
import { profileService } from '@/services/profileService';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { UserCircleIcon, MapPinIcon } from '@heroicons/react/24/outline';
import type { UserProfile } from '@/types';

export const ProfileForm: React.FC = () => {
  const { user } = useAuth();
  const { t } = useLanguage();
  const { showSuccess, showError } = useNotifications();

  const [isLoading, setIsLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [formData, setFormData] = useState<UserProfile>({
    name: '',
    phoneNumber: '',
    city: '',
    state: '',
    pincode: '',
  });

  useEffect(() => {
    const loadProfile = async () => {
      if (!user) return;

      setIsLoading(true);
      try {
        const profile = await profileService.getProfile();
        // Always use the profile data from the API
        setFormData({
          name: profile.name || '',
          phoneNumber: profile.phoneNumber || '',
          city: profile.city || '',
          state: profile.state || '',
          pincode: profile.pincode || '',
        });
      } catch (error) {
        console.error('Failed to load profile:', error);
        // Only use user data as fallback if API call fails
        setFormData({
          name: user.name || '',
          phoneNumber: user.phoneNumber || '',
          city: '',
          state: '',
          pincode: '',
        });
      } finally {
        setIsLoading(false);
      }
    };

    loadProfile();
  }, [user]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }));
  };

  const validateForm = (): boolean => {
    if (!formData.name.trim()) {
      showError(
        t('profile.errors.nameRequired') || 'Name is required',
        ''
      );
      return false;
    }

    if (formData.pincode && !/^\d{6}$/.test(formData.pincode)) {
      showError(
        t('profile.errors.invalidPincode') || 'Pincode must be 6 digits',
        ''
      );
      return false;
    }

    return true;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!validateForm()) return;

    setIsSaving(true);
    try {
      await profileService.updateProfile(formData);
      showSuccess(
        t('profile.success.updated') || 'Profile updated successfully',
        ''
      );
    } catch (error) {
      console.error('Failed to update profile:', error);
      showError(
        t('profile.errors.updateFailed') || 'Failed to update profile',
        ''
      );
    } finally {
      setIsSaving(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex justify-center items-center py-12">
        <LoadingSpinner size="lg" />
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      {/* Personal Information Section */}
      <div className="bg-white dark:bg-gray-800 shadow rounded-lg p-6">
        <div className="flex items-center mb-6">
          <UserCircleIcon className="h-6 w-6 text-primary-600 dark:text-primary-400 mr-2" />
          <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
            {t('profile.sections.personal') || 'Personal Information'}
          </h2>
        </div>

        <div className="space-y-4">
          {/* Name */}
          <div>
            <label
              htmlFor="name"
              className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1"
            >
              {t('profile.fields.name') || 'Name'} <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              id="name"
              name="name"
              value={formData.name}
              onChange={handleChange}
              required
              className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              placeholder={t('profile.placeholders.name') || 'Enter your name'}
            />
          </div>

          {/* Phone Number (Read-only) */}
          <div>
            <label
              htmlFor="phoneNumber"
              className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1"
            >
              {t('profile.fields.phoneNumber') || 'Phone Number'}
            </label>
            <input
              type="tel"
              id="phoneNumber"
              name="phoneNumber"
              value={formData.phoneNumber}
              readOnly
              disabled
              className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg bg-gray-100 dark:bg-gray-900 text-gray-500 dark:text-gray-400 cursor-not-allowed"
            />
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              {t('profile.hints.phoneNumber') || 'Phone number cannot be changed'}
            </p>
          </div>
        </div>
      </div>

      {/* Location Information Section */}
      <div className="bg-white dark:bg-gray-800 shadow rounded-lg p-6">
        <div className="flex items-center mb-6">
          <MapPinIcon className="h-6 w-6 text-primary-600 dark:text-primary-400 mr-2" />
          <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
            {t('profile.sections.location') || 'Location Information'}
          </h2>
        </div>

        <div className="space-y-4">
          {/* City */}
          <div>
            <label
              htmlFor="city"
              className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1"
            >
              {t('profile.fields.city') || 'City'}
            </label>
            <input
              type="text"
              id="city"
              name="city"
              value={formData.city}
              onChange={handleChange}
              className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              placeholder={t('profile.placeholders.city') || 'Enter your city'}
            />
          </div>

          {/* State */}
          <div>
            <label
              htmlFor="state"
              className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1"
            >
              {t('profile.fields.state') || 'State'}
            </label>
            <input
              type="text"
              id="state"
              name="state"
              value={formData.state}
              onChange={handleChange}
              className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              placeholder={t('profile.placeholders.state') || 'Enter your state'}
            />
          </div>

          {/* Pincode */}
          <div>
            <label
              htmlFor="pincode"
              className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1"
            >
              {t('profile.fields.pincode') || 'Pincode'}
            </label>
            <input
              type="text"
              id="pincode"
              name="pincode"
              value={formData.pincode}
              onChange={handleChange}
              maxLength={6}
              pattern="\d{6}"
              className="w-full px-4 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent bg-white dark:bg-gray-700 text-gray-900 dark:text-white"
              placeholder={t('profile.placeholders.pincode') || 'Enter 6-digit pincode'}
            />
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              {t('profile.hints.pincode') || 'Enter a valid 6-digit Indian pincode'}
            </p>
          </div>
        </div>
      </div>

      {/* Submit Button */}
      <div className="flex justify-end">
        <button
          type="submit"
          disabled={isSaving}
          className="px-6 py-3 bg-primary-600 hover:bg-primary-700 disabled:bg-gray-400 text-white font-medium rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 focus:ring-offset-2 transition-colors disabled:cursor-not-allowed flex items-center"
        >
          {isSaving ? (
            <>
              <div className="mr-2">
                <LoadingSpinner size="sm" />
              </div>
              {t('profile.buttons.saving') || 'Saving...'}
            </>
          ) : (
            t('profile.buttons.save') || 'Save Changes'
          )}
        </button>
      </div>
    </form>
  );
};

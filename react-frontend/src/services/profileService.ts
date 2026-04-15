import { apiClient } from './apiClient';
import type { UserProfile } from '@/types';

class ProfileService {
  /**
   * Get user profile
   */
  async getProfile(): Promise<UserProfile> {
    const response = await apiClient.get<UserProfile>('/api/v1/profile');
    return response.data;
  }

  /**
   * Update user profile
   */
  async updateProfile(profile: UserProfile): Promise<UserProfile> {
    // Only send fields that can be updated (exclude phoneNumber - it's read-only)
    const updateData = {
      name: profile.name,
      city: profile.city,
      state: profile.state,
      pincode: profile.pincode,
    };
    const response = await apiClient.put<UserProfile>('/api/v1/profile', updateData);
    return response.data;
  }
}

export const profileService = new ProfileService();

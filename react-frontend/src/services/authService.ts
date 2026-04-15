import { User } from '../store/slices/authSlice';
import { apiClient } from './apiClient';

// Request/Response DTOs matching backend
export interface LoginRequest {
  phoneNumber: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  idToken: string;
  expiresIn: number;
  tokenType: string;
}

export interface RefreshRequest {
  refreshToken: string;
}

export interface RefreshResponse {
  accessToken: string;
  idToken: string;
  expiresIn: number;
  tokenType: string;
}

export interface ValidateResponse {
  isValid: boolean;
  userId: string;
  phoneNumber?: string;
  claims?: Record<string, string>;
}

export interface RegisterRequest {
  phoneNumber: string;
  password: string;
  name: string;
  recaptchaToken?: string;
}

export interface RegisterResponse {
  message: string;
  userId?: string;
  requiresConfirmation: boolean;
  accessToken?: string;
  refreshToken?: string;
  idToken?: string;
  expiresIn?: number;
}

export interface ConfirmRequest {
  phoneNumber: string;
  confirmationCode: string;
}

export interface ConfirmResponse {
  message: string;
}

class AuthService {
  /**
   * Login with phone number and password
   */
  async login(phoneNumber: string, password: string): Promise<LoginResponse> {
    return apiClient.post<LoginResponse>(
      '/api/v1/auth/login',
      { phoneNumber, password },
      { skipAuth: true }
    );
  }

  /**
   * Register a new user
   */
  async register(phoneNumber: string, password: string, name: string, recaptchaToken?: string): Promise<RegisterResponse> {
    return apiClient.post<RegisterResponse>(
      '/api/v1/auth/register',
      { phoneNumber, password, name, recaptchaToken },
      { skipAuth: true }
    );
  }

  /**
   * Confirm registration with OTP code
   */
  async confirmRegistration(phoneNumber: string, confirmationCode: string): Promise<ConfirmResponse> {
    return apiClient.post<ConfirmResponse>(
      '/api/v1/auth/confirm',
      { phoneNumber, confirmationCode },
      { skipAuth: true }
    );
  }

  /**
   * Refresh access token
   */
  async refreshToken(refreshToken: string): Promise<RefreshResponse> {
    return apiClient.post<RefreshResponse>(
      '/api/v1/auth/refresh',
      { refreshToken },
      { skipAuth: true, skipRetry: true }
    );
  }

  /**
   * Validate access token
   */
  async validateToken(accessToken: string): Promise<ValidateResponse> {
    return apiClient.get<ValidateResponse>(
      '/api/v1/auth/validate',
      {
        skipAuth: true,
        headers: {
          Authorization: `Bearer ${accessToken}`
        }
      }
    );
  }

  /**
   * Decode ID token to extract user claims (name, custom attributes, etc.)
   * ID tokens contain user profile information
   */
  decodeIdToken(idToken: string): Record<string, any> | null {
    try {
      // JWT tokens have 3 parts separated by dots: header.payload.signature
      const parts = idToken.split('.');
      if (parts.length !== 3) {
        return null;
      }

      // Decode the payload (second part)
      const payload = parts[1];
      // Replace URL-safe characters and add padding if needed
      const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
      const paddedBase64 = base64.padEnd(base64.length + (4 - base64.length % 4) % 4, '=');
      
      // Decode base64 and parse JSON
      const jsonPayload = atob(paddedBase64);
      return JSON.parse(jsonPayload);
    } catch (error) {
      console.error('Failed to decode ID token:', error);
      return null;
    }
  }

  // Token storage using localStorage (secure storage)
  setTokens(accessToken: string, refreshToken: string, expiresIn: number): void {
    const expiresAt = Date.now() + expiresIn * 1000;
    localStorage.setItem('accessToken', accessToken);
    localStorage.setItem('refreshToken', refreshToken);
    localStorage.setItem('expiresAt', expiresAt.toString());
  }

  getAccessToken(): string | null {
    return localStorage.getItem('accessToken');
  }

  getRefreshToken(): string | null {
    return localStorage.getItem('refreshToken');
  }

  getTokenExpiry(): number | null {
    const expiresAt = localStorage.getItem('expiresAt');
    return expiresAt ? parseInt(expiresAt, 10) : null;
  }

  isTokenExpired(): boolean {
    const expiresAt = this.getTokenExpiry();
    if (!expiresAt) return true;
    return Date.now() >= expiresAt;
  }

  clearTokens(): void {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('expiresAt');
    localStorage.removeItem('user');
  }

  setUser(user: User): void {
    localStorage.setItem('user', JSON.stringify(user));
  }

  getUser(): User | null {
    const userStr = localStorage.getItem('user');
    if (!userStr) return null;
    try {
      return JSON.parse(userStr);
    } catch {
      return null;
    }
  }

  clearUser(): void {
    localStorage.removeItem('user');
  }
}

export const authService = new AuthService();

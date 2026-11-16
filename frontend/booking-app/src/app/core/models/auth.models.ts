export interface TokenResponse {
  accessToken: string;
  expiresAt: string;
  userId: string;
  email: string;
  role: 'Admin' | 'Provider' | 'Customer';
  timeZoneId: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  timeZoneId: string;
  role: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface UserProfile {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  timeZoneId: string;
  role: string;
}

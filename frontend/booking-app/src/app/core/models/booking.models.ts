export type BookingStatus = 'Pending' | 'Confirmed' | 'Cancelled' | 'Completed' | 'NoShow';

export interface Slot {
  startUtc: string;
  endUtc: string;
  providerId: number;
  serviceId: number;
  providerTimeZoneId: string;
}

export interface AvailableSlotsResponse {
  date: string;
  providerId: number;
  serviceId: number;
  providerTimeZoneId: string;
  slots: Slot[];
}

export interface Booking {
  id: number;
  customerId: string;
  customerName: string;
  providerId: number;
  providerName: string;
  serviceId: number;
  serviceName: string;
  startUtc: string;
  endUtc: string;
  status: BookingStatus;
  notes: string | null;
  createdAt: string;
  providerTimeZoneId: string;
}

export interface CreateBookingRequest {
  providerId: number;
  serviceId: number;
  startUtc: string;
  notes: string | null;
  sessionToken: string | null;
}

export interface RescheduleRequest {
  newStartUtc: string;
  notes: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

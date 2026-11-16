export interface Provider {
  id: number;
  name: string;
  bio: string;
  timeZoneId: string;
  services: ServiceInfo[];
}

export interface ProviderDetail extends Provider {
  workingHours: WorkingHours[];
  policy: BookingPolicy | null;
}

export interface ServiceInfo {
  id: number;
  name: string;
  description: string;
  durationMinutes: number;
  bufferMinutes: number;
  price: number | null;
}

export interface WorkingHours {
  dayOfWeek: number;
  startTime: string;
  endTime: string;
}

export interface BookingPolicy {
  cancellationWindowHours: number;
  maxBookingsPerDay: number;
}

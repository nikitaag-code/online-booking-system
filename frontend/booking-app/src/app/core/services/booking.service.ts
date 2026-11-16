import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  Booking, CreateBookingRequest,
  RescheduleRequest, PagedResult
} from '../models/booking.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class BookingService {
  private readonly http = inject(HttpClient);

  getMyBookings(page = 1, pageSize = 20): Observable<PagedResult<Booking>> {
    return this.http.get<PagedResult<Booking>>(`${environment.apiUrl}/bookings`, {
      params: { page, pageSize }
    });
  }

  getById(id: number): Observable<Booking> {
    return this.http.get<Booking>(`${environment.apiUrl}/bookings/${id}`);
  }

  create(req: CreateBookingRequest): Observable<Booking> {
    return this.http.post<Booking>(`${environment.apiUrl}/bookings`, req);
  }

  reschedule(id: number, req: RescheduleRequest): Observable<Booking> {
    return this.http.put<Booking>(`${environment.apiUrl}/bookings/${id}/reschedule`, req);
  }

  cancel(id: number): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/bookings/${id}`);
  }

  // Provider dashboard
  getProviderBookings(from: string, to: string): Observable<Booking[]> {
    return this.http.get<Booking[]>(`${environment.apiUrl}/provider/dashboard/bookings`, {
      params: { from, to }
    });
  }

  getUtilization(from: string, to: string): Observable<any> {
    return this.http.get<any>(`${environment.apiUrl}/provider/dashboard/utilization`, {
      params: { from, to }
    });
  }
}

import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AvailableSlotsResponse } from '../models/booking.models';
import { environment } from '../../../environments/environment';

export interface HoldResponse { sessionToken: string; expiresAt: string; }

@Injectable({ providedIn: 'root' })
export class SlotService {
  private readonly http = inject(HttpClient);

  getSlots(providerId: number, serviceId: number, date: string): Observable<AvailableSlotsResponse> {
    return this.http.get<AvailableSlotsResponse>(`${environment.apiUrl}/slots`, {
      params: { providerId, serviceId, date }
    });
  }

  holdSlot(providerId: number, serviceId: number, startUtc: string): Observable<HoldResponse> {
    return this.http.post<HoldResponse>(`${environment.apiUrl}/slots/hold`, {
      providerId, serviceId, startUtc
    });
  }

  releaseHold(sessionToken: string): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/slots/hold/${sessionToken}`);
  }
}

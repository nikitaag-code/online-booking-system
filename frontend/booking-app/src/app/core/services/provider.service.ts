import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Provider, ProviderDetail } from '../models/provider.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ProviderService {
  private readonly http = inject(HttpClient);

  getAll(): Observable<Provider[]> {
    return this.http.get<Provider[]>(`${environment.apiUrl}/providers`);
  }

  getById(id: number): Observable<ProviderDetail> {
    return this.http.get<ProviderDetail>(`${environment.apiUrl}/providers/${id}`);
  }
}

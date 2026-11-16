import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { TokenResponse, LoginRequest, RegisterRequest, UserProfile } from '../models/auth.models';
import { environment } from '../../../environments/environment';

const TOKEN_KEY = 'bs_token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http   = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _user$ = new BehaviorSubject<TokenResponse | null>(this.loadToken());
  readonly user$ = this._user$.asObservable();

  get currentUser(): TokenResponse | null { return this._user$.value; }
  get isLoggedIn():  boolean              { return !!this._user$.value; }
  get role():        string               { return this._user$.value?.role ?? ''; }
  get timeZoneId():  string               { return this._user$.value?.timeZoneId ?? Intl.DateTimeFormat().resolvedOptions().timeZone; }

  register(req: RegisterRequest): Observable<TokenResponse> {
    return this.http.post<TokenResponse>(`${environment.apiUrl}/auth/register`, req).pipe(
      tap(token => this.persist(token))
    );
  }

  login(req: LoginRequest): Observable<TokenResponse> {
    return this.http.post<TokenResponse>(`${environment.apiUrl}/auth/login`, req).pipe(
      tap(token => this.persist(token))
    );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    this._user$.next(null);
    this.router.navigate(['/auth/login']);
  }

  getProfile(): Observable<UserProfile> {
    return this.http.get<UserProfile>(`${environment.apiUrl}/auth/me`);
  }

  private persist(token: TokenResponse): void {
    localStorage.setItem(TOKEN_KEY, JSON.stringify(token));
    this._user$.next(token);
  }

  private loadToken(): TokenResponse | null {
    try {
      const raw = localStorage.getItem(TOKEN_KEY);
      if (!raw) return null;
      const token: TokenResponse = JSON.parse(raw);
      // Discard expired tokens
      if (new Date(token.expiresAt) < new Date()) {
        localStorage.removeItem(TOKEN_KEY);
        return null;
      }
      return token;
    } catch {
      return null;
    }
  }
}

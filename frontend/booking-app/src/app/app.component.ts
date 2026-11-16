import { Component, inject } from '@angular/core';
import { RouterOutlet, RouterLink, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule }  from '@angular/material/button';
import { MatIconModule }    from '@angular/material/icon';
import { MatMenuModule }    from '@angular/material/menu';
import { AuthService }      from './core/services/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet, RouterLink, CommonModule,
    MatToolbarModule, MatButtonModule, MatIconModule, MatMenuModule
  ],
  template: `
    <mat-toolbar color="primary">
      <span class="app-title" routerLink="/">📅 BookingSystem</span>
      <span class="spacer"></span>

      <ng-container *ngIf="auth.isLoggedIn; else notLoggedIn">
        <span class="user-info">{{ auth.currentUser?.email }}</span>

        <button mat-icon-button [matMenuTriggerFor]="menu">
          <mat-icon>account_circle</mat-icon>
        </button>
        <mat-menu #menu="matMenu">
          <button mat-menu-item routerLink="/book"     *ngIf="auth.role === 'Customer' || auth.role === 'Admin'">
            <mat-icon>calendar_today</mat-icon> Book Appointment
          </button>
          <button mat-menu-item routerLink="/book/my-bookings" *ngIf="auth.role === 'Customer'">
            <mat-icon>list</mat-icon> My Bookings
          </button>
          <button mat-menu-item routerLink="/provider" *ngIf="auth.role === 'Provider'">
            <mat-icon>dashboard</mat-icon> Provider Dashboard
          </button>
          <mat-divider></mat-divider>
          <button mat-menu-item (click)="auth.logout()">
            <mat-icon>logout</mat-icon> Sign Out
          </button>
        </mat-menu>
      </ng-container>

      <ng-template #notLoggedIn>
        <button mat-button routerLink="/auth/login">Sign In</button>
        <button mat-raised-button color="accent" routerLink="/auth/register">Register</button>
      </ng-template>
    </mat-toolbar>

    <div class="page-container">
      <router-outlet />
    </div>
  `,
  styles: [`
    .app-title { font-size: 20px; cursor: pointer; }
    .user-info  { font-size: 13px; opacity: 0.85; margin-right: 8px; }
    mat-toolbar { position: sticky; top: 0; z-index: 100; }
  `]
})
export class AppComponent {
  readonly auth = inject(AuthService);
}

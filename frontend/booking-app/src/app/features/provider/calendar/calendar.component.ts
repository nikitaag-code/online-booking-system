import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router }       from '@angular/router';
import { MatCardModule }     from '@angular/material/card';
import { MatButtonModule }   from '@angular/material/button';
import { MatIconModule }     from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatChipsModule }    from '@angular/material/chips';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { BookingService }    from '../../../core/services/booking.service';
import { UtcToLocalPipe }    from '../../../core/pipes/utc-to-local.pipe';
import { Booking }           from '../../../core/models/booking.models';
import { AuthService }       from '../../../core/services/auth.service';

interface CalendarDay {
  date: Date;
  label: string;
  bookings: Booking[];
}

@Component({
  selector: 'app-provider-calendar',
  standalone: true,
  imports: [
    CommonModule, UtcToLocalPipe,
    MatCardModule, MatButtonModule, MatIconModule, MatProgressBarModule,
    MatChipsModule, MatSnackBarModule
  ],
  template: `
    <div class="calendar-header">
      <button mat-icon-button (click)="prevWeek()"><mat-icon>chevron_left</mat-icon></button>
      <h2>Week of {{ weekStart | date:'MMM d' }} – {{ weekEnd | date:'MMM d, yyyy' }}</h2>
      <button mat-icon-button (click)="nextWeek()"><mat-icon>chevron_right</mat-icon></button>
      <button mat-stroked-button (click)="goToday()">Today</button>
    </div>

    <mat-progress-bar *ngIf="loading" mode="indeterminate"></mat-progress-bar>

    <div class="week-grid" *ngIf="!loading">
      <mat-card *ngFor="let day of calendarDays" class="day-card">
        <mat-card-header>
          <mat-card-title class="day-title" [class.today]="isToday(day.date)">
            {{ day.label }}
          </mat-card-title>
          <mat-chip>{{ day.bookings.length }}</mat-chip>
        </mat-card-header>
        <mat-card-content>
          <div *ngIf="day.bookings.length === 0" class="empty-day">Free</div>
          <div *ngFor="let b of day.bookings" class="calendar-event"
               [style.background]="eventColor(b)">
            <strong>{{ b.startUtc | utcToLocal:'time' }}</strong>
            {{ b.customerName }} – {{ b.serviceName }}
          </div>
        </mat-card-content>
      </mat-card>
    </div>

    <div class="legend">
      <span class="legend-item"><span class="dot" style="background:#1976d2"></span> Confirmed</span>
      <span class="legend-item"><span class="dot" style="background:#2e7d32"></span> Completed</span>
    </div>
  `,
  styles: [`
    .calendar-header  { display: flex; align-items: center; gap: 8px; margin-bottom: 16px; }
    .calendar-header h2 { margin: 0; flex: 1; text-align: center; }
    .week-grid        { display: grid; grid-template-columns: repeat(7, 1fr); gap: 8px; }
    @media(max-width: 900px) { .week-grid { grid-template-columns: repeat(2, 1fr); } }
    .day-card         { min-height: 160px; }
    .day-title        { font-size: 14px; }
    .day-title.today  { color: #1976d2; font-weight: bold; }
    .empty-day        { color: #bbb; font-size: 13px; }
    .calendar-event   { border-radius: 4px; padding: 4px 6px; margin-bottom: 4px; color: white; font-size: 12px; }
    .legend           { display: flex; gap: 16px; margin-top: 12px; font-size: 13px; }
    .legend-item      { display: flex; align-items: center; gap: 4px; }
    .dot              { width: 10px; height: 10px; border-radius: 50%; display: inline-block; }
  `]
})
export class ProviderCalendarComponent implements OnInit {
  private readonly bookingSvc = inject(BookingService);
  private readonly auth       = inject(AuthService);

  loading      = false;
  weekOffset   = 0;
  calendarDays: CalendarDay[] = [];

  get weekStart(): Date { return this.getWeekStart(); }
  get weekEnd():   Date {
    const d = this.getWeekStart(); d.setDate(d.getDate() + 6); return d;
  }

  ngOnInit(): void { this.loadWeek(); }

  loadWeek(): void {
    this.loading = true;
    const start  = this.getWeekStart();
    const end    = new Date(start); end.setDate(end.getDate() + 6); end.setHours(23, 59, 59);

    this.bookingSvc.getProviderBookings(start.toISOString(), end.toISOString()).subscribe({
      next: bookings => {
        this.buildCalendar(start, bookings);
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }

  buildCalendar(weekStart: Date, bookings: Booking[]): void {
    this.calendarDays = [];
    for (let i = 0; i < 7; i++) {
      const d = new Date(weekStart);
      d.setDate(d.getDate() + i);

      const dayBookings = bookings.filter(b => {
        const start = new Date(b.startUtc);
        return start.toDateString() === d.toDateString();
      }).sort((a, b) => a.startUtc.localeCompare(b.startUtc));

      this.calendarDays.push({
        date:     d,
        label:    d.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' }),
        bookings: dayBookings
      });
    }
  }

  prevWeek(): void { this.weekOffset--; this.loadWeek(); }
  nextWeek(): void { this.weekOffset++; this.loadWeek(); }
  goToday():  void { this.weekOffset = 0; this.loadWeek(); }

  isToday(d: Date): boolean {
    return d.toDateString() === new Date().toDateString();
  }

  eventColor(b: Booking): string {
    switch (b.status) {
      case 'Completed': return '#2e7d32';
      case 'Cancelled': return '#c62828';
      default:          return '#1976d2';
    }
  }

  private getWeekStart(): Date {
    const now  = new Date();
    const day  = now.getDay();
    const diff = now.getDate() - day + (day === 0 ? -6 : 1) + this.weekOffset * 7;
    const d    = new Date(now);
    d.setDate(diff);
    d.setHours(0, 0, 0, 0);
    return d;
  }
}

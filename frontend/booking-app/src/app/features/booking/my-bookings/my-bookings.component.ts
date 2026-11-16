import { Component, inject, OnInit } from '@angular/core';
import { CommonModule }  from '@angular/common';
import { Router }        from '@angular/router';
import { MatCardModule }     from '@angular/material/card';
import { MatButtonModule }   from '@angular/material/button';
import { MatIconModule }     from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatChipsModule }    from '@angular/material/chips';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatDividerModule }  from '@angular/material/divider';
import { BookingService }    from '../../../core/services/booking.service';
import { UtcToLocalPipe }    from '../../../core/pipes/utc-to-local.pipe';
import { Booking }           from '../../../core/models/booking.models';

@Component({
  selector: 'app-my-bookings',
  standalone: true,
  imports: [
    CommonModule, UtcToLocalPipe,
    MatCardModule, MatButtonModule, MatIconModule, MatProgressBarModule,
    MatChipsModule, MatSnackBarModule, MatDialogModule,
    MatPaginatorModule, MatDividerModule
  ],
  template: `
    <h2>My Bookings</h2>

    <mat-progress-bar *ngIf="loading" mode="indeterminate"></mat-progress-bar>

    <div *ngIf="!loading">
      <mat-card *ngFor="let b of bookings" class="booking-card">
        <mat-card-header>
          <mat-icon mat-card-avatar>event</mat-icon>
          <mat-card-title>{{ b.serviceName }} with {{ b.providerName }}</mat-card-title>
          <mat-card-subtitle>
            <span>{{ b.startUtc | utcToLocal:'datetime' }}</span>
            <span class="tz-secondary"> · Provider's zone: {{ b.startUtc | utcToLocal:'datetime':b.providerTimeZoneId }}</span>
          </mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <mat-chip [class]="'status-' + b.status.toLowerCase()">{{ b.status }}</mat-chip>
          <span *ngIf="b.notes" class="notes">{{ b.notes }}</span>
        </mat-card-content>

        <mat-card-actions *ngIf="b.status === 'Confirmed'">
          <button mat-stroked-button color="primary"
                  (click)="reschedule(b)">
            <mat-icon>edit_calendar</mat-icon> Reschedule
          </button>
          <button mat-stroked-button color="warn"
                  (click)="cancel(b)">
            <mat-icon>cancel</mat-icon> Cancel
          </button>
        </mat-card-actions>
      </mat-card>

      <p *ngIf="bookings.length === 0 && !loading" class="empty-msg">
        No bookings yet. <a routerLink="/book">Book your first appointment →</a>
      </p>

      <mat-paginator
        [length]="totalCount"
        [pageSize]="pageSize"
        [pageSizeOptions]="[5, 10, 20]"
        (page)="onPage($event)">
      </mat-paginator>
    </div>
  `,
  styles: [`
    .booking-card   { margin-bottom: 12px; }
    .tz-secondary   { font-size: 12px; color: #888; }
    .notes          { margin-left: 8px; font-size: 13px; color: #555; }
    .empty-msg      { text-align: center; color: #999; margin-top: 32px; }
    mat-chip        { margin-right: 8px; }
  `]
})
export class MyBookingsComponent implements OnInit {
  private readonly bookingSvc = inject(BookingService);
  private readonly snack      = inject(MatSnackBar);
  private readonly router     = inject(Router);

  bookings:   Booking[] = [];
  loading     = true;
  totalCount  = 0;
  pageSize    = 10;
  currentPage = 1;

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    this.bookingSvc.getMyBookings(this.currentPage, this.pageSize).subscribe({
      next: result => {
        this.bookings   = result.items;
        this.totalCount = result.totalCount;
        this.loading    = false;
      },
      error: () => { this.loading = false; }
    });
  }

  onPage(e: PageEvent): void {
    this.currentPage = e.pageIndex + 1;
    this.pageSize    = e.pageSize;
    this.load();
  }

  cancel(booking: Booking): void {
    if (!confirm(`Cancel your appointment with ${booking.providerName} on\n${new Date(booking.startUtc).toLocaleString()}?`))
      return;

    this.bookingSvc.cancel(booking.id).subscribe({
      next: () => {
        this.snack.open('Booking cancelled.', '', { duration: 3000 });
        this.load();
      },
      error: err => {
        const msg = err.error?.title ?? err.error ?? 'Cancellation failed.';
        this.snack.open(msg, 'Dismiss', { duration: 5000, panelClass: 'error-snack' });
      }
    });
  }

  reschedule(booking: Booking): void {
    // Navigate back to the slot grid for the same provider+service
    this.router.navigate(['/book', booking.providerId, 'slots', booking.serviceId], {
      queryParams: { reschedule: booking.id }
    });
  }
}

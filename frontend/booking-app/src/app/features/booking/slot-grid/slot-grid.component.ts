import { Component, inject, OnInit, OnDestroy, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { MatCardModule }       from '@angular/material/card';
import { MatButtonModule }     from '@angular/material/button';
import { MatIconModule }       from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatFormFieldModule }  from '@angular/material/form-field';
import { MatInputModule }      from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatDividerModule }    from '@angular/material/divider';
import { Subject, interval, startWith, takeUntil } from 'rxjs';
import { SlotService }    from '../../../core/services/slot.service';
import { BookingService } from '../../../core/services/booking.service';
import { ProviderService } from '../../../core/services/provider.service';
import { AuthService }    from '../../../core/services/auth.service';
import { UtcToLocalPipe } from '../../../core/pipes/utc-to-local.pipe';
import { Slot } from '../../../core/models/booking.models';
import { ProviderDetail } from '../../../core/models/provider.models';

@Component({
  selector: 'app-slot-grid',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, UtcToLocalPipe,
    MatCardModule, MatButtonModule, MatIconModule, MatProgressBarModule,
    MatSnackBarModule, MatFormFieldModule, MatInputModule,
    MatDatepickerModule, MatNativeDateModule, MatDividerModule
  ],
  template: `
    <div class="slot-grid-page">
      <div class="header-row">
        <button mat-icon-button (click)="goBack()"><mat-icon>arrow_back</mat-icon></button>
        <div>
          <h2>{{ provider?.name }}</h2>
          <p class="subtitle" *ngIf="serviceName">
            {{ serviceName }} · Provider timezone: {{ provider?.timeZoneId }}
          </p>
        </div>
      </div>

      <!-- Date picker -->
      <mat-card class="date-card">
        <mat-card-content>
          <mat-form-field appearance="outline">
            <mat-label>Select date</mat-label>
            <input matInput [matDatepicker]="picker"
                   [min]="minDate"
                   [(ngModel)]="selectedDate"
                   (ngModelChange)="onDateChange($event)">
            <mat-datepicker-toggle matIconSuffix [for]="picker"></mat-datepicker-toggle>
            <mat-datepicker #picker></mat-datepicker>
          </mat-form-field>

          <span class="refresh-hint">
            <mat-icon class="spin" *ngIf="loadingSlots">refresh</mat-icon>
            Grid refreshes every 30 seconds
          </span>
        </mat-card-content>
      </mat-card>

      <!-- Slot grid -->
      <mat-progress-bar *ngIf="loadingSlots" mode="indeterminate"></mat-progress-bar>

      <mat-card *ngIf="!loadingSlots">
        <mat-card-header>
          <mat-card-title>
            Available slots
            <span class="provider-tz">
              (shown in your zone: {{ auth.timeZoneId }})
            </span>
          </mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <div *ngIf="slots.length > 0; else noSlots">
            <div class="slot-grid">
              <button *ngFor="let slot of slots"
                      mat-stroked-button
                      class="slot-btn"
                      [class.selected]="selectedSlot?.startUtc === slot.startUtc"
                      (click)="selectSlot(slot)">
                {{ slot.startUtc | utcToLocal:'time' }}
                <br>
                <small>{{ slot.endUtc | utcToLocal:'time' }}</small>
              </button>
            </div>

            <!-- Also show in provider's timezone -->
            <p class="provider-tz-note" *ngIf="selectedSlot">
              In provider's timezone ({{ provider?.timeZoneId }}):
              <strong>{{ selectedSlot.startUtc | utcToLocal:'datetime':provider?.timeZoneId }}</strong>
            </p>
          </div>

          <ng-template #noSlots>
            <p class="empty-msg">
              <mat-icon>event_busy</mat-icon>
              No slots available on this date. Try another date.
            </p>
          </ng-template>
        </mat-card-content>
      </mat-card>

      <!-- Booking form (shown when slot selected) -->
      <mat-card *ngIf="selectedSlot" class="booking-form-card">
        <mat-card-header>
          <mat-card-title>Confirm Booking</mat-card-title>
          <mat-card-subtitle>
            {{ selectedSlot.startUtc | utcToLocal:'datetime' }}
          </mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Notes (optional)</mat-label>
            <textarea matInput rows="3" [(ngModel)]="notes"
                      placeholder="Any notes for the provider…"></textarea>
          </mat-form-field>

          <div class="confirm-actions">
            <button mat-stroked-button (click)="clearSelection()">Cancel</button>
            <button mat-raised-button color="primary"
                    [disabled]="booking"
                    (click)="confirmBooking()">
              <mat-icon>check_circle</mat-icon>
              {{ booking ? 'Booking…' : 'Confirm Booking' }}
            </button>
          </div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .slot-grid-page  { max-width: 900px; }
    .header-row      { display: flex; align-items: center; gap: 8px; margin-bottom: 16px; }
    .header-row h2   { margin: 0; }
    .subtitle        { margin: 2px 0 0; color: #666; font-size: 14px; }
    .date-card       { margin-bottom: 16px; }
    .date-card mat-card-content { display: flex; align-items: center; gap: 24px; flex-wrap: wrap; padding-top: 16px; }
    .refresh-hint    { font-size: 12px; color: #999; display: flex; align-items: center; gap: 4px; }
    .slot-grid       { display: grid; grid-template-columns: repeat(auto-fill, minmax(90px, 1fr)); gap: 8px; }
    .slot-btn        { height: auto; min-height: 56px; line-height: 1.3; font-size: 13px; }
    .slot-btn.selected { background: #1976d2; color: white; }
    .provider-tz     { font-size: 13px; font-weight: normal; color: #666; margin-left: 8px; }
    .provider-tz-note { font-size: 13px; color: #555; margin-top: 12px; }
    .empty-msg       { display: flex; align-items: center; gap: 8px; color: #999; }
    .booking-form-card { margin-top: 16px; }
    .full-width      { width: 100%; }
    .confirm-actions { display: flex; gap: 12px; justify-content: flex-end; }
    .spin            { animation: spin 1s linear infinite; }
    @keyframes spin  { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
  `]
})
export class SlotGridComponent implements OnInit, OnDestroy {
  private readonly route       = inject(ActivatedRoute);
  private readonly router      = inject(Router);
  private readonly slotSvc     = inject(SlotService);
  private readonly bookingSvc  = inject(BookingService);
  private readonly providerSvc = inject(ProviderService);
  readonly         auth        = inject(AuthService);
  private readonly snack       = inject(MatSnackBar);

  private readonly destroy$ = new Subject<void>();
  private readonly refresh$ = new Subject<void>();

  providerId!: number;
  serviceId!:  number;
  provider:    ProviderDetail | null = null;
  serviceName  = '';

  slots:       Slot[]      = [];
  selectedSlot: Slot | null = null;
  notes        = '';
  loadingSlots = false;
  booking      = false;
  sessionToken: string | null = null;

  minDate      = new Date();
  selectedDate = new Date();

  ngOnInit(): void {
    this.providerId = Number(this.route.snapshot.paramMap.get('providerId'));
    this.serviceId  = Number(this.route.snapshot.paramMap.get('serviceId'));

    this.providerSvc.getById(this.providerId).subscribe(p => {
      this.provider   = p;
      this.serviceName = p.services.find(s => s.id === this.serviceId)?.name ?? '';
    });

    // Auto-refresh every 30 seconds
    interval(30_000).pipe(
      startWith(0),
      takeUntil(this.destroy$)
    ).subscribe(() => this.loadSlotsAndUpdate());
  }

  onDateChange(_: Date): void {
    this.clearSelection();
    this.loadSlotsAndUpdate();
  }

  private loadSlotsAndUpdate(): void {
    this.loadingSlots = true;
    const dateStr = this.formatDate(this.selectedDate);

    this.slotSvc.getSlots(this.providerId, this.serviceId, dateStr)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: resp => {
          this.slots = resp?.slots ?? [];
          this.loadingSlots = false;

          // If the selected slot is no longer in the list, it was taken
          if (this.selectedSlot) {
            const still = this.slots.find(s => s.startUtc === this.selectedSlot!.startUtc);
            if (!still) {
              this.snack.open(
                'Your selected slot was just taken by someone else. Please choose another.',
                'OK', { duration: 6000, panelClass: 'warn-snack' }
              );
              this.clearSelection();
            }
          }
        },
        error: () => { this.loadingSlots = false; }
      });
  }

  selectSlot(slot: Slot): void {
    this.selectedSlot = slot;
    this.notes        = '';

    // Place a hold so the slot is hidden from others briefly
    this.slotSvc.holdSlot(this.providerId, this.serviceId, slot.startUtc).subscribe({
      next: res => { this.sessionToken = res.sessionToken; },
      error: () => {
        // Hold failed — slot was likely taken
        this.snack.open('Slot just taken. Refreshing…', '', { duration: 3000, panelClass: 'warn-snack' });
        this.selectedSlot = null;
        this.loadSlotsAndUpdate();
      }
    });
  }

  clearSelection(): void {
    if (this.sessionToken) {
      this.slotSvc.releaseHold(this.sessionToken).subscribe();
      this.sessionToken = null;
    }
    this.selectedSlot = null;
    this.notes = '';
  }

  confirmBooking(): void {
    if (!this.selectedSlot) return;
    this.booking = true;

    this.bookingSvc.create({
      providerId:   this.providerId,
      serviceId:    this.serviceId,
      startUtc:     this.selectedSlot.startUtc,
      notes:        this.notes || null,
      sessionToken: this.sessionToken
    }).subscribe({
      next: booking => {
        this.snack.open('Booking confirmed!', '', { duration: 3000, panelClass: 'success-snack' });
        this.router.navigate(['/book/my-bookings']);
      },
      error: err => {
        this.booking = false;
        if (err.status === 409) {
          this.snack.open(
            'That slot was just taken. Refreshing available times…',
            'OK', { duration: 5000, panelClass: 'warn-snack' }
          );
          this.clearSelection();
          this.loadSlotsAndUpdate();
        } else {
          const msg = err.error?.title ?? err.error ?? 'Booking failed. Please try again.';
          this.snack.open(msg, 'Dismiss', { duration: 5000, panelClass: 'error-snack' });
        }
      }
    });
  }

  goBack(): void { this.router.navigate(['/book']); }

  private formatDate(d: Date): string {
    return d.toISOString().split('T')[0];
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    // Release any active hold
    if (this.sessionToken) {
      this.slotSvc.releaseHold(this.sessionToken).subscribe();
    }
  }
}

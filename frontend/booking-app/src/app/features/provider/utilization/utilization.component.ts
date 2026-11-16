import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule }     from '@angular/material/card';
import { MatButtonModule }   from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule }    from '@angular/material/table';
import { MatIconModule }     from '@angular/material/icon';
import { BookingService }    from '../../../core/services/booking.service';

@Component({
  selector: 'app-utilization',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule, MatButtonModule, MatProgressBarModule,
    MatTableModule, MatIconModule
  ],
  template: `
    <h2>Utilization Report</h2>

    <div class="controls">
      <mat-button-toggle-group (click)="$event.stopPropagation()">
      </mat-button-toggle-group>

      <button mat-stroked-button (click)="loadRange(7)">Last 7 days</button>
      <button mat-stroked-button (click)="loadRange(30)">Last 30 days</button>
      <button mat-stroked-button (click)="loadRange(90)">Last 90 days</button>
    </div>

    <mat-progress-bar *ngIf="loading" mode="indeterminate"></mat-progress-bar>

    <div class="stats-row" *ngIf="stats">
      <mat-card class="stat-card">
        <mat-card-content>
          <div class="stat-num">{{ stats.totalBookings }}</div>
          <div class="stat-label">Total Bookings</div>
        </mat-card-content>
      </mat-card>
      <mat-card class="stat-card">
        <mat-card-content>
          <div class="stat-num">{{ stats.maxPerDay }}</div>
          <div class="stat-label">Max / Day</div>
        </mat-card-content>
      </mat-card>
      <mat-card class="stat-card">
        <mat-card-content>
          <div class="stat-num">{{ stats.avgPerDay }}</div>
          <div class="stat-label">Avg / Day</div>
        </mat-card-content>
      </mat-card>
    </div>

    <!-- Bar chart: bookings per day -->
    <mat-card *ngIf="stats && stats.byDay.length > 0" class="chart-card">
      <mat-card-header>
        <mat-card-title>Bookings per Day</mat-card-title>
      </mat-card-header>
      <mat-card-content>
        <div class="bar-chart">
          <div *ngFor="let d of stats.byDay" class="bar-wrap">
            <div class="bar" [style.height.px]="barHeight(d.bookingCount)" title="{{ d.bookingCount }} bookings">
              <span class="bar-label">{{ d.bookingCount }}</span>
            </div>
            <div class="bar-date">{{ d.date | slice:5 }}</div>
          </div>
        </div>
      </mat-card-content>
    </mat-card>

    <!-- Detail table -->
    <mat-card *ngIf="stats && stats.byDay.length > 0">
      <mat-card-header><mat-card-title>Daily Breakdown</mat-card-title></mat-card-header>
      <mat-card-content>
        <table mat-table [dataSource]="stats.byDay" class="full-width">
          <ng-container matColumnDef="date">
            <th mat-header-cell *matHeaderCellDef>Date</th>
            <td mat-cell *matCellDef="let r">{{ r.date }}</td>
          </ng-container>
          <ng-container matColumnDef="bookingCount">
            <th mat-header-cell *matHeaderCellDef>Bookings</th>
            <td mat-cell *matCellDef="let r">{{ r.bookingCount }}</td>
          </ng-container>
          <ng-container matColumnDef="totalMinutes">
            <th mat-header-cell *matHeaderCellDef>Total Minutes</th>
            <td mat-cell *matCellDef="let r">{{ r.totalMinutes }}</td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
        </table>
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    .controls    { display: flex; gap: 8px; margin-bottom: 16px; }
    .stats-row   { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; margin-bottom: 16px; }
    .stat-card   { text-align: center; }
    .stat-num    { font-size: 36px; font-weight: 500; color: #1976d2; }
    .stat-label  { font-size: 13px; color: #666; }
    .chart-card  { margin-bottom: 16px; }
    .bar-chart   { display: flex; align-items: flex-end; gap: 4px; height: 160px; padding: 8px 0; overflow-x: auto; }
    .bar-wrap    { display: flex; flex-direction: column; align-items: center; min-width: 32px; }
    .bar         { background: #1976d2; width: 24px; border-radius: 3px 3px 0 0; display: flex; align-items: flex-start; justify-content: center; }
    .bar-label   { color: white; font-size: 10px; padding-top: 2px; }
    .bar-date    { font-size: 10px; color: #666; margin-top: 2px; }
    .full-width  { width: 100%; }
  `]
})
export class UtilizationComponent implements OnInit {
  private readonly bookingSvc = inject(BookingService);

  loading = false;
  stats:   any = null;
  columns = ['date', 'bookingCount', 'totalMinutes'];

  ngOnInit(): void { this.loadRange(30); }

  loadRange(days: number): void {
    this.loading = true;
    const to   = new Date();
    const from = new Date(); from.setDate(from.getDate() - days);

    this.bookingSvc.getUtilization(from.toISOString(), to.toISOString()).subscribe({
      next: data => { this.stats = data; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  barHeight(count: number): number {
    if (!this.stats?.maxPerDay || this.stats.maxPerDay === 0) return 0;
    return Math.max(4, (count / this.stats.maxPerDay) * 120);
  }
}

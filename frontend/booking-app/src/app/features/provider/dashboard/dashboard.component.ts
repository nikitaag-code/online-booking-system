import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { MatCardModule }   from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule }   from '@angular/material/icon';
import { MatListModule }   from '@angular/material/list';
import { MatDividerModule } from '@angular/material/divider';

@Component({
  selector: 'app-provider-dashboard',
  standalone: true,
  imports: [RouterLink, MatCardModule, MatButtonModule, MatIconModule, MatListModule, MatDividerModule],
  template: `
    <h2>Provider Dashboard</h2>

    <div class="dashboard-grid">
      <mat-card routerLink="calendar" class="nav-card">
        <mat-card-content>
          <mat-icon class="big-icon">calendar_month</mat-icon>
          <h3>My Calendar</h3>
          <p>View all upcoming appointments in a weekly calendar view.</p>
          <button mat-raised-button color="primary" routerLink="calendar">Open Calendar</button>
        </mat-card-content>
      </mat-card>

      <mat-card routerLink="utilization" class="nav-card">
        <mat-card-content>
          <mat-icon class="big-icon">bar_chart</mat-icon>
          <h3>Utilization</h3>
          <p>Bookings per day, average load, and busy periods.</p>
          <button mat-raised-button color="accent" routerLink="utilization">View Stats</button>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .dashboard-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 16px; }
    .nav-card       { cursor: pointer; transition: box-shadow 0.2s; }
    .nav-card:hover { box-shadow: 0 4px 16px rgba(0,0,0,0.15); }
    .big-icon       { font-size: 48px; height: 48px; width: 48px; color: #1976d2; }
    mat-card-content { text-align: center; padding-top: 24px; }
    h3              { margin: 12px 0 4px; }
    p               { font-size: 13px; color: #666; }
  `]
})
export class ProviderDashboardComponent {}

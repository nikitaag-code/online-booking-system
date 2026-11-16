import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatCardModule }     from '@angular/material/card';
import { MatButtonModule }   from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule }   from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { MatIconModule }     from '@angular/material/icon';
import { MatDividerModule }  from '@angular/material/divider';
import { ProviderService }   from '../../../core/services/provider.service';
import { Provider, ServiceInfo } from '../../../core/models/provider.models';

@Component({
  selector: 'app-provider-list',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatCardModule, MatButtonModule, MatProgressBarModule,
    MatSelectModule, MatFormFieldModule, MatIconModule, MatDividerModule
  ],
  template: `
    <h2>Book an Appointment</h2>

    <mat-progress-bar *ngIf="loading" mode="indeterminate"></mat-progress-bar>

    <div *ngIf="!loading">
      <div *ngFor="let provider of providers" class="provider-card-wrap">
        <mat-card>
          <mat-card-header>
            <mat-icon mat-card-avatar>person</mat-icon>
            <mat-card-title>{{ provider.name }}</mat-card-title>
            <mat-card-subtitle>{{ provider.bio }}</mat-card-subtitle>
          </mat-card-header>

          <mat-card-content>
            <p class="tz-label">
              <mat-icon class="inline-icon">schedule</mat-icon>
              Timezone: <strong>{{ provider.timeZoneId }}</strong>
            </p>

            <mat-divider></mat-divider>
            <p class="services-label">Available services:</p>

            <div class="service-select-row">
              <mat-form-field appearance="outline">
                <mat-label>Select a service</mat-label>
                <mat-select [(value)]="selectedService[provider.id]">
                  <mat-option *ngFor="let svc of provider.services" [value]="svc">
                    {{ svc.name }} – {{ svc.durationMinutes }} min
                    <span *ngIf="svc.price"> (${{ svc.price }})</span>
                  </mat-option>
                </mat-select>
              </mat-form-field>

              <button mat-raised-button color="primary"
                      [disabled]="!selectedService[provider.id]"
                      (click)="goToSlots(provider)">
                <mat-icon>event_available</mat-icon>
                View Slots
              </button>
            </div>
          </mat-card-content>
        </mat-card>
      </div>

      <p *ngIf="providers.length === 0 && !loading" class="empty-msg">
        No providers available at the moment.
      </p>
    </div>
  `,
  styles: [`
    .provider-card-wrap { margin-bottom: 16px; }
    .tz-label           { display: flex; align-items: center; gap: 4px; color: #666; }
    .inline-icon        { font-size: 16px; height: 16px; width: 16px; vertical-align: middle; }
    .services-label     { font-weight: 500; margin-top: 8px; }
    .service-select-row { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; }
    .service-select-row mat-form-field { flex: 1; min-width: 200px; }
    .empty-msg { color: #999; text-align: center; margin-top: 32px; }
  `]
})
export class ProviderListComponent implements OnInit {
  private readonly providerSvc = inject(ProviderService);
  private readonly router      = inject(Router);

  providers: Provider[] = [];
  loading = true;
  selectedService: Record<number, ServiceInfo | null> = {};

  ngOnInit(): void {
    this.providerSvc.getAll().subscribe({
      next: list => {
        this.providers = list;
        this.loading   = false;
      },
      error: () => { this.loading = false; }
    });
  }

  goToSlots(provider: Provider): void {
    const svc = this.selectedService[provider.id];
    if (!svc) return;
    this.router.navigate(['/book', provider.id, 'slots', svc.id]);
  }
}

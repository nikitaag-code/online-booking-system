import { Component, inject } from '@angular/core';
import { CommonModule }     from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink }  from '@angular/router';
import { MatCardModule }       from '@angular/material/card';
import { MatFormFieldModule }  from '@angular/material/form-field';
import { MatInputModule }      from '@angular/material/input';
import { MatSelectModule }     from '@angular/material/select';
import { MatButtonModule }     from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterLink,
    MatCardModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatButtonModule, MatSnackBarModule
  ],
  template: `
    <div class="auth-wrapper">
      <mat-card class="auth-card">
        <mat-card-header>
          <mat-card-title>Create Account</mat-card-title>
        </mat-card-header>

        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="onSubmit()">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>First Name</mat-label>
              <input matInput formControlName="firstName">
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Last Name</mat-label>
              <input matInput formControlName="lastName">
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Email</mat-label>
              <input matInput type="email" formControlName="email">
              <mat-error *ngIf="form.get('email')?.invalid">Valid email required</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Password</mat-label>
              <input matInput type="password" formControlName="password">
              <mat-error *ngIf="form.get('password')?.invalid">Min 8 characters required</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Role</mat-label>
              <mat-select formControlName="role">
                <mat-option value="Customer">Customer (book appointments)</mat-option>
                <mat-option value="Provider">Provider (offer services)</mat-option>
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Your Time Zone</mat-label>
              <input matInput formControlName="timeZoneId" placeholder="e.g. America/New_York">
              <mat-hint>Detected: {{ detectedTz }}</mat-hint>
            </mat-form-field>

            <button mat-raised-button color="primary" type="submit"
                    [disabled]="form.invalid || loading" class="full-width submit-btn">
              {{ loading ? 'Creating account…' : 'Register' }}
            </button>
          </form>

          <p class="auth-link">Already have an account? <a routerLink="/auth/login">Sign In</a></p>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .auth-wrapper { display: flex; justify-content: center; padding-top: 32px; }
    .auth-card    { width: 100%; max-width: 440px; }
    .full-width   { width: 100%; }
    .submit-btn   { margin-top: 8px; height: 44px; }
    .auth-link    { text-align: center; margin-top: 16px; }
    mat-form-field { display: block; margin-bottom: 4px; }
  `]
})
export class RegisterComponent {
  private readonly auth   = inject(AuthService);
  private readonly router = inject(Router);
  private readonly snack  = inject(MatSnackBar);
  private readonly fb     = inject(FormBuilder);

  loading = false;
  detectedTz = Intl.DateTimeFormat().resolvedOptions().timeZone;

  form = this.fb.group({
    firstName:  ['', Validators.required],
    lastName:   ['', Validators.required],
    email:      ['', [Validators.required, Validators.email]],
    password:   ['', [Validators.required, Validators.minLength(8)]],
    role:       ['Customer', Validators.required],
    timeZoneId: [this.detectedTz, Validators.required]
  });

  onSubmit(): void {
    if (this.form.invalid) return;
    this.loading = true;

    this.auth.register(this.form.value as any).subscribe({
      next: user => {
        this.snack.open('Account created!', '', { duration: 2000, panelClass: 'success-snack' });
        this.router.navigate([user.role === 'Provider' ? '/provider' : '/book']);
      },
      error: err => {
        this.loading = false;
        const errors = err.error?.map?.((e: any) => e.description)?.join(', ') ?? 'Registration failed.';
        this.snack.open(errors, 'Dismiss', { duration: 5000, panelClass: 'error-snack' });
      }
    });
  }
}

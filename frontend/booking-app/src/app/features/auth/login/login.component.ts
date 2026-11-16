import { Component, inject } from '@angular/core';
import { CommonModule }     from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink }  from '@angular/router';
import { MatCardModule }       from '@angular/material/card';
import { MatFormFieldModule }  from '@angular/material/form-field';
import { MatInputModule }      from '@angular/material/input';
import { MatButtonModule }     from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterLink,
    MatCardModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatSnackBarModule
  ],
  template: `
    <div class="auth-wrapper">
      <mat-card class="auth-card">
        <mat-card-header>
          <mat-card-title>Sign In</mat-card-title>
          <mat-card-subtitle>Welcome back to BookingSystem</mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="onSubmit()">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Email</mat-label>
              <input matInput type="email" formControlName="email" autocomplete="email">
              <mat-error *ngIf="form.get('email')?.invalid">Valid email required</mat-error>
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Password</mat-label>
              <input matInput type="password" formControlName="password" autocomplete="current-password">
              <mat-error *ngIf="form.get('password')?.invalid">Password required</mat-error>
            </mat-form-field>

            <button mat-raised-button color="primary" type="submit"
                    [disabled]="form.invalid || loading" class="full-width submit-btn">
              {{ loading ? 'Signing in…' : 'Sign In' }}
            </button>
          </form>

          <p class="auth-link">
            Don't have an account? <a routerLink="/auth/register">Register</a>
          </p>

          <div class="demo-creds">
            <strong>Demo credentials:</strong><br>
            Customer: alice@example.local / Customer@123!<br>
            Provider: drsmith@booking.local / Provider@123!
          </div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .auth-wrapper { display: flex; justify-content: center; padding-top: 48px; }
    .auth-card    { width: 100%; max-width: 420px; }
    .full-width   { width: 100%; }
    .submit-btn   { margin-top: 8px; height: 44px; }
    .auth-link    { text-align: center; margin-top: 16px; }
    .demo-creds   { font-size: 12px; background: #f5f5f5; padding: 8px; border-radius: 4px; margin-top: 12px; }
    mat-form-field { display: block; margin-bottom: 8px; }
  `]
})
export class LoginComponent {
  private readonly auth    = inject(AuthService);
  private readonly router  = inject(Router);
  private readonly snack   = inject(MatSnackBar);
  private readonly fb      = inject(FormBuilder);

  loading = false;

  form = this.fb.group({
    email:    ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  onSubmit(): void {
    if (this.form.invalid) return;
    this.loading = true;

    const { email, password } = this.form.value;
    this.auth.login({ email: email!, password: password! }).subscribe({
      next: user => {
        this.snack.open(`Welcome back!`, '', { duration: 2000, panelClass: 'success-snack' });
        this.router.navigate([user.role === 'Provider' ? '/provider' : '/book']);
      },
      error: err => {
        this.loading = false;
        const msg = err.status === 401 ? 'Invalid email or password.' : 'Login failed. Try again.';
        this.snack.open(msg, 'Dismiss', { duration: 4000, panelClass: 'error-snack' });
      }
    });
  }
}

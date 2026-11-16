import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule }   from '@angular/material/icon';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [RouterLink, MatButtonModule, MatIconModule],
  template: `
    <div style="text-align:center;padding:64px 16px">
      <mat-icon style="font-size:64px;height:64px;width:64px;color:#c62828">block</mat-icon>
      <h2>Access Denied</h2>
      <p>You don't have permission to view this page.</p>
      <button mat-raised-button color="primary" routerLink="/">Go Home</button>
    </div>
  `
})
export class UnauthorizedComponent {}

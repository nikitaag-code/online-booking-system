import { Routes } from '@angular/router';
import { authGuard }  from './core/guards/auth.guard';
import { roleGuard }  from './core/guards/role.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'book', pathMatch: 'full' },

  // Auth
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then(m => m.AUTH_ROUTES)
  },

  // Customer booking flow (protected)
  {
    path: 'book',
    canActivate: [authGuard, roleGuard('Customer', 'Admin')],
    loadChildren: () => import('./features/booking/booking.routes').then(m => m.BOOKING_ROUTES)
  },

  // Provider dashboard (protected)
  {
    path: 'provider',
    canActivate: [authGuard, roleGuard('Provider', 'Admin')],
    loadChildren: () => import('./features/provider/provider.routes').then(m => m.PROVIDER_ROUTES)
  },

  // Fallback
  { path: 'unauthorized', loadComponent: () => import('./features/shared/unauthorized.component').then(m => m.UnauthorizedComponent) },
  { path: '**',           redirectTo: 'book' }
];

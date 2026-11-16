import { Routes } from '@angular/router';

export const PROVIDER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./dashboard/dashboard.component').then(m => m.ProviderDashboardComponent)
  },
  {
    path: 'calendar',
    loadComponent: () => import('./calendar/calendar.component').then(m => m.ProviderCalendarComponent)
  },
  {
    path: 'utilization',
    loadComponent: () => import('./utilization/utilization.component').then(m => m.UtilizationComponent)
  }
];

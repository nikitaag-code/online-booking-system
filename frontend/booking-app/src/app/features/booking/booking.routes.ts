import { Routes } from '@angular/router';

export const BOOKING_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./provider-list/provider-list.component').then(m => m.ProviderListComponent)
  },
  {
    path: ':providerId/slots/:serviceId',
    loadComponent: () => import('./slot-grid/slot-grid.component').then(m => m.SlotGridComponent)
  },
  {
    path: 'my-bookings',
    loadComponent: () => import('./my-bookings/my-bookings.component').then(m => m.MyBookingsComponent)
  }
];

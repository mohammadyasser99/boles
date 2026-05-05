import { Routes } from '@angular/router';
import { authGuard, loginGuard, superAdminGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [loginGuard],
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: '',
    loadComponent: () => import('./shared/layout/layout.component').then(m => m.LayoutComponent),
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'cars',
        loadComponent: () => import('./features/cars/cars.component').then(m => m.CarsComponent)
      },
      {
        path: 'users',
        loadComponent: () => import('./features/users/users.component').then(m => m.UsersComponent)
      },
      {
        path: 'fines',
        loadComponent: () => import('./features/fines/fines.component').then(m => m.FinesComponent)
      },
      {
        path: 'entrance-fees',
        loadComponent: () => import('./features/entrance-fees/entrance-fees.component').then(m => m.EntranceFeesComponent)
      },
      {
        path: 'admins',
        canActivate: [superAdminGuard],
        loadComponent: () => import('./features/admins/admins.component').then(m => m.AdminsComponent)
      },
            {
        path: 'create-user-car',
        canActivate: [superAdminGuard],
        loadComponent: () => import('./features/create-user-and-car/create-user-and-car.component').then(m => m.CreateUserAndCarComponent)
      },
      {
        path: 'create-user-car/:userId',
        canActivate: [superAdminGuard],
        loadComponent: () => import('./features/create-user-and-car/create-user-and-car.component').then(m => m.CreateUserAndCarComponent)
      },
      {
        path: 'car-payment-report/:carPlate',
        loadComponent: () => import('./features/car-payment-report/car-payment-report.component').then(m => m.CarPaymentReportComponent)
      },
      {
        path: 'monthly-rental-payments',
        canActivate: [superAdminGuard],
        loadComponent: () => import('./features/monthly-rental-payment/monthly-rental-payment.component').then(m => m.MonthlyRentalPaymentComponent)
      },
    ]
  },
  { path: '**', redirectTo: 'dashboard' }
];

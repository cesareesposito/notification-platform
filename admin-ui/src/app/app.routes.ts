import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'api-keys', pathMatch: 'full' },
  { path: 'login', loadComponent: () => import('./pages/login/login.component').then(m => m.LoginComponent) },
  {
    path: '',
    loadComponent: () => import('./pages/shell/shell.component').then(m => m.ShellComponent),
    canActivate: [authGuard],
    children: [
      { path: 'api-keys', loadComponent: () => import('./pages/api-keys/api-keys.component').then(m => m.ApiKeysComponent) },
    ]
  },
  { path: '**', redirectTo: 'api-keys' }
];

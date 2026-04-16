import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { AuthStateService } from '../../services/auth-state.service';
import { AuthService } from '../../services/auth.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive,
    MatToolbarModule, MatButtonModule, MatIconModule,
    MatSidenavModule, MatListModule
  ],
  template: `
    <mat-sidenav-container class="shell-container">
      <mat-sidenav mode="side" opened class="shell-nav">
        <div class="brand-card app-panel">
          <p class="brand-kicker">Notification Platform</p>
          <span class="app-title">Control Room</span>
          <p class="brand-copy">Provisioning, rotazione chiavi e governance dei client in un pannello più pulito e operativo.</p>
          @if (authState.state()?.clientName) {
            <span class="client-badge">{{ authState.state()?.clientName }}</span>
          }
        </div>

        <mat-nav-list class="nav-list">
          @if (authState.isAdmin()) {
            <a mat-list-item routerLink="/api-keys" routerLinkActive="active-link" class="nav-link">
              <mat-icon matListItemIcon class="material-symbols-rounded">vpn_key</mat-icon>
              <span matListItemTitle>API Keys</span>
            </a>
          }
        </mat-nav-list>

        <div class="sidenav-footer app-panel">
          <div>
            <span class="footer-label">Sessione</span>
            <strong>{{ authState.isAdmin() ? 'Admin' : 'Client' }}</strong>
          </div>
          <button mat-flat-button (click)="logout()">
            <mat-icon class="material-symbols-rounded">logout</mat-icon>
            Logout
          </button>
        </div>
      </mat-sidenav>

      <mat-sidenav-content class="content-area">
        <mat-toolbar class="app-toolbar">
          <div>
            <span class="toolbar-kicker">Workspace</span>
            <div class="toolbar-title">Client identity administration</div>
          </div>
        </mat-toolbar>

        <main class="main-content">
          <router-outlet />
        </main>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [`
    .shell-container { min-height: 100vh; background: transparent; }
    .shell-nav {
      width: 310px;
      padding: 24px 18px;
      border-right: 1px solid rgba(15, 23, 42, 0.08);
      background:
        linear-gradient(180deg, rgba(248, 250, 252, 0.96) 0%, rgba(238, 246, 244, 0.94) 100%),
        radial-gradient(circle at top left, rgba(249, 115, 22, 0.12), transparent 28%);
      display: flex;
      flex-direction: column;
      gap: 18px;
    }
    .brand-card {
      padding: 22px;
      display: grid;
      gap: 10px;
      position: relative;
      overflow: hidden;
    }
    .brand-card::after {
      content: '';
      position: absolute;
      inset: auto -32px -32px auto;
      width: 110px;
      height: 110px;
      border-radius: 50%;
      background: radial-gradient(circle, rgba(249, 115, 22, 0.18), transparent 68%);
    }
    .brand-kicker,
    .toolbar-kicker,
    .footer-label {
      margin: 0;
      font-size: 11px;
      letter-spacing: 0.12em;
      text-transform: uppercase;
      color: #0f766e;
    }
    .app-title {
      font-size: 1.9rem;
      font-weight: 700;
      line-height: 0.95;
    }
    .brand-copy {
      margin: 0;
      color: #516074;
      line-height: 1.5;
    }
    .client-badge {
      width: fit-content;
      padding: 6px 12px;
      border-radius: 999px;
      background: rgba(15, 118, 110, 0.12);
      color: #115e59;
      font-weight: 600;
    }
    .nav-list {
      display: grid;
      gap: 8px;
    }
    .nav-link {
      border-radius: 18px;
      margin-bottom: 0;
      color: #0f172a;
      min-height: 54px;
    }
    .nav-link :is(.mdc-list-item__primary-text, .mat-mdc-list-item-title) {
      font-weight: 600;
    }
    .active-link {
      background: linear-gradient(90deg, rgba(15, 118, 110, 0.15), rgba(249, 115, 22, 0.1));
      border: 1px solid rgba(15, 118, 110, 0.08);
    }
    .sidenav-footer {
      margin-top: auto;
      padding: 18px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }
    .sidenav-footer strong {
      display: block;
      margin-top: 4px;
      font-size: 15px;
    }
    .content-area {
      background: transparent;
    }
    .app-toolbar {
      position: sticky;
      top: 0;
      z-index: 2;
      height: auto;
      padding: 22px 32px 0;
      background: transparent;
    }
    .toolbar-title {
      font-size: 1.4rem;
      font-weight: 700;
      color: #0f172a;
    }
    .main-content {
      padding: 24px 32px 32px;
    }
    @media (max-width: 960px) {
      .shell-nav {
        width: 270px;
      }
      .app-toolbar,
      .main-content {
        padding-left: 20px;
        padding-right: 20px;
      }
    }
  `]
})
export class ShellComponent {
  constructor(
    public authState: AuthStateService,
    private authService: AuthService,
    private router: Router
  ) {}

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}

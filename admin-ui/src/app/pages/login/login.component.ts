import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, MatCardModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <div class="login-container">
      <mat-card class="login-card app-panel">
        <div class="login-hero">
          <span class="hero-kicker">Notification Platform</span>
          <h1>Client access desk</h1>
          <p>Autentica l’accesso amministrativo e gestisci emissione, revoca e provisioning delle chiavi client.</p>
        </div>

        <mat-card-content>
          <form (ngSubmit)="onSubmit()" #f="ngForm">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Username</mat-label>
              <input matInput [(ngModel)]="username" name="username" required autocomplete="username" />
            </mat-form-field>
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Password</mat-label>
              <input matInput type="password" [(ngModel)]="password" name="password" required autocomplete="current-password" />
            </mat-form-field>
            @if (error) {
              <p class="error-msg">{{ error }}</p>
            }
            <button mat-flat-button color="primary" type="submit" class="full-width" [disabled]="loading">
              @if (loading) {
                <mat-spinner diameter="20"></mat-spinner>
              } @else {
                <ng-container>
                  <mat-icon class="material-symbols-rounded">login</mat-icon>
                  Accedi
                </ng-container>
              }
            </button>
          </form>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .login-container {
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 24px;
      background:
        radial-gradient(circle at top left, rgba(15, 118, 110, 0.14), transparent 26%),
        radial-gradient(circle at bottom right, rgba(249, 115, 22, 0.12), transparent 24%);
    }
    .login-card {
      width: min(440px, 100%);
      padding: 28px;
      border-radius: 28px;
    }
    .login-hero {
      margin-bottom: 20px;
      padding: 24px;
      border-radius: 24px;
      color: white;
      background:
        linear-gradient(135deg, #0f766e 0%, #115e59 55%, #f97316 100%),
        radial-gradient(circle at top right, rgba(255, 255, 255, 0.18), transparent 40%);
    }
    .hero-kicker {
      display: inline-block;
      margin-bottom: 10px;
      font-size: 11px;
      letter-spacing: 0.12em;
      text-transform: uppercase;
      opacity: 0.82;
    }
    .login-hero h1 {
      margin: 0 0 8px;
      font-size: 2rem;
      line-height: 1;
    }
    .login-hero p {
      margin: 0;
      line-height: 1.5;
      max-width: 30ch;
      color: rgba(255, 255, 255, 0.86);
    }
    .full-width {
      width: 100%;
      margin-bottom: 12px;
    }
    .error-msg {
      color: #b91c1c;
      font-size: 14px;
      margin-bottom: 12px;
      padding: 10px 12px;
      border-radius: 14px;
      background: rgba(254, 242, 242, 0.9);
      border: 1px solid rgba(185, 28, 28, 0.15);
    }
    button.full-width {
      min-height: 48px;
    }
    button.full-width .mat-icon {
      margin-right: 8px;
    }
  `]
})
export class LoginComponent {
  username = '';
  password = '';
  loading = false;
  error = '';

  constructor(private authService: AuthService, private router: Router) {}

  async onSubmit(): Promise<void> {
    this.loading = true;
    this.error = '';
    try {
      await this.authService.loginWithPassword(this.username, this.password);
      this.router.navigate(['/api-keys']);
    } catch {
      this.error = 'Credenziali non valide.';
    } finally {
      this.loading = false;
    }
  }
}

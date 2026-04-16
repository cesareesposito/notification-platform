import { Component, OnInit } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { AuthStateService } from '../../services/auth-state.service';
import { TenantFormDialogComponent } from './tenant-form-dialog.component';

export interface TenantSummary {
  tenantId: string;
  displayName: string;
  emailProvider: string;
  pushProvider: string;
  isActive: boolean;
  clientId?: string;
  providerSettings?: Record<string, string>;
  emailFrom?: string;
  emailFromName?: string;
  rateLimitPerMinute?: number;
}

@Component({
  selector: 'app-tenants',
  standalone: true,
  imports: [MatCardModule, MatButtonModule, MatIconModule, MatChipsModule, MatProgressSpinnerModule, MatDialogModule, MatSnackBarModule],
  template: `
    <section class="page-shell">
      <div class="page-header">
        <div>
          <h1 class="page-title">Tenant</h1>
          <p class="page-subtitle">Vista rapida dello stato dei tenant configurati, provider attivi e collegamenti client.</p>
        </div>
        @if (authState.isAdmin()) {
          <button mat-flat-button color="primary" (click)="createTenant()">
            <mat-icon class="material-symbols-rounded">add</mat-icon>
            Nuovo tenant
          </button>
        }
      </div>

      @if (error) {
        <div class="status-banner">
          <mat-icon class="material-symbols-rounded">warning</mat-icon>
          <div>
            <strong>Caricamento tenant non riuscito.</strong>
            <div>{{ error }}</div>
          </div>
        </div>
      }

      @if (loading) {
        <div class="loading-state app-panel">
          <mat-spinner diameter="42"></mat-spinner>
        </div>
      } @else {
        <div class="tenant-grid">
          @for (tenant of tenants; track tenant.tenantId) {
            <mat-card class="tenant-card app-panel" [class.inactive]="!tenant.isActive">
          <mat-card-header>
            <mat-card-title>{{ tenant.displayName }}</mat-card-title>
            <mat-card-subtitle>{{ tenant.tenantId }}</mat-card-subtitle>
          </mat-card-header>
          <mat-card-content>
            <div class="chip-row">
              <mat-chip>{{ tenant.emailProvider }}</mat-chip>
              <mat-chip>{{ tenant.pushProvider }}</mat-chip>
              @if (!tenant.isActive) { <mat-chip color="warn">Inattivo</mat-chip> }
            </div>
            @if (tenant.clientId) {
              <p class="client-label">Client: {{ tenant.clientId }}</p>
            }
          </mat-card-content>
          <mat-card-actions>
            <button mat-button type="button" (click)="editTenant(tenant)">Gestisci</button>
            @if (authState.isAdmin()) {
              <button mat-button type="button" color="warn" (click)="deleteTenant(tenant)">Elimina</button>
            }
          </mat-card-actions>
            </mat-card>
          }
        </div>
      }

      @if (!loading && tenants.length === 0) {
        <div class="empty-state app-panel">
          <mat-icon class="material-symbols-rounded">apartment</mat-icon>
          <p>Nessun tenant trovato.</p>
        </div>
      }
    </section>
  `,
  styles: [`
    .tenant-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 18px;
    }
    .tenant-card {
      padding: 10px;
      transition: transform 160ms ease, box-shadow 160ms ease;
    }
    .tenant-card:hover {
      transform: translateY(-2px);
    }
    .tenant-card.inactive {
      opacity: 0.7;
    }
    .chip-row {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
      margin-bottom: 12px;
    }
    .client-label {
      font-size: 12px;
      color: #516074;
      margin: 0;
    }
    .loading-state,
    .empty-state {
      min-height: 220px;
      display: grid;
      place-items: center;
      padding: 32px;
    }
    .empty-state {
      gap: 12px;
    }
    .empty-state mat-icon {
      width: 42px;
      height: 42px;
      font-size: 42px;
      color: #0f766e;
    }
  `]
})
export class TenantsComponent implements OnInit {
  tenants: TenantSummary[] = [];
  loading = false;
  error = '';

  constructor(
    private api: ApiService,
    public authState: AuthStateService,
    private dialog: MatDialog,
    private router: Router,
    private authService: AuthService,
    private snackBar: MatSnackBar
  ) {}

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async load(): Promise<void> {
    this.loading = true;
    this.error = '';
    try {
      this.tenants = await firstValueFrom(this.api.get<TenantSummary[]>('/api/admin/tenants'));
    } catch (error) {
      this.tenants = [];
      this.handleError(error, 'Impossibile caricare i tenant.');
    } finally {
      this.loading = false;
    }
  }

  createTenant(): void {
    const ref = this.dialog.open(TenantFormDialogComponent, { width: '520px', data: null });
    ref.afterClosed().subscribe(result => { if (result) this.load(); });
  }

  editTenant(tenant: TenantSummary): void {
    const ref = this.dialog.open(TenantFormDialogComponent, { width: '520px', data: tenant });
    ref.afterClosed().subscribe(result => { if (result) this.load(); });
  }

  async deleteTenant(tenant: TenantSummary): Promise<void> {
    if (!this.authState.isAdmin()) {
      return;
    }

    const confirmed = window.confirm(`Eliminare il tenant ${tenant.displayName}?`);
    if (!confirmed) {
      return;
    }

    try {
      await firstValueFrom(this.api.delete(`/api/admin/tenants/${tenant.tenantId}`));
      this.snackBar.open('Tenant eliminato.', 'OK', { duration: 3000 });
      await this.load();
    } catch (error) {
      this.handleError(error, 'Impossibile eliminare il tenant.');
    }
  }

  private handleError(error: unknown, fallbackMessage: string): void {
    if (error instanceof HttpErrorResponse) {
      this.error = error.error?.error || fallbackMessage;
      if (error.status === 401 || error.status === 403) {
        this.authService.logout();
        this.router.navigate(['/login']);
      }
    } else {
      this.error = fallbackMessage;
    }

    this.snackBar.open(this.error, 'OK', { duration: 4000 });
  }
}

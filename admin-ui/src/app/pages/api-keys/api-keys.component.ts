import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { CreateApiKeyDialogComponent, CreatedApiKeyResult } from './create-api-key-dialog.component';

export interface ApiKeySummary {
  id: string;
  clientName: string;
  clientId: string;
  isActive: boolean;
  createdAt: string;
  revokedAt?: string;
}

type ApiKeySummaryDto = Partial<ApiKeySummary> & {
  Id?: string;
  ClientName?: string;
  ClientId?: string;
  IsActive?: boolean;
  CreatedAt?: string;
  RevokedAt?: string;
  $values?: ApiKeySummaryDto[];
};

@Component({
  selector: 'app-api-keys',
  standalone: true,
  imports: [DatePipe, MatCardModule, MatButtonModule, MatIconModule, MatTableModule, MatChipsModule, MatProgressSpinnerModule, MatFormFieldModule, MatInputModule, MatDialogModule, MatSnackBarModule],
  template: `
    <section class="page-shell">
      <mat-card class="hero-card app-panel">
        <div>
          <p class="hero-kicker">Access orchestration</p>
          <h1 class="page-title">Client API keys</h1>
          <p class="page-subtitle">Genera la chiave, crea il client in trasparenza e mostra subito il valore raw da copiare. Nessun inserimento manuale del client identifier.</p>
        </div>
        <div class="hero-actions">
          <div class="hero-note">
            <strong>{{ keys.length }}</strong>
            <span>client registrati</span>
          </div>
          <button mat-flat-button color="primary" (click)="create()">
            <mat-icon class="material-symbols-rounded">add</mat-icon>
            Nuova chiave
          </button>
        </div>
      </mat-card>

      <div class="section-label">Registro credenziali</div>

      <div class="page-header compact-header">
        <div>
          <h2>Client attivi e revocati</h2>
          <p>Ogni chiave è associata a un Client ID stabile usato dalle API e dal flusso di autenticazione.</p>
        </div>
      </div>

      @if (lastCreatedKey) {
        <mat-card class="created-key-card app-panel">
          <div class="created-key-header">
            <div>
              <p class="section-label inline-label">Ultima chiave generata</p>
              <strong>{{ lastCreatedKey.clientName }}</strong>
              <span>{{ lastCreatedKey.clientId }}</span>
            </div>
            <button mat-stroked-button type="button" (click)="copyLastCreatedKey()">
              <mat-icon class="material-symbols-rounded">content_copy</mat-icon>
              Copia chiave
            </button>
          </div>

          <mat-form-field appearance="outline" class="full-width generated-key-field">
            <mat-label>Raw API key</mat-label>
            <textarea matInput readonly [value]="lastCreatedKey.rawKey"></textarea>
          </mat-form-field>
        </mat-card>
      }

      @if (error) {
        <div class="status-banner">
          <mat-icon class="material-symbols-rounded">error</mat-icon>
          <div>
            <strong>Caricamento non riuscito.</strong>
            <div>{{ error }}</div>
          </div>
        </div>
      }

      <mat-card class="table-card app-panel">
        @if (loading) {
          <div class="loading-state">
            <mat-spinner diameter="42"></mat-spinner>
          </div>
        } @else if (keys.length === 0) {
          <div class="empty-state">
            <mat-icon class="material-symbols-rounded">vpn_key_off</mat-icon>
            <p>Nessuna chiave client presente nel database.</p>
          </div>
        } @else {
          <table mat-table [dataSource]="keys" class="full-width data-table">
        <ng-container matColumnDef="clientName">
          <th mat-header-cell *matHeaderCellDef>Cliente</th>
          <td mat-cell *matCellDef="let k">
            <div class="client-cell">
              <strong>{{ k.clientName }}</strong>
              <span>{{ k.clientId }}</span>
            </div>
          </td>
        </ng-container>
        <ng-container matColumnDef="clientId">
          <th mat-header-cell *matHeaderCellDef>Client ID</th>
          <td mat-cell *matCellDef="let k"><code>{{ k.clientId }}</code></td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>Stato</th>
          <td mat-cell *matCellDef="let k">
            @if (k.isActive) { <mat-chip color="primary">Attiva</mat-chip> }
            @else { <mat-chip color="warn">Revocata</mat-chip> }
          </td>
        </ng-container>
        <ng-container matColumnDef="createdAt">
          <th mat-header-cell *matHeaderCellDef>Creata il</th>
          <td mat-cell *matCellDef="let k">{{ k.createdAt | date:'dd/MM/yyyy HH:mm' }}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef></th>
          <td mat-cell *matCellDef="let k">
            @if (k.isActive) {
              <button mat-icon-button color="warn" (click)="revoke(k)" title="Revoca">
                <mat-icon class="material-symbols-rounded">block</mat-icon>
              </button>
            }
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        }
      </mat-card>
    </section>
  `,
  styles: [`
    .hero-card {
      padding: 28px;
      display: flex;
      justify-content: space-between;
      gap: 24px;
      align-items: flex-end;
      background:
        linear-gradient(135deg, rgba(15, 118, 110, 0.14), rgba(255, 255, 255, 0.92)),
        radial-gradient(circle at top right, rgba(249, 115, 22, 0.18), transparent 32%);
    }
    .hero-kicker,
    .section-label {
      text-transform: uppercase;
      letter-spacing: 0.14em;
      font-size: 11px;
      color: #0f766e;
    }
    .hero-kicker {
      margin: 0 0 10px;
    }
    .hero-actions {
      display: grid;
      gap: 14px;
      justify-items: end;
    }
    .hero-note {
      display: grid;
      justify-items: end;
      color: #516074;
    }
    .hero-note strong {
      font-size: 2rem;
      color: #0f172a;
      line-height: 1;
    }
    .compact-header {
      align-items: end;
    }
    .compact-header h2 {
      margin: 0;
      font-size: 1.15rem;
    }
    .compact-header p {
      margin: 6px 0 0;
      color: #516074;
    }
    .table-card {
      padding: 8px;
      overflow: hidden;
    }
    .created-key-card {
      padding: 20px;
      display: grid;
      gap: 14px;
      border-color: rgba(15, 118, 110, 0.18);
      background: linear-gradient(135deg, rgba(240, 253, 250, 0.92), rgba(255, 255, 255, 0.94));
    }
    .created-key-header {
      display: flex;
      justify-content: space-between;
      gap: 16px;
      align-items: start;
    }
    .created-key-header strong {
      display: block;
      font-size: 1rem;
      margin-bottom: 4px;
    }
    .created-key-header span {
      color: #516074;
      font-size: 13px;
    }
    .inline-label {
      margin: 0 0 10px;
    }
    .generated-key-field textarea {
      min-height: 92px;
      font-family: 'IBM Plex Mono', monospace;
      letter-spacing: 0.02em;
    }
    .loading-state {
      min-height: 220px;
      display: grid;
      place-items: center;
    }
    .empty-state {
      display: grid;
      gap: 12px;
      place-items: center;
    }
    .empty-state mat-icon {
      font-size: 42px;
      width: 42px;
      height: 42px;
      color: #0f766e;
    }
    .full-width {
      width: 100%;
    }
    .client-cell {
      display: grid;
      gap: 4px;
    }
    .client-cell strong {
      font-size: 14px;
    }
    .client-cell span,
    code {
      color: #516074;
      font-size: 12px;
    }
    code {
      font-family: 'IBM Plex Mono', monospace;
      background: rgba(15, 23, 42, 0.04);
      padding: 4px 8px;
      border-radius: 999px;
    }
    .data-table th {
      color: #516074;
      font-size: 12px;
      letter-spacing: 0.06em;
      text-transform: uppercase;
    }
    .data-table td,
    .data-table th {
      padding-top: 18px;
      padding-bottom: 18px;
    }
    .data-table tr:hover td {
      background: rgba(15, 118, 110, 0.04);
    }
    @media (max-width: 960px) {
      .hero-card {
        flex-direction: column;
        align-items: stretch;
      }
      .hero-actions,
      .hero-note {
        justify-items: start;
      }
      .created-key-header {
        flex-direction: column;
        align-items: stretch;
      }
      .table-card {
        overflow-x: auto;
      }
    }
  `]
})
export class ApiKeysComponent implements OnInit {
  keys: ApiKeySummary[] = [];
  lastCreatedKey: CreatedApiKeyResult | null = null;
  loading = false;
  error = '';
  columns = ['clientName', 'clientId', 'createdAt', 'status', 'actions'];

  constructor(
    private api: ApiService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar,
    private authService: AuthService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async load(): Promise<void> {
    this.loading = true;
    this.error = '';
    try {
      const response = await firstValueFrom(this.api.get<ApiKeySummaryDto[] | ApiKeySummaryDto>('/api/admin/apikeys'));
      this.keys = this.normalizeApiKeys(response);
    } catch (error) {
      this.keys = [];
      this.handleError(error, 'Impossibile caricare le API key.');
    } finally {
      this.loading = false;
      this.cdr.detectChanges();
    }
  }

  create(): void {
    const ref = this.dialog.open(CreateApiKeyDialogComponent, { width: '480px' });
    ref.afterClosed().subscribe(async (result: CreatedApiKeyResult | true | undefined) => {
      if (!result) {
        return;
      }

      if (result !== true) {
        this.lastCreatedKey = result;
      }

      await this.load();
    });
  }

  async revoke(key: ApiKeySummary): Promise<void> {
    if (!confirm(`Revocare la chiave di "${key.clientName}"?`)) return;
    try {
      await firstValueFrom(this.api.delete(`/api/admin/apikeys/${encodeURIComponent(key.clientId)}`));
      this.snackBar.open('Chiave revocata.', 'OK', { duration: 3000 });
      await this.load();
    } catch (error) {
      this.handleError(error, 'Impossibile revocare la chiave selezionata.');
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

  async copyLastCreatedKey(): Promise<void> {
    if (!this.lastCreatedKey) {
      return;
    }

    try {
      await navigator.clipboard.writeText(this.lastCreatedKey.rawKey);
      this.snackBar.open('API key copiata negli appunti.', 'OK', { duration: 3000 });
    } catch {
      this.snackBar.open('Copia automatica non riuscita. Copia la chiave dal riquadro.', 'OK', { duration: 4000 });
    }
  }

  private normalizeApiKeys(response: ApiKeySummaryDto[] | ApiKeySummaryDto): ApiKeySummary[] {
    const items = Array.isArray(response)
      ? response
      : Array.isArray(response?.$values)
        ? response.$values
        : [];

    return items.map(item => ({
      id: item.id ?? item.Id ?? '',
      clientName: item.clientName ?? item.ClientName ?? '-',
      clientId: item.clientId ?? item.ClientId ?? '-',
      isActive: item.isActive ?? item.IsActive ?? false,
      createdAt: item.createdAt ?? item.CreatedAt ?? '',
      revokedAt: item.revokedAt ?? item.RevokedAt,
    }));
  }
}

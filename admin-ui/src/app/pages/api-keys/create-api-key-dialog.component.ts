import { ChangeDetectorRef, Component } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { TextFieldModule } from '@angular/cdk/text-field';
import { ApiService } from '../../services/api.service';

interface CreateApiKeyResponse {
  rawKey?: string;
  clientName?: string;
  clientId?: string;
  tenantCreated?: boolean;
  RawKey?: string;
  ClientName?: string;
  ClientId?: string;
  TenantCreated?: boolean;
}

export interface CreatedApiKeyResult {
  rawKey: string;
  clientName: string;
  clientId: string;
  tenantCreated: boolean;
}

@Component({
  selector: 'app-create-api-key-dialog',
  standalone: true,
  imports: [
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    TextFieldModule,
    MatSnackBarModule
  ],
  template: `
    <h2 mat-dialog-title>Nuova API Key</h2>
    <mat-dialog-content>
      @if (!createdRawKey) {
        <form #f="ngForm">
          <p class="intro-copy">Inserisci solo il nome cliente. Il backend genera il Client ID, crea il record se manca e associa la nuova chiave a quel client.</p>
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Client name</mat-label>
            <input matInput [(ngModel)]="clientName" name="clientName" required placeholder="es. om" />
            <mat-hint>Il Client ID viene derivato automaticamente dal nome inserito.</mat-hint>
          </mat-form-field>
          @if (error) { <p class="error">{{ error }}</p> }
        </form>
      } @else {
        <div class="success-banner">
          <mat-icon class="material-symbols-rounded">check_circle</mat-icon>
          <div>
            <strong>API key creata correttamente.</strong>
            <div>Copia la chiave ora: dopo la chiusura del dialog non sarà più recuperabile.</div>
            @if (tenantCreated) {
              <div>Il tenant è stato creato automaticamente durante l'emissione della chiave.</div>
            }
          </div>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Chiave generata</mat-label>
          <textarea
            matInput
            readonly
            cdkTextareaAutosize
            cdkAutosizeMinRows="3"
            cdkAutosizeMaxRows="5"
            [value]="createdRawKey"
          ></textarea>
        </mat-form-field>

        <div class="result-meta">
          <span><strong>Client:</strong> {{ createdClientName || clientName }}</span>
          <span><strong>Client ID:</strong> {{ createdClientId }}</span>
        </div>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      @if (!createdRawKey) {
        <button mat-button mat-dialog-close>Annulla</button>
        <button mat-flat-button color="primary" (click)="save()" [disabled]="loading || !canSave">
          @if (loading) {
            <mat-spinner diameter="18"></mat-spinner>
          } @else {
            Crea
          }
        </button>
      } @else {
        <button mat-button type="button" (click)="copyKey()">
          <mat-icon class="material-symbols-rounded">content_copy</mat-icon>
          Copia
        </button>
        <button mat-flat-button color="primary" (click)="close()">Chiudi</button>
      }
    </mat-dialog-actions>
  `,
  styles: [`
    .full-width { width: 100%; margin-bottom: 8px; }
    .intro-copy {
      margin: 0 0 18px;
      color: #516074;
      line-height: 1.55;
    }
    .error { color: #f44336; }
    .success-banner {
      display: flex;
      gap: 12px;
      align-items: flex-start;
      margin-bottom: 16px;
      padding: 14px 16px;
      border-radius: 16px;
      color: #166534;
      background: #f0fdf4;
      border: 1px solid #bbf7d0;
    }
    .success-banner mat-icon {
      color: #16a34a;
    }
    .result-meta {
      display: grid;
      gap: 6px;
      color: #516074;
      font-size: 13px;
    }
    mat-dialog-actions button mat-icon {
      margin-right: 6px;
    }
  `]
})
export class CreateApiKeyDialogComponent {
  clientName = '';
  createdRawKey = '';
  createdClientName = '';
  createdClientId = '';
  tenantCreated = false;
  loading = false;
  error = '';
  private createdResult: CreatedApiKeyResult | null = null;

  constructor(
    private api: ApiService,
    private dialogRef: MatDialogRef<CreateApiKeyDialogComponent>,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef
  ) {}

  get canSave(): boolean {
    return this.clientName.trim().length > 0;
  }

  async save(): Promise<void> {
    this.loading = true;
    this.error = '';
    try {
      const res = await firstValueFrom(
        this.api.post<CreateApiKeyResponse>('/api/admin/apikeys', {
          clientName: this.clientName
        })
      );

      const normalized = this.normalizeResponse(res);

      if (!normalized.rawKey) {
        throw new Error('API key creata ma rawKey non presente nella risposta.');
      }

      this.createdResult = normalized;
      this.createdRawKey = normalized.rawKey;
      this.createdClientName = normalized.clientName;
      this.createdClientId = normalized.clientId;
      this.tenantCreated = normalized.tenantCreated;
      this.loading = false;
      this.cdr.detectChanges();
      this.dialogRef.updateSize('640px');
      await this.copyKey(true);
    } catch (error) {
      this.error = error instanceof HttpErrorResponse
        ? error.error?.error || 'Errore durante la creazione.'
        : 'Errore durante la creazione.';
    } finally {
      this.loading = false;
      this.cdr.detectChanges();
    }
  }

  async copyKey(silent = false): Promise<void> {
    if (!this.createdRawKey) {
      return;
    }

    try {
      await navigator.clipboard.writeText(this.createdRawKey);
      if (!silent) {
        this.snackBar.open('API key copiata negli appunti.', 'OK', { duration: 3000 });
      }
    } catch {
      this.snackBar.open('Copia automatica non riuscita. Puoi copiarla manualmente dal campo.', 'OK', { duration: 4000 });
    }
  }

  close(): void {
    this.dialogRef.close(this.createdResult ?? true);
  }

  private normalizeResponse(response: CreateApiKeyResponse): CreatedApiKeyResult {
    return {
      rawKey: response.rawKey ?? response.RawKey ?? '',
      clientName: response.clientName ?? response.ClientName ?? this.clientName,
      clientId: response.clientId ?? response.ClientId ?? '',
      tenantCreated: response.tenantCreated ?? response.TenantCreated ?? false,
    };
  }
}

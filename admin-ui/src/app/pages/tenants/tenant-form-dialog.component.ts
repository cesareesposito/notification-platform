import { Component, Inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-tenant-form-dialog',
  standalone: true,
  imports: [FormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatCheckboxModule],
  template: `
    <h2 mat-dialog-title>{{ isEdit ? 'Modifica tenant' : 'Nuovo tenant' }}</h2>
    <mat-dialog-content>
      <form #f="ngForm">
        @if (!isEdit) {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Tenant ID</mat-label>
            <input matInput [(ngModel)]="form.tenantId" name="tenantId" required placeholder="es. om-orgA" />
          </mat-form-field>
        }
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Display Name</mat-label>
          <input matInput [(ngModel)]="form.displayName" name="displayName" required />
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Client ID</mat-label>
          <input matInput [(ngModel)]="form.clientId" name="clientId" placeholder="es. om" />
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Email Provider</mat-label>
          <mat-select [(ngModel)]="form.emailProvider" name="emailProvider">
            <mat-option value="Smtp">SMTP</mat-option>
            <mat-option value="SendGrid">SendGrid</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Email From</mat-label>
          <input matInput [(ngModel)]="form.emailFrom" name="emailFrom" />
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Email From Name</mat-label>
          <input matInput [(ngModel)]="form.emailFromName" name="emailFromName" />
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>SMTP Host</mat-label>
          <input matInput [(ngModel)]="form.providerSettings['Smtp:Host']" name="smtpHost" />
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>SMTP Port</mat-label>
          <input matInput type="number" [(ngModel)]="form.providerSettings['Smtp:Port']" name="smtpPort" />
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>SMTP Username</mat-label>
          <input matInput [(ngModel)]="form.providerSettings['Smtp:Username']" name="smtpUsername" />
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>SMTP Password</mat-label>
          <input matInput [(ngModel)]="form.providerSettings['Smtp:Password']" name="smtpPassword" />
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Push Provider</mat-label>
          <mat-select [(ngModel)]="form.pushProvider" name="pushProvider">
            <mat-option value="Firebase">Firebase</mat-option>
            <mat-option value="None">None</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-checkbox [(ngModel)]="smtpUseSsl" name="smtpUseSsl" (ngModelChange)="syncSmtpUseSsl($event)">Usa SSL</mat-checkbox>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Rate Limit (msg/min)</mat-label>
          <input matInput type="number" [(ngModel)]="form.rateLimitPerMinute" name="rateLimitPerMinute" />
        </mat-form-field>
        <mat-checkbox [(ngModel)]="form.isActive" name="isActive">Attivo</mat-checkbox>
      </form>
      @if (error) { <p class="error">{{ error }}</p> }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Annulla</button>
      <button mat-flat-button color="primary" (click)="save()" [disabled]="loading">Salva</button>
    </mat-dialog-actions>
  `,
  styles: [`.full-width { width: 100%; margin-bottom: 8px; } .error { color: #f44336; }`]
})
export class TenantFormDialogComponent {
  isEdit: boolean;
  form = {
    tenantId: '',
    displayName: '',
    clientId: '',
    emailProvider: 'Smtp',
    emailFrom: '',
    emailFromName: '',
    pushProvider: 'Firebase',
    providerSettings: {
      'Smtp:Host': '',
      'Smtp:Port': '587',
      'Smtp:Username': '',
      'Smtp:Password': '***',
      'Smtp:UseSsl': 'false'
    } as Record<string, string>,
    rateLimitPerMinute: 100,
    isActive: true
  };
  smtpUseSsl = false;
  loading = false;
  error = '';

  constructor(
    private api: ApiService,
    private dialogRef: MatDialogRef<TenantFormDialogComponent>,
    @Inject(MAT_DIALOG_DATA) data: { tenantId: string; [k: string]: unknown } | null
  ) {
    this.isEdit = data !== null;
    if (data) {
      Object.assign(this.form, data);
      this.form.providerSettings = {
        'Smtp:Host': '',
        'Smtp:Port': '587',
        'Smtp:Username': '',
        'Smtp:Password': '***',
        'Smtp:UseSsl': 'false',
        ...(data['providerSettings'] as Record<string, string> | undefined ?? {})
      };
      this.smtpUseSsl = this.form.providerSettings['Smtp:UseSsl'] === 'true';
    }
  }

  syncSmtpUseSsl(checked: boolean): void {
    this.form.providerSettings['Smtp:UseSsl'] = checked ? 'true' : 'false';
  }

  async save(): Promise<void> {
    this.loading = true;
    this.error = '';
    try {
      const body = {
        ...this.form,
        clientId: this.form.clientId || null,
        providerSettings: { ...this.form.providerSettings }
      };
      if (this.isEdit) {
        await firstValueFrom(this.api.put(`/api/admin/tenants/${this.form.tenantId}`, body));
      } else {
        await firstValueFrom(this.api.post(`/api/admin/tenants/${this.form.tenantId}`, body));
      }
      this.dialogRef.close(true);
    } catch (e: unknown) {
      this.error = 'Errore durante il salvataggio.';
    } finally {
      this.loading = false;
    }
  }
}

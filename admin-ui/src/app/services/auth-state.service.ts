import { Injectable, signal } from '@angular/core';

export interface AuthState {
  token: string;
  scope: 'admin' | 'client';
  clientId?: string;
  clientName?: string;
  expiresAt: number; // unix timestamp ms
}

const STORAGE_KEY = 'notification-admin-auth';

@Injectable({ providedIn: 'root' })
export class AuthStateService {
  private _state = signal<AuthState | null>(null);

  readonly state = this._state.asReadonly();

  constructor() {
    this.restore();
  }

  set(state: AuthState): void {
    this._state.set(state);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
  }

  clear(): void {
    this._state.set(null);
    localStorage.removeItem(STORAGE_KEY);
  }

  isAuthenticated(): boolean {
    const s = this._state();
    return s !== null && s.expiresAt > Date.now();
  }

  isAdmin(): boolean {
    return this._state()?.scope === 'admin';
  }

  get token(): string | null {
    return this._state()?.token ?? null;
  }

  get clientId(): string | null {
    return this._state()?.clientId ?? null;
  }

  private restore(): void {
    const rawState = localStorage.getItem(STORAGE_KEY);
    if (!rawState) {
      return;
    }

    try {
      const parsed = JSON.parse(rawState) as Partial<AuthState>;
      if (!this.isValidState(parsed) || parsed.expiresAt <= Date.now()) {
        localStorage.removeItem(STORAGE_KEY);
        return;
      }

      this._state.set(parsed);
    } catch {
      localStorage.removeItem(STORAGE_KEY);
    }
  }

  private isValidState(state: Partial<AuthState>): state is AuthState {
    return typeof state.token === 'string'
      && (state.scope === 'admin' || state.scope === 'client')
      && typeof state.expiresAt === 'number';
  }
}

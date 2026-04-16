import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthState, AuthStateService } from './auth-state.service';

interface LoginResponse {
  token: string;
  scope: 'admin' | 'client';
  clientId?: string;
  clientName?: string;
  expiresIn: number;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(private http: HttpClient, private authState: AuthStateService) {}

  async loginWithPassword(username: string, password: string): Promise<void> {
    const res = await firstValueFrom(
      this.http.post<LoginResponse>('/api/auth/admin/login', { username, password })
    );
    this.storeToken(res);
  }

  async exchangeApiKey(apiKey: string): Promise<void> {
    const res = await firstValueFrom(
      this.http.post<LoginResponse>('/api/auth/admin/exchange', { apiKey })
    );
    this.storeToken(res);
  }

  logout(): void {
    this.authState.clear();
  }

  private storeToken(res: LoginResponse): void {
    const state: AuthState = {
      token: res.token,
      scope: res.scope,
      clientId: res.clientId,
      clientName: res.clientName,
      expiresAt: Date.now() + res.expiresIn * 1000,
    };
    this.authState.set(state);
  }
}

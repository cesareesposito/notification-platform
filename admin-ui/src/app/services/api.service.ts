import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthStateService } from './auth-state.service';

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private http: HttpClient, private authState: AuthStateService) {}

  private headers(): HttpHeaders {
    const token = this.authState.token;
    return token ? new HttpHeaders({ Authorization: `Bearer ${token}` }) : new HttpHeaders();
  }

  get<T>(path: string): Observable<T> {
    return this.http.get<T>(path, { headers: this.headers() });
  }

  post<T>(path: string, body: unknown): Observable<T> {
    return this.http.post<T>(path, body, { headers: this.headers() });
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<T>(path, body, { headers: this.headers() });
  }

  delete<T>(path: string): Observable<T> {
    return this.http.delete<T>(path, { headers: this.headers() });
  }
}

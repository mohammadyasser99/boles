import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, LoginRequest, LoginResponse, RefreshTokenResponse } from '../models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private readonly base = `${environment.apiUrl}/auth`;

  private _isLoggedIn$ = new BehaviorSubject<boolean>(this.hasToken());
  isLoggedIn$ = this._isLoggedIn$.asObservable();

  login(req: LoginRequest): Observable<ApiResponse<LoginResponse>> {
    return this.http.post<ApiResponse<LoginResponse>>(`${this.base}/login`, req).pipe(
      tap(res => {
        if (res.success && res.data) {
          localStorage.setItem('access_token', res.data.accessToken);
          localStorage.setItem('refresh_token', res.data.refreshToken);
          localStorage.setItem('admin_name', res.data.name);
          localStorage.setItem('admin_role', res.data.role);
          this._isLoggedIn$.next(true);
        }
      })
    );
  }

  refreshToken(): Observable<ApiResponse<RefreshTokenResponse>> {
    const refreshToken = localStorage.getItem('refresh_token') ?? '';
    return this.http.post<ApiResponse<RefreshTokenResponse>>(`${this.base}/refresh-token`, { refreshToken }).pipe(
      tap(res => {
        if (res.success && res.data) {
          localStorage.setItem('access_token', res.data.accessToken);
          localStorage.setItem('refresh_token', res.data.refreshToken);
        }
      })
    );
  }

  logout(): void {
    localStorage.clear();
    this._isLoggedIn$.next(false);
    this.router.navigate(['/login']);
  }

  getToken(): string | null { return localStorage.getItem('access_token'); }
  getAdminName(): string { return localStorage.getItem('admin_name') ?? 'Admin'; }
  getAdminRole(): string { return localStorage.getItem('admin_role') ?? ''; }
  isSuperAdmin(): boolean { return this.getAdminRole() === 'SuperAdmin'; }
  private hasToken(): boolean { return !!localStorage.getItem('access_token'); }
}

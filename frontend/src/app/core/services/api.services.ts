import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApiResponse, User, CreateUserRequest,
  Car, CreateCarRequest, AssignCarRequest,
  Admin, CreateAdminRequest,
  FineImportResult, EntranceFeeImportResult, CarDebt
} from '../models';

// ── User Service ──────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class UserService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/users`;

  getAll(): Observable<ApiResponse<User[]>> { return this.http.get<ApiResponse<User[]>>(this.base); }
  getById(id: string): Observable<ApiResponse<User>> { return this.http.get<ApiResponse<User>>(`${this.base}/${id}`); }
  create(req: CreateUserRequest): Observable<ApiResponse<User>> { return this.http.post<ApiResponse<User>>(this.base, req); }
  update(id: string, req: CreateUserRequest): Observable<ApiResponse<void>> { return this.http.put<ApiResponse<void>>(`${this.base}/${id}`, req); }
  delete(id: string): Observable<ApiResponse<void>> { return this.http.delete<ApiResponse<void>>(`${this.base}/${id}`); }
}

// ── Car Service ───────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class CarService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/cars`;

  getAll(): Observable<ApiResponse<Car[]>> { return this.http.get<ApiResponse<Car[]>>(this.base); }
  getByPlate(plate: string): Observable<ApiResponse<Car>> { return this.http.get<ApiResponse<Car>>(`${this.base}/${plate}`); }
  create(req: CreateCarRequest): Observable<ApiResponse<Car>> { return this.http.post<ApiResponse<Car>>(this.base, req); }
  assignToUser(req: AssignCarRequest): Observable<ApiResponse<void>> { return this.http.post<ApiResponse<void>>(`${this.base}/assign`, req); }
  setRentalPrice(plate: string, price: number): Observable<ApiResponse<void>> { return this.http.patch<ApiResponse<void>>(`${this.base}/${plate}/rental-price`, price); }
  delete(plate: string): Observable<ApiResponse<void>> { return this.http.delete<ApiResponse<void>>(`${this.base}/${plate}`); }
}

// ── Admin Service ─────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/admins`;

  getAll(): Observable<ApiResponse<Admin[]>> { return this.http.get<ApiResponse<Admin[]>>(this.base); }
  create(req: CreateAdminRequest): Observable<ApiResponse<Admin>> { return this.http.post<ApiResponse<Admin>>(this.base, req); }
  delete(id: string): Observable<ApiResponse<void>> { return this.http.delete<ApiResponse<void>>(`${this.base}/${id}`); }
}

// ── Fine Service ──────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class FineService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/fines`;

markFineAsPaid(violationNumber: string) {
  return this.http.patch<ApiResponse<any>>(
    `${this.base}/${violationNumber}/pay`,
    {}
  );
}
  searchFines(violationNumber?: string, isPaid?: boolean, page = 1, pageSize = 10) {
  let params: any = { page, pageSize };

  if (violationNumber) params.violationNumber = violationNumber;
  if (isPaid !== undefined) params.isPaid = isPaid;

  return this.http.get<ApiResponse<any>>(
    `${this.base}/search`,
    { params }
  );
}

  importFines(file: File): Observable<ApiResponse<FineImportResult>> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ApiResponse<FineImportResult>>(`${this.base}/import`, form);
  }

  getAllDebts(): Observable<ApiResponse<CarDebt[]>> { return this.http.get<ApiResponse<CarDebt[]>>(`${this.base}/debts`); }
  getDebtByPlate(plate: string): Observable<ApiResponse<CarDebt>> { return this.http.get<ApiResponse<CarDebt>>(`${this.base}/debts/${plate}`); }
}

// ── Entrance Fee Service ──────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class EntranceFeeService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/entrance-fees`;
markAsPaid(tripNumber: string) {
  return this.http.patch<ApiResponse<any>>(
    `${this.base}/${tripNumber}/pay`,
    {}
  );
}
searchFees(tripNumber?: string, isPaid?: boolean, page = 1, pageSize = 10) {
  let params: any = {
    page,
    pageSize
  };

  if (tripNumber) params.tripNumber = tripNumber;
  if (isPaid !== undefined) params.isPaid = isPaid;

  return this.http.get<ApiResponse<any>>(
    `${this.base}/search`,
    { params }
  );
}

  importFees(file: File): Observable<ApiResponse<EntranceFeeImportResult>> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ApiResponse<EntranceFeeImportResult>>(`${this.base}/import`, form);
  }
}

import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApiResponse, User, CreateUserRequest,
  Car, CreateCarRequest, AssignCarRequest,
  Admin, CreateAdminRequest,
  FineImportResult, EntranceFeeImportResult, CarDebt,
  PagedResult, CarSummaryDto,
  MonthlyRentalPaymentDto, CreateMonthlyRentalPaymentRequestDto, UpdateMonthlyRentalPaymentRequestDto,
  SystemMonthlyRowDto
} from '../models';

// ── User Service ──────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class UserService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/users`;

  getAll(): Observable<ApiResponse<User[]>> { return this.http.get<ApiResponse<User[]>>(this.base); }
  getById(id: string): Observable<ApiResponse<User>> { return this.http.get<ApiResponse<User>>(`${this.base}/${id}`); }
  getUserWithCar(userId: string): Observable<ApiResponse<any>> { return this.http.get<ApiResponse<any>>(`${this.base}/GetUserWithCar/${userId}`); }
  create(req: FormData): Observable<ApiResponse<User>> { return this.http.post<ApiResponse<User>>(`${this.base}/createUser`, req); }
  createwithcar(req: any): Observable<ApiResponse<User>> { return this.http.post<ApiResponse<User>>(`${this.base}/CreateUserWithCar`, req); }
  updateCarAndUser(req: any): Observable<ApiResponse<User>> { return this.http.post<ApiResponse<User>>(`${this.base}/UpdateCarAndUser`, req); }
update(id: string, formData: FormData): Observable<ApiResponse<User>> {
  return this.http.put<ApiResponse<User>>(`${this.base}/${id}/update`, formData);
}  
delete(id: string): Observable<ApiResponse<void>> { return this.http.delete<ApiResponse<void>>(`${this.base}/${id}`); }

downloadDocument(documentId: string): Observable<Blob> {
  return this.http.get(
    `${environment.apiUrl}/users/documents/${documentId}/download`,
    {
      responseType: 'blob'
    }
  );
}
}

// ── Car Service ───────────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class CarService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/cars`;

  getAll(): Observable<ApiResponse<Car[]>> { return this.http.get<ApiResponse<Car[]>>(this.base); }
getAllWithDebs(page: number, pageSize: number) {
  return this.http.get<ApiResponse<PagedResult<Car>>>(
    `${this.base}/cars-with-debs?page=${page}&pageSize=${pageSize}`
  );
}
  getCarPaymentReport(plate: string): Observable<ApiResponse<CarSummaryDto>> { return this.http.get<ApiResponse<CarSummaryDto>>(`${this.base}/car-payment-report/${plate}`); }
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
searchFines(
  violationNumber?: string,
  carPlate?: string,
  isPaid?: boolean,
  page: number = 1,
  pageSize: number = 10
) {
  let params: any = {
    page,
    pageSize
  };

  if (violationNumber) {
    params.violationNumber = violationNumber;
  }

  if (carPlate) {
    params.carPlate = carPlate;
  }

  if (isPaid !== null && isPaid !== undefined) {
    params.isPaid = isPaid;
  }

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
searchFees(
  tripNumber?: string,
  carPlate?: string,
  isPaid?: boolean,
  page = 1,
  pageSize = 10
) {
  let params: any = { page, pageSize };

  if (tripNumber) params.tripNumber = tripNumber;
  if (carPlate) params.carPlate = carPlate;
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

// ── Payment Service ───────────────────────────────────────────────────────────
@Injectable({ providedIn: 'root' })
export class PaymentService {
  private http = inject(HttpClient);
  private base = `${environment.apiUrl}/payment`;

  getAll(): Observable<ApiResponse<MonthlyRentalPaymentDto[]>> {
    return this.http.get<ApiResponse<MonthlyRentalPaymentDto[]>>(this.base);
  }

  getSystemSummary() {
    return this.http.get<ApiResponse<SystemMonthlyRowDto>>(
      `${this.base}/system-summary`
    );
  }

  getById(id: string): Observable<ApiResponse<MonthlyRentalPaymentDto>> {
    return this.http.get<ApiResponse<MonthlyRentalPaymentDto>>(`${this.base}/${id}`);
  }

  create(req: CreateMonthlyRentalPaymentRequestDto): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(this.base, req);
  }

  update(id: string, req: UpdateMonthlyRentalPaymentRequestDto): Observable<ApiResponse<any>> {
    return this.http.put<ApiResponse<any>>(`${this.base}/${id}`, req);
  }
}

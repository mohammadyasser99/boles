export interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
}

// Auth
export interface LoginRequest { username: string; password: string; }
export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiry: string;
  adminId: string;
  name: string;
  role: string;
}
export interface RefreshTokenResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiry: string;
}

// User
export interface User { id: string; name: string; phoneNumber: string; email: string; }
export interface CreateUserRequest {
  // User
  name: string;
  phoneNumber: string;
  email: string;
  nationalId?: string;

  // Document (optional)
  documentFile?: File;
  documentType?: string;
}
// Car
export interface Car { carPlate: string; totalDebt: number; rentalPrice: number; userId?: string; userName?: string;totaldebs?:number }
export interface CreateCarRequest {   carPlate: string;
  rentalPrice?: number;
  brand?: string;
  model?: string;
  year?: number | null;
  chassisNumber?: string; }
export interface AssignCarRequest { carPlate: string; userId: string; }
export interface CarMonthlyRowDto {
  year: number;
  month: number;
  rentalPrice: number;
  paymentDate: string | null;
  amountPaid: number;
  totalFines: number;
  finesCount: number;
  totalEntranceFees: number;
  entranceFeesCount: number;
}
export interface CarSummaryDto {
  carPlate: string;
  brand: string | null;
  model: string | null;
  carYear: number | null;
  rentalPrice: number;
  rows: CarMonthlyRowDto[];
  joinDate: string | null;
}
export interface MonthlyRentalPaymentDto {
  id: string;
  amount: number;
  paidAt: string;
  carPlate: string;
  userId: string;
}
export interface CreateMonthlyRentalPaymentRequestDto {
  amount: number;
  paidAt: string;
  carPlate: string;
  userId: string;
}
export interface UpdateMonthlyRentalPaymentRequestDto {
  amount: number;
  paidAt: string;
}

// Admin
export interface Admin { id: string; name: string; username: string; role: string; }
export interface CreateAdminRequest { name: string; username: string; password: string; role: string; }

// Fines
export interface FineImportResult {
  totalRowsProcessed: number;
  newFinesAdded: number;
  duplicatesSkipped: number;
  carSummaries: CarFinesSummary[];
}
export interface CarFinesSummary {
  carPlate: string;
  newFinesAmount: number;
  totalDebt: number;
  newViolationsAdded: number;
}

// Entrance Fees
export interface EntranceFeeImportResult {
  totalRowsProcessed: number;
  newFeesAdded: number;
  duplicatesSkipped: number;
  carSummaries: CarEntranceFeeSummary[];
}
export interface CarEntranceFeeSummary {
  carPlate: string;
  newFeesAmount: number;
  totalEntranceFees: number;
  newTripsAdded: number;
}

// Debt
export interface CarDebt {
  carPlate: string;
  totalDebt: number;
  userName?: string;
  userEmail?: string;
  userPhone?: string;
}


export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface SystemMonthlyRowDto {
  year: number;
  month: number;

  totalRevenue: number;
  totalDebt: number;
  netBalance: number;

  totalFines: number;
  finesCount: number;

  totalEntranceFees: number;
  entranceFeesCount: number;
}
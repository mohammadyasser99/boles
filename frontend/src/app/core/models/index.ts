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
export interface CreateUserRequest { name: string; phoneNumber: string; email: string; }

// Car
export interface Car { carPlate: string; totalDebt: number; rentalPrice: number; userId?: string; userName?: string; }
export interface CreateCarRequest { carPlate: string; rentalPrice?: number; }
export interface AssignCarRequest { carPlate: string; userId: string; }

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

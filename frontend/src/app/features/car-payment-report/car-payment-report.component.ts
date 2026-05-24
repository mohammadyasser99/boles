import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';
import { DropdownModule } from 'primeng/dropdown';   // ← SelectModule is not statically analysable in your PrimeNG version; DropdownModule is stable
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { CarService, PaymentService } from 'src/app/core/services/api.services';
import { ApiResponse, CarMonthlyRowDto, CarSummaryDto } from 'src/app/core/models';
import { DialogModule } from 'primeng/dialog';

interface DisplayRow extends CarMonthlyRowDto {
  rentalRemaining: number;   // scheduled - paid
  finesRemaining:  number;   // totalFines - finesPaid
  feesRemaining:   number;   // totalEntranceFees - entranceFeesPaid
  totalDue:        number;
  totalRemaining:  number;
  isPaid:          boolean;
  isPartial:       boolean;
  isOverpaid:      boolean;
}

interface YearOption { label: string; value: number }

const MONTHS = [
  'January','February','March','April','May','June',
  'July','August','September','October','November','December',
];

@Component({
  selector: 'app-car-payment-report',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    TagModule,
    SkeletonModule,
    DropdownModule,    // ← replaces SelectModule; use p-dropdown in the template
    ButtonModule,
    TooltipModule,
    DialogModule
  ],
  templateUrl: './car-payment-report.component.html',
  styleUrl:    './car-payment-report.component.css',
})
export class CarPaymentReportComponent implements OnInit {   // ← implements OnInit added
  private carService = inject(CarService);
  private paymentService = inject(PaymentService);
  private route = inject(ActivatedRoute);
  carPlate = signal('');


  payDialogVisible = signal(false);
payingRow        = signal<DisplayRow | null>(null);
payAmount        = signal<number>(0);
paying           = signal(false);

  // ── State ─────────────────────────────────────────────────────────────────
  loading      = signal(false);
  errorMsg     = signal('');
  summary      = signal<CarSummaryDto | null>(null);
  yearOptions  = signal<YearOption[]>([]);
  selectedYear = signal<number>(new Date().getFullYear());

  readonly skeletonRows = Array(12).fill({});

  // ── Computed rows ─────────────────────────────────────────────────────────

displayRows = computed<DisplayRow[]>(() => {
  const s = this.summary();
  if (!s || !s.rows?.length) return [];

  // ── Start from earliest row returned by API ───────────────────
  const sortedRows = [...s.rows].sort((a, b) => {
    if (a.year !== b.year) return a.year - b.year;
    return a.month - b.month;
  });

  const first = sortedRows[0];
  const last  = sortedRows[sortedRows.length - 1];

  let y = first.year;
  let m = first.month;

  const endYear  = last.year;
  const endMonth = last.month;

  const rows: DisplayRow[] = [];

  while (y < endYear || (y === endYear && m <= endMonth)) {

    if (y === this.selectedYear()) {

      const raw = s.rows.find(r => r.year === y && r.month === m) ?? {
        year: y,
        month: m,
        paymentDate: null,
        rentalPrice: 0,
        rentalPaid: 0,
        finesPaid: 0,
        entranceFeesPaid: 0,
        amountPaid: 0,
        totalFines: 0,
        finesCount: 0,
        totalEntranceFees: 0,
        entranceFeesCount: 0,
      } as CarMonthlyRowDto;

      const rentalRemaining = Math.max(0, raw.rentalPrice - raw.rentalPaid);
      const finesRemaining  = Math.max(0, raw.totalFines - raw.finesPaid);
      const feesRemaining   = Math.max(0, raw.totalEntranceFees - raw.entranceFeesPaid);

      const totalDue =
        raw.rentalPrice +
        raw.totalFines +
        raw.totalEntranceFees;

      const totalPaid =
        raw.rentalPaid +
        raw.finesPaid +
        raw.entranceFeesPaid;

      const totalRemaining =
        rentalRemaining +
        finesRemaining +
        feesRemaining;

      rows.push({
        ...raw,
        rentalRemaining,
        finesRemaining,
        feesRemaining,
        totalDue,
        totalRemaining,
        isPaid: totalDue > 0 && totalRemaining === 0,
        isPartial: totalPaid > 0 && totalRemaining > 0,
        isOverpaid: totalPaid > totalDue,
      });
    }

    m++;

    if (m > 12) {
      m = 1;
      y++;
    }
  }

  return rows;
});

totals = computed(() => {
  const s = this.summary();
  if (!s?.rows?.length) {
    return {
      rentalDue: 0, rentalPaid: 0,
      finesPaid: 0, totalFines: 0,
      entranceFeesPaid: 0, totalFees: 0,
      totalDue: 0, totalPaid: 0, totalRemaining: 0,
    };
  }

  const now     = new Date();
  const payDay  = s.paymentDayOfMonth ?? 1;

  // Only count months whose payment due date has already passed
  const dueRows = s.rows.filter(r => {
    const dueDate = new Date(r.year, r.month - 1, payDay);
    return dueDate <= now;
  });

  const rentalDue        = dueRows.reduce((sum, r) => sum + r.rentalPrice,        0);
  const rentalPaid       = dueRows.reduce((sum, r) => sum + r.rentalPaid,         0);
  const totalFines       = dueRows.reduce((sum, r) => sum + r.totalFines,         0);
  const finesPaid        = dueRows.reduce((sum, r) => sum + r.finesPaid,          0);
  const totalFees        = dueRows.reduce((sum, r) => sum + r.totalEntranceFees,  0);
  const entranceFeesPaid = dueRows.reduce((sum, r) => sum + r.entranceFeesPaid,   0);

  const rentalRemaining = dueRows.reduce((sum, r) => sum + Math.max(0, r.rentalPrice       - r.rentalPaid),       0);
  const finesRemaining  = dueRows.reduce((sum, r) => sum + Math.max(0, r.totalFines        - r.finesPaid),        0);
  const feesRemaining   = dueRows.reduce((sum, r) => sum + Math.max(0, r.totalEntranceFees - r.entranceFeesPaid), 0);

  return {
    rentalDue, rentalPaid,
    finesPaid, totalFines,
    entranceFeesPaid, totalFees,
    totalDue:       rentalDue + totalFines + totalFees,
    totalPaid:      rentalPaid + finesPaid + entranceFeesPaid,
    totalRemaining: rentalRemaining + finesRemaining + feesRemaining,
  };
});
  // ── Lifecycle ─────────────────────────────────────────────────────────────

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const routePlate = params.get('carPlate') ?? params.get('plate');
      const queryPlate = this.route.snapshot.queryParamMap.get('carPlate') ?? this.route.snapshot.queryParamMap.get('plate');
      const plate = routePlate ?? queryPlate ?? '';

      this.carPlate.set(plate);
      this.load();
    });
  }

  load(): void {
    const plate = this.carPlate().trim();
    if (!plate) {
      this.errorMsg.set('Car plate is missing in URL.');
      this.summary.set(null);
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.errorMsg.set('');

    this.carService.getCarPaymentReport(plate)
      .subscribe({
        next: (response: ApiResponse<CarSummaryDto>) => {
          const data = response.data;
          if (!data) {
            this.errorMsg.set(response.message || 'No report data found.');
            this.loading.set(false);
            return;
          }
          this.summary.set(data);
          this.buildYears(data);
          this.loading.set(false);
        },
        error: (err: any) => {
          this.errorMsg.set(err?.error?.message ?? 'Failed to load data.');
          this.loading.set(false);
        },
      });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

private buildYears(summary: CarSummaryDto): void {
  if (!summary.joinDate || !summary.contractExpiry) return;

  const startYear = new Date(summary.joinDate).getFullYear();
  const endYear   = new Date(summary.contractExpiry).getFullYear();

  const years: number[] = [];
  for (let y = startYear; y <= endYear; y++) years.push(y);

  this.yearOptions.set(years.reverse().map(y => ({ label: String(y), value: y })));

  const currentYear = new Date().getFullYear();
  const best = years.find(y => y === currentYear) ?? years[0];
  this.selectedYear.set(best);
}

openPayDialog(row: DisplayRow): void {
  this.payingRow.set(row);
  this.payAmount.set(row.rentalRemaining);   // pre-fill with full remaining
  this.payDialogVisible.set(true);
}

payFromBalance(): void {

  const summary = this.summary();

  if (!summary || (summary.balance ?? 0) <= 0) return;

  // TODO:
  // call your API here

  console.log('Pay from balance clicked');
}

submitRentalPayment(): void {
  const row     = this.payingRow();
  const summary = this.summary();
  if (!row || !summary || this.payAmount() <= 0) return;

  this.paying.set(true);
  this.paymentService.addRentalPayment(summary.clientId, {
    month:  row.month,
    year:   row.year,
    amount: this.payAmount(),
  }).subscribe({
    next: () => {
      this.payDialogVisible.set(false);
      this.paying.set(false);
      this.load();
    },
    error: (err: any) => {
      this.paying.set(false);
    },
  });
}

  onYearChange(value: number): void {
    this.selectedYear.set(value);
  }

  monthName(m: number): string { return MONTHS[m - 1]; }

isFuture(row: DisplayRow): boolean {
  const payDay = this.summary()?.paymentDayOfMonth ?? 1;
  const dueDate = new Date(row.year, row.month - 1, payDay);
  return new Date() < dueDate;
}

paymentDueDate(row: DisplayRow): string {
  const payDay = this.summary()?.paymentDayOfMonth ?? 1;
  return new Date(row.year, row.month - 1, payDay)
    .toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
}


statusLabel(row: DisplayRow): string {
  if (row.totalDue === 0 && row.amountPaid === 0) return 'Clear';
  if (row.amountPaid === 0)  return 'Unpaid';
  if (row.isOverpaid)        return 'Overpaid';
  if (row.isPaid)            return 'Paid';
  return 'Partial';
}

  statusSeverity(row: DisplayRow): 'success' | 'warning' | 'danger' | 'info' {
    if (row.isPaid)     return 'success';
    if (row.isOverpaid) return 'info';
    if (row.isPartial)  return 'warning';
    return 'danger';
  }

  readonly columns = [
    { label: 'Month',         align: 'left',   width: '140px' },
    { label: 'Status',        align: 'center', width: '100px' },
    { label: 'Payment Date',  align: 'right' },
    { label: 'Rental Price',  align: 'right' },
    { label: 'Fines',         align: 'right',  accent: 'text-orange-400/70' },
    { label: 'Entrance Fees', align: 'right',  accent: 'text-purple-400/70' },
    { label: 'Total Due',     align: 'right',  accent: 'text-surface-200' },
    { label: 'Amount Paid',   align: 'right',  accent: 'text-green-400/70' },
    { label: 'Remaining',     align: 'right',  accent: 'text-red-400/70' },
  ];

  fmt(value: number): string {
    return new Intl.NumberFormat('en-AE', {
      style: 'currency', currency: 'AED', minimumFractionDigits: 0,
    }).format(value);
  }

  isContractExpired(): boolean {
  const summary = this.summary();

  if (!summary?.contractExpiry) return false;

  return new Date(summary.contractExpiry) < new Date();
}

contractStatusClass(): string {
  return this.isContractExpired()
    ? 'bg-red-100 text-red-700'
    : 'bg-green-100 text-green-700';
}

contractDateClass(): string {
  return this.isContractExpired()
    ? 'text-red-600'
    : 'text-surface-800';
}
}
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
import { CarService } from 'src/app/core/services/api.services';
import { ApiResponse, CarMonthlyRowDto, CarSummaryDto } from 'src/app/core/models';

interface DisplayRow extends CarMonthlyRowDto {
  totalDue:   number;
  remaining:  number;
  isPaid:     boolean;
  isOverpaid: boolean;
  isPartial:  boolean;
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
  ],
  templateUrl: './car-payment-report.component.html',
  styleUrl:    './car-payment-report.component.css',
})
export class CarPaymentReportComponent implements OnInit {   // ← implements OnInit added
  private carService = inject(CarService);
  private route = inject(ActivatedRoute);
  carPlate = signal('');

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
    if (!s) return [];
  
    const join = s.joinDate ? new Date(s.joinDate) : null;
  
    const filtered = s.rows.filter(r => r.year === this.selectedYear());
  
    return Array.from({ length: 12 }, (_, i) => {
      const month = i + 1;
  
      const raw = filtered.find(r => r.month === month) ?? {
        year: this.selectedYear(),
        month,
        rentalPrice: s.rentalPrice,
        paymentDate: null,
        amountPaid: 0,
        totalFines: 0,
        finesCount: 0,
        totalEntranceFees: 0,
        entranceFeesCount: 0,
      };
  
      // 🚨 FIX HERE: enforce join date rule in frontend
 
      let rent = 0;

      if (join) {
        const rowStart = new Date(raw.year, raw.month - 1, 1);
        const now = new Date();
      
        const isAfterJoin =
          rowStart >= new Date(join.getFullYear(), join.getMonth(), 1);
      
        const isNotFuture =
          raw.year < now.getFullYear() ||
          (raw.year === now.getFullYear() && raw.month <= now.getMonth() + 1);
      
        if (isAfterJoin && isNotFuture) {
          rent = raw.rentalPrice;
        }
      }
      const totalDue = rent + raw.totalFines + raw.totalEntranceFees;
      const remaining = totalDue - raw.amountPaid;
  
      return {
        ...raw,
        rentalPrice: rent,   // 🔥 override so UI is consistent
        totalDue,
        remaining,
        isPaid: remaining <= 0 && raw.amountPaid > 0,
        isOverpaid: remaining < 0,
        isPartial: raw.amountPaid > 0 && remaining > 0,
      };
    });
  });

  totals = computed(() => {
    const rows = this.displayRows();
  
    const amountPaid = rows.reduce((s, r) => s + r.amountPaid, 0);
    const totalFines = rows.reduce((s, r) => s + r.totalFines, 0);
    const totalEntranceFees = rows.reduce((s, r) => s + r.totalEntranceFees, 0);
  
    const totalRent = rows.reduce((s, r) => s + (r.rentalPrice ?? 0), 0);
  
    const totalDue = totalRent + totalFines + totalEntranceFees;
    const remaining = totalDue - amountPaid;
  
    return {
      amountPaid,
      totalFines,
      totalEntranceFees,
      totalDue,
      remaining
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
    const currentYear = new Date().getFullYear();
    const rowYears = summary.rows.map(r => r.year);

    // Keep data years, but also provide a practical recent range for navigation.
    const recentYears = Array.from({ length: 6 }, (_, i) => currentYear - i);
    const years = [...new Set([...rowYears, ...recentYears])].sort((a, b) => b - a);

    this.yearOptions.set(years.map(y => ({ label: String(y), value: y })));
    this.selectedYear.set(rowYears.includes(currentYear) ? currentYear : years[0]);
  }

  onYearChange(value: number): void {
    this.selectedYear.set(value);
  }

  monthName(m: number): string { return MONTHS[m - 1]; }

  isFuture(row: DisplayRow): boolean {
    const now = new Date();
    return row.year > now.getFullYear() ||
      (row.year === now.getFullYear() && row.month > now.getMonth() + 1);
  }

  statusLabel(row: DisplayRow): string {
    const totalDue = row.totalDue;
  
    if (totalDue === 0) return 'Clear';   // ✅ fix
  
    if (row.amountPaid === 0) return 'Unpaid';
    if (row.isOverpaid)       return 'Overpaid';
    if (row.isPaid)           return 'Paid';
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
}
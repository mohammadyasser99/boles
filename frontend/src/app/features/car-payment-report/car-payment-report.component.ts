import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';
import { DropdownModule } from 'primeng/dropdown';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { CalendarModule } from 'primeng/calendar';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { CarService, FineService, PaymentService } from 'src/app/core/services/api.services';
import { ApiResponse, CarMonthlyRowDto, CarSummaryDto } from 'src/app/core/models';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
interface DisplayRow extends CarMonthlyRowDto {
  rentalRemaining: number;
  finesRemaining:  number;
  feesRemaining:   number;
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
    ReactiveFormsModule,
    TableModule,
    TagModule,
    SkeletonModule,
    DropdownModule,
    ButtonModule,
    TooltipModule,
    DialogModule,
    InputNumberModule,
    CalendarModule,
    ToastModule,
  ],
  providers: [MessageService],
  templateUrl: './car-payment-report.component.html',
  styleUrl:    './car-payment-report.component.css',
})
export class CarPaymentReportComponent implements OnInit {
  private carService     = inject(CarService);
  private paymentService = inject(PaymentService);
  private fineService    = inject(FineService);
  private route          = inject(ActivatedRoute);
  private fb             = inject(FormBuilder);
  private toast          = inject(MessageService);
selectedRow: DisplayRow | null = null;

  carPlate = signal('');

  // ── Pay dialog (copied from MonthlyRentalPaymentComponent) ────────────────
  showDialog    = false;
  saving        = signal(false);
  selectedBalance = 0;
  fineOptions   = signal<{ label: string; value: string; amount: number; violationDate: string }[]>([]);
  form!: FormGroup;

  paymentSourceOptions = [
    { label: 'Normal Payment',   value: 'Normal'  },
    { label: 'Pay From Balance', value: 'Balance' },
  ];

  paymentTypeOptions = [
    { label: 'Monthly Rental Payment', value: 1 },
    { label: 'Fines Payment',          value: 2 },
    { label: 'Entrance Fees Payment',  value: 3 },
  ];

  // ── State ─────────────────────────────────────────────────────────────────
  loading      = signal(false);
  errorMsg     = signal('');
  summary      = signal<CarSummaryDto | null>(null);
  yearOptions  = signal<YearOption[]>([]);
  selectedYear = signal<number>(new Date().getFullYear());

  readonly skeletonRows = Array(12).fill({});

  // ── Computed ──────────────────────────────────────────────────────────────
  displayRows = computed<DisplayRow[]>(() => {
    const s = this.summary();
    if (!s || !s.rows?.length) return [];

    const sortedRows = [...s.rows].sort((a, b) => {
      if (a.year !== b.year) return a.year - b.year;
      return a.month - b.month;
    });

    const first = sortedRows[0];
    const last  = sortedRows[sortedRows.length - 1];

    let y = first.year;
    let m = first.month;

    const rows: DisplayRow[] = [];

    while (y < last.year || (y === last.year && m <= last.month)) {
      if (y === this.selectedYear()) {
        const raw = s.rows.find(r => r.year === y && r.month === m) ?? {
          year: y, month: m, paymentDate: null,
          rentalPrice: 0, rentalPaid: 0, finesPaid: 0,
          entranceFeesPaid: 0, amountPaid: 0,
          totalFines: 0, finesCount: 0,
          totalEntranceFees: 0, entranceFeesCount: 0,
        } as CarMonthlyRowDto;

        const rentalRemaining = Math.max(0, raw.rentalPrice - raw.rentalPaid);
        const finesRemaining  = Math.max(0, raw.totalFines - raw.finesPaid);
        const feesRemaining   = Math.max(0, raw.totalEntranceFees - raw.entranceFeesPaid);
        const totalDue        = raw.rentalPrice + raw.totalFines + raw.totalEntranceFees;
        const totalPaid       = raw.rentalPaid + raw.finesPaid + raw.entranceFeesPaid;
        const totalRemaining  = rentalRemaining + finesRemaining + feesRemaining;

        rows.push({
          ...raw, rentalRemaining, finesRemaining, feesRemaining, totalDue, totalRemaining,
          isPaid:     totalDue > 0 && totalRemaining === 0,
          isPartial:  totalPaid > 0 && totalRemaining > 0,
          isOverpaid: totalPaid > totalDue,
        });
      }
      m++;
      if (m > 12) { m = 1; y++; }
    }

    return rows;
  });

  totals = computed(() => {
    const s = this.summary();
    if (!s?.rows?.length) {
      return {
        rentalDue: 0, rentalPaid: 0, finesPaid: 0, totalFines: 0,
        entranceFeesPaid: 0, totalFees: 0, totalDue: 0, totalPaid: 0, totalRemaining: 0,
      };
    }

    const now    = new Date();
    const payDay = s.paymentDayOfMonth ?? 1;

    const dueRows = s.rows.filter(r => new Date(r.year, r.month - 1, payDay) <= now);

    const rentalDue        = dueRows.reduce((sum, r) => sum + r.rentalPrice,       0);
    const rentalPaid       = dueRows.reduce((sum, r) => sum + r.rentalPaid,        0);
    const totalFines       = dueRows.reduce((sum, r) => sum + r.totalFines,        0);
    const finesPaid        = dueRows.reduce((sum, r) => sum + r.finesPaid,         0);
    const totalFees        = dueRows.reduce((sum, r) => sum + r.totalEntranceFees, 0);
    const entranceFeesPaid = dueRows.reduce((sum, r) => sum + r.entranceFeesPaid,  0);

    return {
      rentalDue, rentalPaid, finesPaid, totalFines, entranceFeesPaid, totalFees,
      totalDue:       rentalDue + totalFines + totalFees,
      totalPaid:      rentalPaid + finesPaid + entranceFeesPaid,
      totalRemaining: dueRows.reduce((sum, r) => sum + Math.max(0, r.rentalPrice - r.rentalPaid), 0)
                    + dueRows.reduce((sum, r) => sum + Math.max(0, r.totalFines - r.finesPaid), 0)
                    + dueRows.reduce((sum, r) => sum + Math.max(0, r.totalEntranceFees - r.entranceFeesPaid), 0),
    };
  });

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.initForm();
    this.route.paramMap.subscribe(params => {
      const routePlate = params.get('carPlate') ?? params.get('plate');
      const queryPlate = this.route.snapshot.queryParamMap.get('carPlate') ?? this.route.snapshot.queryParamMap.get('plate');
      this.carPlate.set(routePlate ?? queryPlate ?? '');
      this.load();
    });
  }

  // ── Form (exact copy from MonthlyRentalPaymentComponent) ──────────────────
  initForm(): void {
    this.form = this.fb.group({
      paymentSource:   ['Normal'],
      amount:          [null, Validators.required],
      paidAt:          [null, Validators.required],
      paymentType:     [null, Validators.required],
      violationNumber: [''],
      violationDate:   [''],
    }, {   validators: [
    this.balanceValidator(),
    this.paidMonthValidator()
  ]});

    this.form.get('paymentType')?.valueChanges.subscribe(() => this.loadFines());
this.form.get('paidAt')?.valueChanges.subscribe(() => {
  this.form.updateValueAndValidity();
});
    this.form.get('violationNumber')?.valueChanges.subscribe(v => {
      const fine = this.fineOptions().find(f => f.value === v);
      if (fine) {
        this.form.patchValue({ amount: fine.amount, violationDate: fine.violationDate });
      }
    });
  }

  private paidMonthValidator() {
  return (group: AbstractControl) => {
    const paidAt = group.get('paidAt')?.value;

    if (!paidAt || !this.selectedRow) {
      return null;
    }

    const paidDate = new Date(paidAt);

    const paidMonth = paidDate.getMonth() + 1;
    const paidYear = paidDate.getFullYear();

    if (
      paidMonth !== this.selectedRow.month ||
      paidYear !== this.selectedRow.year
    ) {
      return { invalidPaymentMonth: true };
    }

    return null;
  };
}

  private balanceValidator() {
    return (group: AbstractControl) => {
      const source = group.get('paymentSource')?.value;
      const amount = group.get('amount')?.value;
      if (source === 'Balance' && amount > this.selectedBalance) {
        return { insufficientBalance: true };
      }
      return null;
    };
  }

  loadFines(): void {
    const type     = this.form.get('paymentType')?.value;
    const carPlate = this.carPlate();

    if (type !== 2 || !carPlate) { this.fineOptions.set([]); return; }

    this.fineService.getDebtByPlate(carPlate).subscribe({
      next: res => {
        if (res.success && res.data) {
          this.fineOptions.set(
            res.data.fines.map((f: any) => ({
              label:         `${f.violationNumber} - ${f.amount} AED`,
              value:         f.violationNumber,
              amount:        f.amount,
              violationDate: f.violationDate,
            }))
          );
        }
      },
    });
  }

  // ── Open dialog ───────────────────────────────────────────────────────────
openPayDialog(row: DisplayRow): void {
  this.selectedRow = row;

  this.selectedBalance = this.summary()?.balance ?? 0;

  this.form.reset({
    paymentSource: 'Normal',
    amount: null,
    paidAt: null,
    paymentType: null,
    violationNumber: '',
    violationDate: '',
  });

  this.fineOptions.set([]);
  this.showDialog = true;
}

  // ── Save (exact same logic as MonthlyRentalPaymentComponent) ─────────────
  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }

    const f       = this.form.value;
    const summary = this.summary();
    if (!summary) return;

    if (f.paymentSource === 'Balance' && f.amount > this.selectedBalance) {
      this.toast.add({ severity: 'warn', summary: 'Insufficient Balance', detail: `Amount exceeds available balance of ${this.selectedBalance}` });
      return;
    }

    this.saving.set(true);

    this.paymentService.create({
      amount:          f.amount,
      paidAt:          this.toDateOnly(f.paidAt),
      carPlate:        summary.carPlate,
      userId:          summary.clientId,
      paymentType:     f.paymentType,
      violationNumber: f.paymentType === 2 ? f.violationNumber : null,
      violationDate:   f.paymentType === 2 ? f.violationDate   : null,
      useBalance:      f.paymentSource === 'Balance',
    }).subscribe({
      next: res => {
        this.saving.set(false);
        if (res.success) {
          this.toast.add({ severity: 'success', summary: 'Saved', detail: res.message ?? 'Payment recorded' });
          this.showDialog = false;
          this.load();
        }
      },
      error: err => {
        this.saving.set(false);
        this.toast.add({ severity: 'error', summary: 'Error', detail: err.error?.message });
      },
    });
  }

  // ── Rest of existing methods (unchanged) ──────────────────────────────────
  payFromBalance(): void {
    const summary = this.summary();
    if (!summary || (summary.balance ?? 0) <= 0) return;
    console.log('Pay from balance clicked');
  }

  load(): void {
    const plate = this.carPlate().trim();
    if (!plate) { this.errorMsg.set('Car plate is missing in URL.'); this.summary.set(null); this.loading.set(false); return; }

    this.loading.set(true);
    this.errorMsg.set('');

    this.carService.getCarPaymentReport(plate).subscribe({
      next: (response: ApiResponse<CarSummaryDto>) => {
        const data = response.data;
        if (!data) { this.errorMsg.set(response.message || 'No report data found.'); this.loading.set(false); return; }
        this.summary.set(data);
        this.buildYears(data);
        this.loading.set(false);
      },
      error: (err: any) => { this.errorMsg.set(err?.error?.message ?? 'Failed to load data.'); this.loading.set(false); },
    });
  }

  private buildYears(summary: CarSummaryDto): void {
    if (!summary.joinDate || !summary.contractExpiry) return;
    const startYear = new Date(summary.joinDate).getFullYear();
    const endYear   = new Date(summary.contractExpiry).getFullYear();
    const years: number[] = [];
    for (let y = startYear; y <= endYear; y++) years.push(y);
    this.yearOptions.set(years.reverse().map(y => ({ label: String(y), value: y })));
    const best = years.find(y => y === new Date().getFullYear()) ?? years[0];
    this.selectedYear.set(best);
  }

  onYearChange(value: number): void { this.selectedYear.set(value); }
  monthName(m: number): string { return MONTHS[m - 1]; }

  isFuture(row: DisplayRow): boolean {
    const payDay = this.summary()?.paymentDayOfMonth ?? 1;
    return new Date() < new Date(row.year, row.month - 1, payDay);
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

  fmt(value: number): string {
    return new Intl.NumberFormat('en-AE', { style: 'currency', currency: 'AED', minimumFractionDigits: 0 }).format(value);
  }

  isContractExpired(): boolean {
    const summary = this.summary();
    if (!summary?.contractExpiry) return false;
    return new Date(summary.contractExpiry) < new Date();
  }

  contractStatusClass(): string { return this.isContractExpired() ? 'bg-red-100 text-red-700' : 'bg-green-100 text-green-700'; }
  contractDateClass():   string { return this.isContractExpired() ? 'text-red-600' : 'text-surface-800'; }

  private toDateOnly(date: string | Date): string {
    if (!date) return '';
    if (typeof date === 'string') return date;
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, '0');
    const d = String(date.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }

  downloadPdf(): void {
  const summary = this.summary();
  if (!summary) return;

  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' });
  const pageW = doc.internal.pageSize.getWidth();

  // ── Header bar ────────────────────────────────────────────────────────────
  doc.setFillColor(30, 64, 175);          // brand blue
  doc.rect(0, 0, pageW, 22, 'F');
  doc.setTextColor(255, 255, 255);
  doc.setFontSize(14);
  doc.setFont('helvetica', 'bold');
  doc.text('Car Payment Report', 14, 14);
  doc.setFontSize(10);
  doc.setFont('helvetica', 'normal');
  doc.text(`Generated: ${new Date().toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })}`, pageW - 14, 14, { align: 'right' });

  // ── Car info block ────────────────────────────────────────────────────────
  doc.setTextColor(30, 30, 30);
  doc.setFontSize(16);
  doc.setFont('helvetica', 'bold');
  doc.text(`${(summary.brand ?? '').toUpperCase()} ${(summary.model ?? '').toUpperCase()}`, 14, 32);

  doc.setFontSize(9);
  doc.setFont('helvetica', 'normal');
  doc.setTextColor(80, 80, 80);

  const col1 = 14, col2 = 90, col3 = 170;
  const row1 = 40, row2 = 46, row3 = 52;

  doc.text(`Plate:`,          col1, row1); doc.setFont('helvetica','bold'); doc.text(summary.carPlate ?? '',          col1 + 18, row1); doc.setFont('helvetica','normal');
  doc.text(`Year:`,           col1, row2); doc.setFont('helvetica','bold'); doc.text(String(summary.carYear ?? ''),   col1 + 18, row2); doc.setFont('helvetica','normal');
  doc.text(`Customer:`,       col2, row1); doc.setFont('helvetica','bold'); doc.text(summary.userName ?? '',          col2 + 22, row1); doc.setFont('helvetica','normal');
  doc.text(`Balance:`,        col2, row2); doc.setFont('helvetica','bold'); doc.text(this.fmt(summary.balance ?? 0),  col2 + 22, row2); doc.setFont('helvetica','normal');
  doc.text(`Down Payment:`,   col2, row3); doc.setFont('helvetica','bold'); doc.text(this.fmt(summary.downPayment ?? 0), col2 + 28, row3); doc.setFont('helvetica','normal');
  doc.text(`Join Date:`,      col3, row1); doc.setFont('helvetica','bold'); doc.text(new Date(summary.joinDate).toLocaleDateString('en-GB', { day:'2-digit', month:'short', year:'numeric' }), col3 + 22, row1); doc.setFont('helvetica','normal');
  doc.text(`Contract Expiry:`,col3, row2); doc.setFont('helvetica','bold'); doc.text(new Date(summary.contractExpiry).toLocaleDateString('en-GB', { day:'2-digit', month:'short', year:'numeric' }), col3 + 30, row2); doc.setFont('helvetica','normal');

  const expired = new Date(summary.contractExpiry) < new Date();
  doc.text(`Status:`, col3, row3);
  doc.setFont('helvetica', 'bold');
  doc.setTextColor(expired ? 185 : 22, expired ? 28 : 163, expired ? 28 : 74);
  doc.text(expired ? 'Expired' : 'Active', col3 + 22, row3);
  doc.setTextColor(80, 80, 80);
  doc.setFont('helvetica', 'normal');

  // ── Totals summary row ────────────────────────────────────────────────────
  const t = this.totals();
  const summaryItems = [
    { label: 'Rental Due',   value: this.fmt(t.rentalDue),        color: [30,30,30]    as [number,number,number] },
    { label: 'Rental Paid',  value: this.fmt(t.rentalPaid),       color: [22,163,74]   as [number,number,number] },
    { label: 'Fines Total',  value: this.fmt(t.totalFines),       color: [234,88,12]   as [number,number,number] },
    { label: 'Fines Paid',   value: this.fmt(t.finesPaid),        color: [251,146,60]  as [number,number,number] },
    { label: 'Fees Total',   value: this.fmt(t.totalFees),        color: [126,34,206]  as [number,number,number] },
    { label: 'Fees Paid',    value: this.fmt(t.entranceFeesPaid), color: [167,139,250] as [number,number,number] },
    { label: 'Still Owed',   value: this.fmt(t.totalRemaining),   color: t.totalRemaining > 0 ? [220,38,38] as [number,number,number] : [22,163,74] as [number,number,number] },
  ];

  const boxW = (pageW - 28) / summaryItems.length;
  const boxY = 58;

  summaryItems.forEach((item, i) => {
    const x = 14 + i * boxW;
    doc.setFillColor(248, 248, 252);
    doc.roundedRect(x, boxY, boxW - 2, 16, 2, 2, 'F');
    doc.setFontSize(7);
    doc.setTextColor(120, 120, 120);
    doc.setFont('helvetica', 'normal');
    doc.text(item.label, x + (boxW - 2) / 2, boxY + 5, { align: 'center' });
    doc.setFontSize(9);
    doc.setFont('helvetica', 'bold');
    doc.setTextColor(...item.color);
    doc.text(item.value, x + (boxW - 2) / 2, boxY + 12, { align: 'center' });
  });

  // ── Monthly table ─────────────────────────────────────────────────────────
  const rows = this.summary()!.rows ?? [];

  const tableRows = rows.map(r => {
    const rentalRemaining = Math.max(0, r.rentalPrice - r.rentalPaid);
    const finesRemaining  = Math.max(0, r.totalFines  - r.finesPaid);
    const feesRemaining   = Math.max(0, r.totalEntranceFees - r.entranceFeesPaid);
    const totalRemaining  = rentalRemaining + finesRemaining + feesRemaining;
    const totalPaid       = r.rentalPaid + r.finesPaid + r.entranceFeesPaid;
    const totalDue        = r.rentalPrice + r.totalFines + r.totalEntranceFees;

    const payDay  = summary.paymentDayOfMonth ?? 1;
    const dueDate = new Date(r.year, r.month - 1, payDay);
    const isFuture = new Date() < dueDate;

    let status = 'Unpaid';
    if (totalDue === 0 && totalPaid === 0)           status = 'Clear';
    else if (totalPaid > totalDue)                   status = 'Overpaid';
    else if (totalDue > 0 && totalRemaining === 0)   status = 'Paid';
    else if (totalPaid > 0 && totalRemaining > 0)    status = 'Partial';
    if (isFuture)                                    status = 'Upcoming';

    return [
      `${this.monthName(r.month)} ${r.year}`,
      status,
      r.paymentDate ? new Date(r.paymentDate).toLocaleDateString('en-GB', { day:'2-digit', month:'short', year:'numeric' }) : '—',
      this.fmt(r.rentalPrice),
      r.rentalPaid > 0         ? this.fmt(r.rentalPaid)         : '—',
      r.totalFines > 0         ? this.fmt(r.totalFines)         : '—',
      r.finesPaid > 0          ? this.fmt(r.finesPaid)          : '—',
      r.totalEntranceFees > 0  ? this.fmt(r.totalEntranceFees)  : '—',
      r.entranceFeesPaid > 0   ? this.fmt(r.entranceFeesPaid)   : '—',
      isFuture ? this.fmt(r.rentalPrice) : (totalRemaining > 0 ? this.fmt(totalRemaining) : '✓ Settled'),
      dueDate.toLocaleDateString('en-GB', { day:'2-digit', month:'short', year:'numeric' }),
    ];
  });

  autoTable(doc, {
    startY: 80,
    head: [[
      'Month', 'Status', 'Payment Date',
      'Rental Price', 'Rental Paid',
      'Fines Total', 'Fines Paid',
      'Fees Total', 'Fees Paid',
      'Remaining', 'Due Date',
    ]],
    body: tableRows,
    styles:       { fontSize: 7.5, cellPadding: 2.5 },
    headStyles:   { fillColor: [30, 64, 175], textColor: 255, fontStyle: 'bold', fontSize: 7.5 },
    columnStyles: {
      0:  { fontStyle: 'bold' },
      3:  { halign: 'right' },
      4:  { halign: 'right', textColor: [22, 163, 74] },
      5:  { halign: 'right', textColor: [234, 88, 12] },
      6:  { halign: 'right', textColor: [251, 146, 60] },
      7:  { halign: 'right', textColor: [126, 34, 206] },
      8:  { halign: 'right', textColor: [167, 139, 250] },
      9:  { halign: 'right', fontStyle: 'bold' },
      10: { halign: 'left',  textColor: [100, 100, 100] },
    },
    didParseCell: (data) => {
      if (data.section === 'body') {
        const status = data.row.raw as string[];
        if (status[1] === 'Paid')     data.row.cells[1].styles.textColor = [22, 163, 74];
        if (status[1] === 'Unpaid')   data.row.cells[1].styles.textColor = [220, 38, 38];
        if (status[1] === 'Partial')  data.row.cells[1].styles.textColor = [234, 88, 12];
        if (status[1] === 'Upcoming') data.row.cells[1].styles.textColor = [120, 120, 120];
        if (status[1] === 'Overpaid') data.row.cells[1].styles.textColor = [30, 64, 175];

        const remaining = status[9];
        if (data.column.index === 9 && remaining !== '✓ Settled') {
          data.cell.styles.textColor = [220, 38, 38];
        }
        if (data.column.index === 9 && remaining === '✓ Settled') {
          data.cell.styles.textColor = [22, 163, 74];
        }

        if (status[1] === 'Paid')     data.row.cells[0].styles.fillColor = [240, 253, 244];
        if (status[1] === 'Partial')  data.row.cells[0].styles.fillColor = [255, 251, 235];
        if (status[1] === 'Upcoming') {
          Object.values(data.row.cells).forEach(cell => {
            cell.styles.textColor = [180, 180, 180];
          });
        }
      }
    },
    alternateRowStyles: { fillColor: [250, 250, 253] },
  });

  // ── Footer ────────────────────────────────────────────────────────────────
  const totalPages = (doc as any).internal.getNumberOfPages();
  for (let i = 1; i <= totalPages; i++) {
    doc.setPage(i);
    doc.setFontSize(7);
    doc.setTextColor(160, 160, 160);
    doc.setFont('helvetica', 'normal');
    doc.text(`Page ${i} of ${totalPages}`, pageW - 14, doc.internal.pageSize.getHeight() - 6, { align: 'right' });
    doc.text(`${summary.carPlate} — ${(summary.brand ?? '').toUpperCase()} ${(summary.model ?? '').toUpperCase()}`, 14, doc.internal.pageSize.getHeight() - 6);
  }

  doc.save(`payment-report-${summary.carPlate}-${this.selectedYear()}.pdf`);
}
}
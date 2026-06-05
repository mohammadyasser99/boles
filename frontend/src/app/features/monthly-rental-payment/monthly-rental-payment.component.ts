  import { Component, OnInit, inject, signal } from '@angular/core';
  import { ReactiveFormsModule, FormBuilder, Validators, FormGroup, AbstractControl } from '@angular/forms';
  import { CommonModule } from '@angular/common';
  import { FormsModule } from '@angular/forms';
  import { TableModule } from 'primeng/table';
  import { ButtonModule } from 'primeng/button';
  import { DialogModule } from 'primeng/dialog';
  import { InputNumberModule } from 'primeng/inputnumber';
  import { DropdownModule } from 'primeng/dropdown';
  import { CalendarModule } from 'primeng/calendar';
  import { ToastModule } from 'primeng/toast';
  import { TagModule } from 'primeng/tag';
  import { MessageService } from 'primeng/api';
  import {
    CarService,
    EntranceFeeService,
    FineService,
    PaymentService,
    UserService,
  } from 'src/app/core/services/api.services';
  import { MonthlyRentalPaymentDto } from 'src/app/core/models';

  @Component({
    selector: 'app-monthly-rental-payment',
    standalone: true,
    imports: [
      CommonModule,
      FormsModule,
      ReactiveFormsModule,
      TableModule,
      ButtonModule,
      DialogModule,
      InputNumberModule,
      DropdownModule,
      CalendarModule,
      ToastModule,
      TagModule,
    ],
    providers: [MessageService],
    templateUrl: './monthly-rental-payment.component.html',
    styleUrl: './monthly-rental-payment.component.css',
  })
  export class MonthlyRentalPaymentComponent implements OnInit {
    private paymentService    = inject(PaymentService);
    private userService       = inject(UserService);
    private entranceFeeService = inject(EntranceFeeService);
    private fineService       = inject(FineService);
    private toast             = inject(MessageService);
    private fb                = inject(FormBuilder);

    // ── Table state ────────────────────────────────────────────────────────────
    payments     = signal<MonthlyRentalPaymentDto[]>([]);
    totalRecords = signal(0);
    page         = signal(1);
    rows         = signal(10);
    loading      = signal(false);
showBalanceDialog = false;
balanceForm!: FormGroup;
selectedUserBalance = 0;
savingBalance = signal(false);

operationOptions = [
  { label: 'Add to Balance',      value: 'Add'      },
  { label: 'Subtract from Balance', value: 'Subtract' },
  { label: 'Set Balance to',      value: 'Set'      },
];
    // ── Filters ────────────────────────────────────────────────────────────────
    searchTerm  = '';
    searchBy    = 'carplate';
    paymentType = '';
  selectedBalance = 0;
    searchByOptions = [
      { label: 'Car Plate',   value: 'carplate' },
      { label: 'Client Name', value: 'username' },
    ];
  paymentSourceOptions = [
    { label: 'Normal Payment', value: 'Normal' },
    { label: 'Pay From Balance', value: 'Balance' }
  ];
    /** Used in the table toolbar filter (includes "All Types") */
    paymentTypeFilterOptions = [
      { label: 'All Types',    value: ''            },
      { label: 'Rental',       value: 'Rental'      },
      { label: 'Fine',         value: 'Fine'         },
      { label: 'Entrance Fee', value: 'EntranceFee' },
    ];

    /** Used in the Add/Edit dialog form (no "All Types" option) */
    paymentTypeOptions = [
      { label: 'Monthly Rental Payment', value: 1 },
      { label: 'Fines Payment',          value: 2 },
      { label: 'Entrance Fees Payment',  value: 3 },
    ];

    // ── Dialog / form state ────────────────────────────────────────────────────
    showDialog = false;
    editMode   = signal(false);
    editId     = '';
    saving     = signal(false);
  private allUsers = signal<any[]>([]);
    userOptions = signal<{ label: string; value: string }[]>([]);
    carOptions  = signal<{ label: string; value: string }[]>([]);
    private allCars = signal<{ carPlate: string; userId: string | null }[]>([]);

    feeOptions  = signal<{ label: string; value: string; amount: number }[]>([]);
    fineOptions = signal<{
      label: string;
      value: string;
      amount: number;
      violationDate: string;
    }[]>([]);

    form!: FormGroup;

    // ── Lifecycle ──────────────────────────────────────────────────────────────
    ngOnInit(): void {
      this.initForm();
      this.load();
  this.loadUsersWithCars();
    }

    // ── Form ───────────────────────────────────────────────────────────────────
    initForm(): void {
  this.form = this.fb.group({
    userId: [''],
    carPlate: [''],
    paymentSource: ['Normal'],   // ✅ FIXED
    amount: [null, Validators.required],
    paidAt: [null, Validators.required],
    paymentType: [null, Validators.required],
    violationNumber: [''],
    violationDate: [''],
  },
  { validators: this.balanceValidator() });

  // ── balance form ──
  this.balanceForm = this.fb.group({
    userId:    [null, Validators.required],
    operation: ['Add', Validators.required],
    amount:    [null, [Validators.required, Validators.min(0.01)]],
  });

  this.balanceForm.get('userId')?.valueChanges.subscribe(uid => {
    const user = this.allUsers().find(u => u.userId === uid);
    this.selectedUserBalance = user?.balance ?? 0;
  });
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


  openBalanceDialog(): void {
  this.balanceForm.reset({ userId: null, operation: 'Add', amount: null });
  this.selectedUserBalance = 0;
  this.showBalanceDialog = true;
}

saveBalance(): void {
  if (this.balanceForm.invalid) {
    this.balanceForm.markAllAsTouched();
    return;
  }

  const { userId, amount, operation } = this.balanceForm.value;
  this.savingBalance.set(true);

  this.userService.modifyBalance(userId, amount, operation).subscribe({
    next: res => {
      this.savingBalance.set(false);
      if (res.success) {
        this.toast.add({ severity: 'success', summary: 'Done', detail: res.message });
        this.showBalanceDialog = false;
        // Refresh local user list so balance card reflects new value
        this.loadUsersWithCars();
      }
    },
    error: err => {
      this.savingBalance.set(false);
      this.toast.add({ severity: 'error', summary: 'Error', detail: err.error?.message });
    },
  });
}

  loadUsersWithCars(): void {
    this.userService.getUsersWithCars().subscribe(res => {
      if (res.success && res.data) {
  this.allUsers.set(res.data);
        this.userOptions.set(
          res.data.map((u: any) => ({
            label: u.userName,
            value: u.userId
          }))
        );

        const cars = res.data.flatMap((u: any) =>
          (u.cars ?? []).map((c: any) => ({
            carPlate: c.carPlate,
            userId: u.userId
          }))
        );

        this.allCars.set(cars);

  this.carOptions.set(
    cars.map((c: { carPlate: string; userId: string }) => ({
      label: c.carPlate,
      value: c.carPlate
    }))
  );
      }
    });
  }

    // ── Data loaders ───────────────────────────────────────────────────────────
    load(): void {
      this.loading.set(true);
      this.paymentService
        .getAll(this.page(), this.rows(), this.searchTerm, this.searchBy, this.paymentType)
        .subscribe((res: any) => {
          if (res.success && res.data) {
            this.payments.set(res.data.items);
            this.totalRecords.set(res.data.totalCount);
          }
          this.loading.set(false);
        });
    }



    loadFines(): void {
      const paymentType = this.form.get('paymentType')?.value;
      const carPlate    = this.form.get('carPlate')?.value;

      if (paymentType !== 2 || !carPlate) {
        this.fineOptions.set([]);
        return;
      }

      this.fineService.getDebtByPlate(carPlate).subscribe({
        next: res => {
          if (res.success && res.data) {
            this.fineOptions.set(
              res.data.fines.map(f => ({
                label:         `${f.violationNumber} - ${f.amount} EGP`,
                value:         f.violationNumber,
                amount:        f.amount,
                violationDate: f.violationDate,
              }))
            );
          }
        },
        error: () => {
          this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to load fines' });
        },
      });
    }

    loadEntranceFees(): void {
      const paymentType = this.form.get('paymentType')?.value;
      const carPlate    = this.form.get('carPlate')?.value;

      if (paymentType !== 3 || !carPlate) {
        this.feeOptions.set([]);
        return;
      }

      this.entranceFeeService.getFeesByPlate(carPlate).subscribe({
        next: res => {
          if (res.success && res.data) {
            this.feeOptions.set(
              res.data.fees.map(f => ({
                label:  `${f.tripNumber} - ${f.amount} EGP`,
                value:  f.tripNumber,
                amount: f.amount,
              }))
            );
          }
        },
        error: () => {},
      });
    }

    // ── Dialog openers ─────────────────────────────────────────────────────────
    openCreate(): void {
      this.editMode.set(false);
      this.editId = '';

  this.form.reset({
    userId: '',
    carPlate: '',
    paymentSource: 'Normal',
    amount: null,
    paidAt: null,
    paymentType: null,
    violationNumber: '',
    violationDate: '',
  });

      this.fineOptions.set([]);
      this.feeOptions.set([]);

      this.showDialog = true;

      // Filter cars by selected user
      this.form.get('userId')?.valueChanges.subscribe(selectedUserId => {
        this.form.patchValue({ carPlate: '' }, { emitEvent: false });

        if (!selectedUserId) {
          this.carOptions.set(
            this.allCars().map(c => ({ label: c.carPlate, value: c.carPlate }))
          );
        } else {
          this.carOptions.set(
            this.allCars()
              .filter(c => c.userId === selectedUserId)
              .map(c => ({ label: c.carPlate, value: c.carPlate }))
          );
        }

  const user = this.allUsers().find(u => u.userId === selectedUserId);
  this.selectedBalance = user?.balance ?? 0;
  this.form.updateValueAndValidity(); 
      });

      this.form.get('paymentType')?.valueChanges.subscribe(() => {
        this.loadFines();
        this.loadEntranceFees();
      });

      this.form.get('carPlate')?.valueChanges.subscribe(() => {
        this.loadFines();
        this.loadEntranceFees();
      });

      // Auto-fill amount when a fine is selected
      this.form.get('violationNumber')?.valueChanges.subscribe(v => {
        const fine = this.fineOptions().find(f => f.value === v);
        if (fine) {
          this.form.patchValue({ amount: fine.amount, violationDate: fine.violationDate });
        }
      });


    }

    openEdit(payment: MonthlyRentalPaymentDto): void {
      this.editMode.set(true);
      this.editId = payment.id;

      this.form.patchValue({
        userId:      payment.userId,
        carPlate:    payment.carPlate,
        amount:      payment.amount,
        paidAt:      payment.paidAt
          ? payment.paidAt.toString().split('T')[0]
          : null,
      });

      this.showDialog = true;
    }

    // ── Save ───────────────────────────────────────────────────────────────────
  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const formValue = this.form.value;

    if (!this.editMode() && (!formValue.userId || !formValue.carPlate)) {
      this.toast.add({ severity: 'warn', summary: 'Required', detail: 'User and Car are required.' });
      return;
    }

    // Guard: balance payment cannot exceed available balance
    if (formValue.paymentSource === 'Balance' && formValue.amount > this.selectedBalance) {
      this.toast.add({
        severity: 'warn',
        summary: 'Insufficient Balance',
        detail: `Amount exceeds available balance of ${this.selectedBalance}`
      });
      return;
    }

    const paidAt = this.toDateOnly(formValue.paidAt);
    this.saving.set(true);

    const request$ = this.editMode()
      ? this.paymentService.update(this.editId, { amount: formValue.amount, paidAt })
      : this.paymentService.create({
          amount:          formValue.amount,
          paidAt,
          carPlate:        formValue.carPlate,
          userId:          formValue.userId,
          paymentType:     formValue.paymentType,
          violationNumber: formValue.paymentType === 2 ? formValue.violationNumber : null,
          violationDate:   formValue.paymentType === 2 ? formValue.violationDate   : null,
          useBalance:      formValue.paymentSource === 'Balance',  // ← new
        });

    request$.subscribe({
      next: res => {
        this.saving.set(false);
        if (res.success) {
          this.toast.add({
            severity: 'success',
            summary:  'Saved',
            detail:   res.message ?? (this.editMode() ? 'Payment updated' : 'Payment created'),
          });
          this.showDialog = false;
          this.load();
        }
      },
      error: err => {
        this.saving.set(false);
        this.toast.add({ severity: 'error', summary: 'Error', detail: err.error.message });
      },
    });
  }

    // ── Pagination / search ────────────────────────────────────────────────────
    search(): void {
      this.page.set(1);
      this.load();
    }

    clear(): void {
      this.searchTerm  = '';
      this.searchBy    = 'carplate';
      this.paymentType = '';
      this.page.set(1);
      this.load();
    }

    onPageChange(event: any): void {
      this.page.set(event.first / event.rows + 1);
      this.rows.set(event.rows);
      this.load();
    }

    // ── Display helpers ────────────────────────────────────────────────────────
    paymentTypeSeverity(type: number): 'success' | 'warning' | 'danger' | 'info' {
      switch (type) {
        case 1:  return 'success';
        case 2:  return 'danger';
        case 3:  return 'warning';
        default: return 'info';
      }
    }

    getPaymentTypeLabel(type: number): string {
      switch (type) {
        case 1:  return 'Monthly Rental';
        case 2:  return 'Fines';
        case 3:  return 'Entrance Fees';
        default: return 'Unknown';
      }
    }

    fmt(v: number): string {
      return new Intl.NumberFormat('en-AE', {
        style: 'currency', currency: 'AED', minimumFractionDigits: 0,
      }).format(v);
    }

    private toDateOnly(date: string | Date): string {
      if (!date) return '';
      if (typeof date === 'string') return date;

      const y = date.getFullYear();
      const m = String(date.getMonth() + 1).padStart(2, '0');
      const d = String(date.getDate()).padStart(2, '0');
      return `${y}-${m}-${d}`;
    }
  }
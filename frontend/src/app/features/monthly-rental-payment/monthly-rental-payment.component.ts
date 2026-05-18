import { Component, OnInit, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { DropdownModule } from 'primeng/dropdown';
import { CalendarModule } from 'primeng/calendar';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { CarService, EntranceFeeService, FineService, PaymentService, UserService } from 'src/app/core/services/api.services';
import { MonthlyRentalPaymentDto } from 'src/app/core/models';

@Component({
  selector: 'app-monthly-rental-payment',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    DialogModule,
    InputNumberModule,
    DropdownModule,
    CalendarModule,
    ToastModule ,
    ReactiveFormsModule
  ],
  providers: [MessageService],
  templateUrl: './monthly-rental-payment.component.html',
  styleUrl: './monthly-rental-payment.component.css'
})
export class MonthlyRentalPaymentComponent implements OnInit {
  private paymentService = inject(PaymentService);
  private userService = inject(UserService);
  private carService = inject(CarService);
  private toast = inject(MessageService);
private entranceFeeService = inject(EntranceFeeService);

private fineService = inject(FineService);
  paymentTypeOptions = [
    { label: 'Monthly Rental Payment', value: 1 },
    { label: 'Fines Payment', value: 2 },
    { label: 'Entrance Fees Payment', value: 3 }
  ];
feeOptions = signal<{ label: string; value: string; amount: number }[]>([]);
  fineOptions = signal<{
  label: string;
  value: string;
  amount: number;
  violationDate: string;
}[]>([]);

selectedFineAmount = signal<number>(0);
  payments = signal<MonthlyRentalPaymentDto[]>([]);
  loading = signal(false);
  saving = signal(false);

  showDialog = false;
  editMode = signal(false);
  editId = '';

  userOptions = signal<{ label: string; value: string }[]>([]);
  carOptions = signal<{ label: string; value: string }[]>([]);
private allCars = signal<{ carPlate: string; userId: string | null }[]>([]);

  private fb = inject(FormBuilder);

  form!: FormGroup;

  ngOnInit(): void {
    this.initForm();
    this.loadPayments();
    this.loadUsers();
    this.loadCars();
  }
initForm() {
this.form = this.fb.group({
  userId: [''],
  carPlate: [''],
  amount: [null, Validators.required],
  paidAt: [null, Validators.required],
  paymentType: [null, Validators.required],
  violationNumber: [''],
  violationDate: [''],
  tripNumber: ['']
});
}

  loadPayments(): void {
    this.loading.set(true);
    this.paymentService.getAll().subscribe({
      next: res => {
        this.payments.set(res.data ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to load payments.' });
      }
    });
  }

  loadUsers(): void {
    this.userService.getAll().subscribe(res => {
      if (res.success && res.data) {
        this.userOptions.set(res.data.map(u => ({ label: `${u.name} (${u.email})`, value: u.id })));
      }
    });
  }

  loadCars(): void {
    this.carService.getAll().subscribe(res => {
      if (res.success && res.data) {
        // Store full car data including userId
        this.allCars.set(res.data.map(c => ({ carPlate: c.carPlate, userId: c.userId ?? null })));
        // Initially show all cars
        this.carOptions.set(res.data.map(c => ({ label: c.carPlate, value: c.carPlate })));
      }
    });
  }


  loadFines(): void {
  const paymentType = this.form.get('paymentType')?.value;
  const carPlate = this.form.get('carPlate')?.value;

  // Only load fines when payment type = fines
  if (paymentType !== 2 || !carPlate) {
    this.fineOptions.set([]);
    return;
  }

  this.fineService.getDebtByPlate(carPlate).subscribe({
    next: res => {
      if (res.success && res.data) {
const fines = res.data.fines.map(f => ({
  label: `${f.violationNumber} - ${f.amount} EGP`,
  value: f.violationNumber,
  amount: f.amount,
  violationDate: f.violationDate
}));

        this.fineOptions.set(fines);
      }
    },
    error: () => {
      this.toast.add({
        severity: 'error',
        summary: 'Error',
        detail: 'Failed to load fines'
      });
    }
  });
}

openCreate(): void {
  this.editMode.set(false);
  this.editId = '';

this.form.reset({
  userId: '',
  carPlate: '',
  amount: null,
  paidAt: null,
  paymentType: null,
  violationNumber: '',
  tripNumber: ''
});

  this.showDialog = true;

  // Filter cars by user
  this.form.get('userId')?.valueChanges.subscribe(selectedUserId => {
    this.form.patchValue({ carPlate: '' }, { emitEvent: false });

    if (!selectedUserId) {
      this.carOptions.set(
        this.allCars().map(c => ({
          label: c.carPlate,
          value: c.carPlate
        }))
      );
    } else {
      const filtered = this.allCars()
        .filter(c => c.userId === selectedUserId)
        .map(c => ({
          label: c.carPlate,
          value: c.carPlate
        }));

      this.carOptions.set(filtered);
    }
  });

this.form.get('paymentType')?.valueChanges.subscribe(() => {
  this.loadFines();
  this.loadEntranceFees();
});

this.form.get('carPlate')?.valueChanges.subscribe(() => {
  this.loadFines();
  this.loadEntranceFees();
});

this.form.get('tripNumber')?.valueChanges.subscribe(v => {
  const fee = this.feeOptions().find(f => f.value === v);

  if (fee) {
    this.form.patchValue({
      amount: fee.amount
    });
  }
});

  // Auto fill amount when fine selected
this.form.get('violationNumber')?.valueChanges.subscribe(v => {
  const fine = this.fineOptions().find(f => f.value === v);

  if (fine) {
    this.form.patchValue({
      amount: fine.amount,
      violationDate: fine.violationDate
    });
  }
});
}
  openEdit(payment: MonthlyRentalPaymentDto): void {
    this.editMode.set(true);
    this.editId = payment.id;
  
    this.form.patchValue({
      userId: payment.userId,
      carPlate: payment.carPlate,
      amount: payment.amount,
      paidAt: payment.paidAt
      ? payment.paidAt.toString().split('T')[0]
      : null
        });
  
    this.showDialog = true;
  }

loadEntranceFees(): void {
  const paymentType = this.form.get('paymentType')?.value;
  const carPlate = this.form.get('carPlate')?.value;

  // Entrance Fees Payment
  if (paymentType !== 3 || !carPlate) {
    this.feeOptions.set([]);
    return;
  }

  this.entranceFeeService.getFeesByPlate(carPlate).subscribe({
    next: res => {
      if (res.success && res.data) {
        const fees = res.data.fees.map(f => ({
          label: `${f.tripNumber} - ${f.amount} EGP`,
          value: f.tripNumber,
          amount: f.amount
        }));

        this.feeOptions.set(fees);
      }
    },
    error: () => {

    }
  });
}
save(): void {
  if (this.form.invalid) {
    this.form.markAllAsTouched();
    return;
  }

  const formValue = this.form.value;

  if (!this.editMode() && (!formValue.userId || !formValue.carPlate)) {
    this.toast.add({
      severity: 'warn',
      summary: 'Required',
      detail: 'User and Car are required.'
    });
    return;
  }

  const paidAt = this.toDateOnly(formValue.paidAt);
  this.saving.set(true);

  const request$ = this.editMode()
    ? this.paymentService.update(this.editId, {
        amount: formValue.amount,
        paidAt
      })
  : this.paymentService.create({
    amount: formValue.amount,
    paidAt,
    carPlate: formValue.carPlate,
    userId: formValue.userId,
    paymentType: formValue.paymentType,

    violationNumber:
      formValue.paymentType === 2
        ? formValue.violationNumber
        : null,

    violationDate:
      formValue.paymentType === 2
        ? formValue.violationDate
        : null,

    tripNumber:
      formValue.paymentType === 3
        ? formValue.tripNumber
        : null
  });

  request$.subscribe({
    next: res => {
      this.saving.set(false);

      if (res.success) {
        this.toast.add({
          severity: 'success',
          summary: 'Saved',
          detail: this.editMode() ? 'Payment updated' : 'Payment created'
        });

        this.showDialog = false;
        this.loadPayments();
      }
    },
    error: (err) => {
      console.log(err);
      
      this.saving.set(false);
      this.toast.add({
        severity: 'error',
        summary: 'Error',
        detail: err.error.message
      });
    }
  });
}

private toDateOnly(date: string | Date): string {
  if (!date) return '';

  if (typeof date === 'string') {
    return date;
  }

  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');

  return `${y}-${m}-${d}`;
}
}

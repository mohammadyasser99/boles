import { Component, OnInit, inject, signal } from '@angular/core';
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
import { CarService, PaymentService, UserService } from 'src/app/core/services/api.services';
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
    ToastModule
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

  payments = signal<MonthlyRentalPaymentDto[]>([]);
  loading = signal(false);
  saving = signal(false);

  showDialog = false;
  editMode = signal(false);
  editId = '';

  userOptions = signal<{ label: string; value: string }[]>([]);
  carOptions = signal<{ label: string; value: string }[]>([]);

  form: { userId: string; carPlate: string; amount: number | null; paidAt: Date | null } = {
    userId: '',
    carPlate: '',
    amount: null,
    paidAt: null,
  };

  ngOnInit(): void {
    this.loadPayments();
    this.loadUsers();
    this.loadCars();
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
        this.carOptions.set(res.data.map(c => ({ label: c.carPlate, value: c.carPlate })));
      }
    });
  }

  openCreate(): void {
    this.editMode.set(false);
    this.editId = '';
    this.form = { userId: '', carPlate: '', amount: null, paidAt: null };
    this.showDialog = true;
  }

  openEdit(payment: MonthlyRentalPaymentDto): void {
    this.editMode.set(true);
    this.editId = payment.id;
    this.form = {
      userId: payment.userId,
      carPlate: payment.carPlate,
      amount: payment.amount,
      paidAt: payment.paidAt ? new Date(payment.paidAt) : null
    };
    this.showDialog = true;
  }

  save(): void {
    if (!this.form.amount || !this.form.paidAt) {
      this.toast.add({ severity: 'warn', summary: 'Required', detail: 'Amount and Paid Date are required.' });
      return;
    }

    if (!this.editMode() && (!this.form.userId || !this.form.carPlate)) {
      this.toast.add({ severity: 'warn', summary: 'Required', detail: 'User and Car are required for new payment.' });
      return;
    }

    const paidAt = this.toDateOnly(this.form.paidAt);
    this.saving.set(true);

    const request$ = this.editMode()
      ? this.paymentService.update(this.editId, { amount: this.form.amount, paidAt })
      : this.paymentService.create({
          amount: this.form.amount,
          paidAt,
          carPlate: this.form.carPlate,
          userId: this.form.userId
        });

    request$.subscribe({
      next: res => {
        this.saving.set(false);
        if (res.success) {
          this.toast.add({
            severity: 'success',
            summary: 'Saved',
            detail: this.editMode() ? 'Payment updated successfully.' : 'Payment created successfully.'
          });
          this.showDialog = false;
          this.loadPayments();
        } else {
          this.toast.add({ severity: 'error', summary: 'Error', detail: res.message || 'Operation failed.' });
        }
      },
      error: err => {
        this.saving.set(false);
        this.toast.add({ severity: 'error', summary: 'Error', detail: err.error?.message ?? 'Operation failed.' });
      }
    });
  }

  private toDateOnly(date: Date): string {
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, '0');
    const d = String(date.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }
}

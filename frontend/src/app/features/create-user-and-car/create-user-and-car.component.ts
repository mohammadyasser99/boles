// create-user-and-car.component.ts
import { Component, inject, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Subject, takeUntil, combineLatest } from 'rxjs';

import { InputTextModule } from 'primeng/inputtext';
import { DropdownModule }   from 'primeng/dropdown';
import { ButtonModule }     from 'primeng/button';
import { ToastModule }      from 'primeng/toast';
import { CalendarModule }   from 'primeng/calendar';
import { TableModule }      from 'primeng/table';
import { MessageService }   from 'primeng/api';
import { UserService }      from 'src/app/core/services/api.services';

type DocumentItem = {
  id?:       string;
  file?:     File | null;
  fileName?: string;
  type:      string;
  isExisting: boolean;
};

// mirrors PaymentScheduleItem on the backend
export interface PaymentRow {
  month:      number;
  year:       number;
  monthLabel: string;   // e.g. "January 2025"
  amount:     number;
  isPaid:     boolean;
  paidAt?:    string | null;
}

const MONTH_NAMES = [
  'January','February','March','April','May','June',
  'July','August','September','October','November','December'
];

@Component({
  selector: 'app-create-user-and-car',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule,
    InputTextModule, DropdownModule, ButtonModule,
    ToastModule, CalendarModule, TableModule
  ],
  providers: [MessageService],
  templateUrl: './create-user-and-car.component.html',
  styleUrl:    './create-user-and-car.component.css'
})
export class CreateUserAndCarComponent implements OnDestroy {

  form!:     FormGroup;
  documents: DocumentItem[] = [];
  paymentRows: PaymentRow[] = [];      // ✅ NEW
  removedDocumentIds: string[] = [];
  isEditMode = false;

  private fb          = inject(FormBuilder);
  private userService = inject(UserService);
  private toast       = inject(MessageService);
  private route       = inject(ActivatedRoute);
  private destroy$    = new Subject<void>();

  constructor() {
    this.initForm();
    this.watchDatesForSchedule();   // ✅ NEW

    const userId =
      this.route.snapshot.paramMap.get('userId')   ??
      this.route.snapshot.paramMap.get('userid')   ??
      this.route.snapshot.queryParamMap.get('userId') ??
      this.route.snapshot.queryParamMap.get('userid');

    if (userId) {
      this.isEditMode = true;
      this.loadUserWithCar(userId);
    }
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ─────────────────────────────────────────────────────────────────────
  initForm() {
    this.form = this.fb.group({
      name:           ['', Validators.required],
      phoneNumber:    ['', Validators.required],
      email:          ['', [Validators.required, Validators.email]],
      nationalId:     ['', Validators.required],
      dateOfPayment:  [null],
      joinDate:       [null, Validators.required],
      contractExpiry: [null, Validators.required],
      carPlate:       ['', Validators.required],
      brand:          [''],
      modelName:      [''],
      year:           [null],
      // ❌ rentalPrice removed
      chassisNumber:  [''],
      userId:         [null]
    });
  }

  // ── Watch both dates and regenerate schedule ─────────────────────────
  private watchDatesForSchedule() {
    combineLatest([
      this.form.get('joinDate')!.valueChanges,
      this.form.get('contractExpiry')!.valueChanges
    ])
    .pipe(takeUntil(this.destroy$))
    .subscribe(([join, expiry]) => {
      this.generatePaymentRows(join, expiry);
    });
  }

  generatePaymentRows(joinRaw: string | null, expiryRaw: string | null) {
    if (!joinRaw || !expiryRaw) {
      this.paymentRows = [];
      return;
    }

    const join   = new Date(joinRaw);
    const expiry = new Date(expiryRaw);

    if (isNaN(join.getTime()) || isNaN(expiry.getTime()) || join > expiry) {
      this.paymentRows = [];
      return;
    }

    // Preserve existing amounts when re-generating (e.g. user fixes a date)
    const existingAmounts = new Map(
      this.paymentRows.map(r => [`${r.year}-${r.month}`, r.amount])
    );

    const rows: PaymentRow[] = [];
    const cur = new Date(join.getFullYear(), join.getMonth(), 1);
    const end = new Date(expiry.getFullYear(), expiry.getMonth(), 1);

    while (cur <= end) {
      const m = cur.getMonth() + 1;
      const y = cur.getFullYear();
      rows.push({
        month:      m,
        year:       y,
        monthLabel: `${MONTH_NAMES[m - 1]} ${y}`,
        amount:     existingAmounts.get(`${y}-${m}`) ?? 0,
        isPaid:     false
      });
      cur.setMonth(cur.getMonth() + 1);
    }

    this.paymentRows = rows;
  }

  // ─────────────────────────────────────────────────────────────────────
  addDocument() {
    this.documents.push({ file: null, type: '', isExisting: false });
  }

  removeDocument(index: number) {
    const doc = this.documents[index];
    if (doc.isExisting && doc.id) this.removedDocumentIds.push(doc.id);
    this.documents.splice(index, 1);
  }

  onFileSelected(event: any, index: number) {
    this.documents[index].file = event.target.files?.[0] ?? null;
  }

  // ─────────────────────────────────────────────────────────────────────
private loadUserWithCar(userId: string) {
  this.userService.getUserWithCar(userId).subscribe({
    next: (res) => {
      const data = res?.data;
      if (!data) return;

   this.form.patchValue({
  name: data.name ?? '',
  phoneNumber: data.phoneNumber ?? '',
  email: data.email ?? '',
  nationalId: data.nationalId ?? '',

  dateOfPayment: data.dateOfPayment ? new Date(data.dateOfPayment) : null,
  joinDate: data.joinDate ? new Date(data.joinDate) : null,
  contractExpiry: data.contractExpiry ? new Date(data.contractExpiry) : null,

  carPlate: data.car?.carPlate ?? '',
  brand: data.car?.brand ?? '',
  modelName: data.car?.model ?? '',
  year: data.car?.year ?? null,
  chassisNumber: data.car?.chassisNumber ?? '',
  userId: data.id ?? userId
});

      // ── Restore payment schedule from JSON string ──────────────────
      if (data.paymentScheduleJson) {
        try {
const parsed: any[] = JSON.parse(data.paymentScheduleJson);

this.paymentRows = parsed.map(p => ({
  month:      p.Month,
  year:       p.Year,
  monthLabel: `${MONTH_NAMES[p.Month - 1]} ${p.Year}`,
  amount:     p.rentalPrice ?? 0,
  isPaid:     p.IsPaid ?? false,
  paidAt:     p.PaidAt ?? null
}));
        } catch {
          // Malformed JSON — fall back to generating from dates
          this.generatePaymentRows(
            data.joinDate?.toString().split('T')[0] ?? null,
            data.contractExpiry?.toString().split('T')[0] ?? null
          );
        }
      } else {
        // No schedule saved yet — generate empty rows from the date range
        this.generatePaymentRows(
          data.joinDate?.toString().split('T')[0] ?? null,
          data.contractExpiry?.toString().split('T')[0] ?? null
        );
      }

      // ── Documents ──────────────────────────────────────────────────
      this.documents = (data.documents ?? []).map((d: any) => ({
        id:         d.id,
        file:       null,
        fileName:   d.fileName,
        type:       d.documentType,
        isExisting: true
      }));
    }
  });
}

  // ─────────────────────────────────────────────────────────────────────
  downloadDocument(documentId: string) {
    this.userService.downloadDocument(documentId).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = 'document'; a.click();
        window.URL.revokeObjectURL(url);
      },
      error: () => this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to download document' })
    });
  }

  // ─────────────────────────────────────────────────────────────────────
  submit() {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }

    const v = this.form.value;
    const formData = new FormData();

    formData.append('name',        v.name);
    formData.append('phoneNumber', v.phoneNumber);
    formData.append('email',       v.email);
    formData.append('nationalId',  v.nationalId);
    formData.append('carPlate',    v.carPlate);
    formData.append('brand',       v.brand        || '');
    formData.append('model',       v.modelName    || '');
    formData.append('year',        v.year         ?? '');
    formData.append('chassisNumber', v.chassisNumber || '');
    // ❌ rentalPrice removed

    if (v.userId)         formData.append('userId',         v.userId);
    if (v.dateOfPayment)  formData.append('dateOfPayment',  v.dateOfPayment);
    if (v.joinDate)       formData.append('joinDate',       v.joinDate);
    if (v.contractExpiry) formData.append('contractExpiry', v.contractExpiry);

    // ✅ NEW — send one amount per payment row, in order
    for (const row of this.paymentRows) {
      formData.append('monthlyAmounts', String(row.amount ?? 0));
    }

    // documents
    for (const doc of this.documents) {
      if (doc.file && doc.type) {
        formData.append('documentFiles', doc.file);
        formData.append('documentTypes', doc.type);
      }
    }
    const keptIds = this.documents
      .filter(d => d.isExisting && d.id && !this.removedDocumentIds.includes(d.id))
      .map(d => d.id!);
    for (const id of keptIds) formData.append('existingDocumentIds', id);

    const request$ = this.isEditMode
      ? this.userService.updateCarAndUser(formData)
      : this.userService.createwithcar(formData);

    request$.subscribe({
      next: (res: any) => {
        this.toast.add({ severity: 'success', summary: 'Success', detail: res?.message || 'Success' });
        if (!this.isEditMode) this.reset();
      },
      error: (err) => {
        if (err.status === 200) {
          this.toast.add({ severity: 'success', summary: 'Success', detail: 'Updated successfully' });
          return;
        }
        this.toast.add({ severity: 'error', summary: 'Error', detail: err.error?.message || 'Operation failed' });
      }
    });
  }

  reset() {
    this.form.reset({ name:'', phoneNumber:'', email:'', nationalId:'',
      dateOfPayment:null, joinDate:null, contractExpiry:null,
      carPlate:'', brand:'', modelName:'', year:null, chassisNumber:'', userId:null });
    this.paymentRows = [];
    this.documents = [];
  }
}
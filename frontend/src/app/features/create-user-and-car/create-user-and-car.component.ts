import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
// PrimeNG
import { InputTextModule } from 'primeng/inputtext';
import { DropdownModule } from 'primeng/dropdown';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { CalendarModule } from 'primeng/calendar';
import { MessageService } from 'primeng/api';
import { UserService } from 'src/app/core/services/api.services';
type DocumentItem = {
  id?: string;        // existing doc id (from backend)
  file?: File | null; // new upload
  fileName?: string;  // existing file name
  type: string;
  isExisting: boolean;
};
@Component({
  selector: 'app-create-user-and-car',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, InputTextModule, ButtonModule, ToastModule, CalendarModule,DropdownModule],
  providers: [MessageService],
  templateUrl: './create-user-and-car.component.html',
  styleUrl: './create-user-and-car.component.css'
})
export class CreateUserAndCarComponent {
  removedDocumentIds: string[] = [];
  documents: DocumentItem[] = [];
  isEditMode = false;
  private fb = inject(FormBuilder);

  form!: FormGroup;
    private userService = inject(UserService);
    private toast = inject(MessageService);
    private route = inject(ActivatedRoute);

  constructor() {
    this.initForm();
    const routeUserId = this.route.snapshot.paramMap.get('userId') ?? this.route.snapshot.paramMap.get('userid');
    const queryUserId = this.route.snapshot.queryParamMap.get('userId') ?? this.route.snapshot.queryParamMap.get('userid');
    const userId = routeUserId ?? queryUserId;

    if (userId) {
      this.isEditMode = true;
      this.loadUserWithCar(userId);
    }
  }

  initForm() {
    this.form = this.fb.group({
      name: ['', Validators.required],
      phoneNumber: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      nationalId: ['', Validators.required],
      dateOfPayment: [null],
      joinDate: [null, Validators.required],
      carPlate: ['', Validators.required],
      brand: [''],
      modelName: [''],
      year: [null],
      rentalPrice: [null],
      chassisNumber: [''],
      userId: [null]
    });
  }

  
  addDocument(): void {
    this.documents.push({
      file: null,
      type: '',
      isExisting: false
    });
  }
  
  removeDocument(index: number): void {
    const doc = this.documents[index];
  
    // if it's existing → mark for deletion
    if (doc.isExisting && doc.id) {
      this.removedDocumentIds.push(doc.id);
    }
  
    this.documents.splice(index, 1);
  }
  
  onFileSelected(event: any, index: number): void {
    const file = event.target.files?.[0] ?? null;
    this.documents[index].file = file;
  }
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
        
          carPlate: data.car?.carPlate ?? '',
          brand: data.car?.brand ?? '',
          modelName: data.car?.model ?? '',
          year: data.car?.year ?? null,
          rentalPrice: data.car?.rentalPrice ?? null,
          chassisNumber: data.car?.chassisNumber ?? '',
        
          userId: data.id ?? userId
        });
  
        // ✅ ADD THIS
        this.documents = (data.documents ?? []).map((d: any) => ({
          id: d.id,
          file: null,
          fileName: d.fileName,
          type: d.documentType,
          isExisting: true
        }));
            }
    });
  }

  downloadDocument(documentId: string) {
    this.userService.downloadDocument(documentId).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
  
        a.href = url;
        a.download = 'document';
        a.click();
  
        window.URL.revokeObjectURL(url);
      },
      error: () => {
        this.toast.add({
          severity: 'error',
          summary: 'Error',
          detail: 'Failed to download document'
        });
      }
    });
  }
  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
  
    const formValue = this.form.value;
  
    const formData = new FormData();

    formData.append('name', formValue.name);
    formData.append('phoneNumber', formValue.phoneNumber);
    formData.append('email', formValue.email);
    formData.append('nationalId', formValue.nationalId);
    
    formData.append('carPlate', formValue.carPlate);
    formData.append('brand', formValue.brand || '');
    formData.append('model', formValue.modelName || '');
    formData.append('year', formValue.year ?? '');
    formData.append('rentalPrice', formValue.rentalPrice ?? 0);
    formData.append('chassisNumber', formValue.chassisNumber || '');
    
    if (formValue.userId) {
      formData.append('userId', formValue.userId);
    }
    
    if (formValue.dateOfPayment) {
      formData.append(
        'dateOfPayment',
        formValue.dateOfPayment.toISOString().split('T')[0]
      );
    }
    
    if (formValue.joinDate) {
      formData.append(
        'joinDate',
        formValue.joinDate.toISOString().split('T')[0]
      );
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
  
  for (const id of keptIds) {
    formData.append('existingDocumentIds', id);
  }
    const request$ = this.isEditMode
      ? this.userService.updateCarAndUser(formData)
      : this.userService.createwithcar(formData);
  
    request$.subscribe({
      next: (res: any) => {
        this.toast.add({
          severity: 'success',
          summary: 'Success',
          detail: res?.message || 'Success'
        });
  
        if (!this.isEditMode) this.reset();
      },
      error: (err) => {
        if (err.status === 200) {
          this.toast.add({
            severity: 'success',
            summary: 'Success',
            detail: 'Updated successfully'
          });
          return;
        }
  
        this.toast.add({
          severity: 'error',
          summary: 'Error',
          detail: err.error?.message || 'Operation failed'
        });
      }
    });
  }

  reset() {
    this.form.reset({
      name: '',
      phoneNumber: '',
      email: '',
      nationalId: '',
      dateOfPayment: null,
      joinDate: null,
      carPlate: '',
      brand: '',
      modelName: '',
      year: null,
      rentalPrice: null,
      chassisNumber: '',
      userId: null
    });
  }
}

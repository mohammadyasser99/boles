import { Component, inject, OnInit, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UserService } from '../../core/services/api.services';
import { User } from '../../core/models';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MessageService, ConfirmationService } from 'primeng/api';
import { AvatarModule } from 'primeng/avatar';
import { TooltipModule } from 'primeng/tooltip';
import { DropdownModule } from 'primeng/dropdown';
import { CalendarModule } from 'primeng/calendar';
import { Router } from '@angular/router';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    ToastModule,
    ConfirmDialogModule,
    AvatarModule,
    TooltipModule,
    DropdownModule,
    CalendarModule,
    ReactiveFormsModule
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './users.component.html',
  styleUrl: './users.component.css'
})
export class UsersComponent implements OnInit {
  private userService = inject(UserService);
  private toast = inject(MessageService);
  private confirm = inject(ConfirmationService);
  private router = inject(Router);
  existingDocument: any = null;
  downloading = signal(false);
  users = signal<User[]>([]);
  loading = signal(true);
  saving = signal(false);

  showDialog = false;
  editMode = signal(false);
  editId = '';

  private fb = inject(FormBuilder);

  userForm!: FormGroup;

  documents: {
    id?: string;
    file: File | null;
    fileName?: string;
    type: string;
    existing?: boolean;
  }[] = [];

  ngOnInit(): void {
    this.initForm();
    this.loadUsers();
  }
  addDocument(): void {
    this.documents.push({
      file: null,
      type: ''
    });
  }
  
  removeDocument(index: number): void {
    this.documents.splice(index, 1);
  }

  initForm(): void {
    this.userForm = this.fb.group({
      name: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      phoneNumber: ['', Validators.required],
      nationalId: ['', Validators.required],
      joinDate: [null, Validators.required],
      dateOfPayment :[null]
    });
  }

  loadUsers(): void {
    this.loading.set(true);

    this.userService.getAll().subscribe(res => {
      if (res.success && res.data) {
        this.users.set(res.data);
      }
      this.loading.set(false);
    });
  }

onFileSelected(event: any, index: number): void {
  const file = event.target.files?.[0] ?? null;

  this.documents[index].file = file;
}

  openCreate(): void {
    this.editMode.set(false);
  
    this.userForm.reset({
      name: '',
      email: '',
      phoneNumber: '',
      nationalId: '',
      joinDate: null,
      dateOfPayment:null
    });
  
    this.documents = [];
  
    this.showDialog = true;
  }

  openEdit(user: any): void {
    this.editMode.set(true);
    this.editId = user.id;
  
    this.userForm.patchValue({
      name: user.name || '',
      email: user.email || '',
      phoneNumber: user.phoneNumber || '',
      nationalId: user.nationalId || '',
      joinDate: user.joinDate ? new Date(user.joinDate) : null,
      dateOfPayment: user.dateOfPayment
        ? new Date(user.dateOfPayment)
        : null
    });
  
    this.documents = (user.documents || []).map((d: any) => ({
      id: d.id,
      file: null,
      fileName: d.fileName,
      type: d.documentType,
      existing: true
    }));
  
    this.showDialog = true;
  }
  downloadDocument(documentId: string, fileName: string): void {

    this.downloading.set(true);
  
    this.userService
      .downloadDocument(documentId)
      .subscribe({
        next: blob => {
  
          const url = window.URL.createObjectURL(blob);
  
          const a = document.createElement('a');
          a.href = url;
          a.download = fileName;
  
          a.click();
  
          window.URL.revokeObjectURL(url);
  
          this.downloading.set(false);
        },
        error: () => {
  
          this.downloading.set(false);
  
          this.toast.add({
            severity: 'error',
            summary: 'Error',
            detail: 'Failed to download document'
          });
        }
      });
  }
  saveUser(): void {
    if (this.userForm.invalid) {
      this.userForm.markAllAsTouched();
      return;
    }
  
    this.saving.set(true);
  
    const formValue = this.userForm.value;
  
    const formData = new FormData();
    formData.append('name', formValue.name);
    formData.append('email', formValue.email);
    formData.append('phoneNumber', formValue.phoneNumber);
    formData.append('nationalId', formValue.nationalId || '');
  
    if (formValue.joinDate) {
      const date = formValue.joinDate as Date;
      const formatted =
        date.getFullYear() + '-' +
        String(date.getMonth() + 1).padStart(2, '0') + '-' +
        String(date.getDate()).padStart(2, '0');
  
      formData.append('joinDate', formatted);
    }
  
    if (formValue.dateOfPayment) {
      const date = formValue.dateOfPayment as Date;
      const formatted =
        date.getFullYear() + '-' +
        String(date.getMonth() + 1).padStart(2, '0') + '-' +
        String(date.getDate()).padStart(2, '0');
  
      formData.append('dateOfPayment', formatted);
    }

    for (const doc of this.documents) {

      // existing document
      if (doc.existing && doc.id) {
        formData.append('existingDocumentIds', doc.id);
      }
    
      // new document
      if (doc.file && doc.type) {
        formData.append('documentFiles', doc.file);
        formData.append('documentTypes', doc.type);
      }
    }
  
    const request$ = this.editMode()
      ? this.userService.update(this.editId, formData)
      : this.userService.create(formData);
  
    request$.subscribe({
      next: (res: any) => {
        this.saving.set(false);
        if (res.success) {
          this.showDialog = false;
          this.toast.add({
            severity: 'success',
            summary: 'Saved',
            detail: this.editMode() ? 'User updated' : 'User created'
          });
          this.loadUsers();
        }
      },
      error: (err: any) => {
        this.saving.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Error',
          detail: err.error?.message ?? 'Operation failed.'
        });
      }
    });
  }

  edituserandcar(user: User): void {
    this.router.navigate([`/create-user-car/${user.id}`]);
}

goToCreateUserCar(): void {
  this.router.navigate(['/create-user-car']);
}
}
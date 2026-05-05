import { Component, inject, OnInit, signal } from '@angular/core';
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
    CalendarModule
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

  users = signal<User[]>([]);
  loading = signal(true);
  saving = signal(false);

  showDialog = false;
  editMode = signal(false);
  editId = '';

  form: any = {
    name: '',
    email: '',
    phoneNumber: '',
    nationalId: '',
    joinDate: null
  };

  selectedFile: File | null = null;
  documentType: string = '';

  ngOnInit(): void {
    this.loadUsers();
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

  onFileSelected(event: any): void {
    this.selectedFile = event.target.files[0];
  }

  openCreate(): void {
    this.editMode.set(false);
    this.form = {
      name: '',
      email: '',
      phoneNumber: ''
    };
    this.showDialog = true;
  }

openEdit(user: any): void {
  this.editMode.set(true);
  this.editId = user.id;

  this.form = {
    name: user.name || '',
    email: user.email || '',
    phoneNumber: user.phoneNumber || '',
    nationalId: user.nationalId || '',
    joinDate: user.joinDate ? new Date(user.joinDate) : null
  };

  this.showDialog = true;
}

saveUser(): void {
  if (!this.form.name || !this.form.email || !this.form.phoneNumber || !this.form.joinDate) {
    this.toast.add({ severity: 'warn', summary: 'Required', detail: 'All fields are required.' });
    return;
  }

  this.saving.set(true);

  const formData = new FormData();
  formData.append('name', this.form.name);
  formData.append('email', this.form.email);
  formData.append('phoneNumber', this.form.phoneNumber);
  formData.append('nationalId', this.form.nationalId || '');

  if (this.form.joinDate) {
    const date = this.form.joinDate as Date;
    const formatted =
      date.getFullYear() + '-' +
      String(date.getMonth() + 1).padStart(2, '0') + '-' +
      String(date.getDate()).padStart(2, '0');
    formData.append('joinDate', formatted);
  }

  if (this.selectedFile && this.documentType) {
    formData.append('documentFile', this.selectedFile);
    formData.append('documentType', this.documentType);
  }

  // ✅ Branch: create vs update
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
      } else {
        this.toast.add({ severity: 'error', summary: 'Error', detail: res.message });
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
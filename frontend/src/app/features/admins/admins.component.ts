import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../core/services/api.services';
import { Admin } from '../../core/models';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { DropdownModule } from 'primeng/dropdown';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MessageService, ConfirmationService } from 'primeng/api';
import { AvatarModule } from 'primeng/avatar';
@Component({
  selector: 'app-admins',
  standalone: true,
  imports: [CommonModule, FormsModule, TableModule, ButtonModule, DialogModule,
    InputTextModule, PasswordModule, DropdownModule, TagModule, ToastModule,
    ConfirmDialogModule, AvatarModule],
  templateUrl: './admins.component.html',
  styleUrl: './admins.component.css'
})
export class AdminsComponent implements OnInit {
private adminService = inject(AdminService);
  private toast = inject(MessageService);
  private confirm = inject(ConfirmationService);

  admins = signal<Admin[]>([]);
  loading = signal(true);
  saving = signal(false);
  showDialog = false;
  form = { name: '', username: '', password: '', role: 'Admin' };
  roleOptions = [
    { label: 'Admin', value: 'Admin' },
    { label: 'SuperAdmin', value: 'SuperAdmin' }
  ];

  ngOnInit(): void { this.loadAdmins(); }

  loadAdmins(): void {
    this.loading.set(true);
    this.adminService.getAll().subscribe(res => {
      if (res.success && res.data) this.admins.set(res.data);
      this.loading.set(false);
    });
  }

  openCreate(): void { this.form = { name: '', username: '', password: '', role: 'Admin' }; this.showDialog = true; }

  createAdmin(): void {
    if (!this.form.name || !this.form.username || !this.form.password) {
      this.toast.add({ severity: 'warn', summary: 'Required', detail: 'All fields are required.' }); return;
    }
    this.saving.set(true);
    this.adminService.create(this.form).subscribe({
      next: res => {
        this.saving.set(false);
        if (res.success) { this.showDialog = false; this.toast.add({ severity: 'success', summary: 'Created', detail: 'Admin account created.' }); this.loadAdmins(); }
        else this.toast.add({ severity: 'error', summary: 'Error', detail: res.message });
      },
      error: err => { this.saving.set(false); this.toast.add({ severity: 'error', summary: 'Error', detail: err.error?.message ?? 'Failed to create admin.' }); }
    });
  }

  deleteAdmin(admin: Admin): void {
    this.confirm.confirm({
      message: `Delete admin <strong>${admin.name}</strong>?`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.adminService.delete(admin.id).subscribe({
          next: res => { if (res.success) { this.toast.add({ severity: 'success', summary: 'Deleted', detail: 'Admin removed.' }); this.loadAdmins(); } },
          error: () => this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to delete.' })
        });
      }
    });
  }
}

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
import { Observable } from 'rxjs';
@Component({
  selector: 'app-users',
  standalone: true,
  imports: [CommonModule, FormsModule, TableModule, ButtonModule, DialogModule,
    InputTextModule, ToastModule, ConfirmDialogModule, AvatarModule, TooltipModule],
  providers: [MessageService, ConfirmationService],  
  templateUrl: './users.component.html',
  styleUrl: './users.component.css'
})
export class UsersComponent implements OnInit {
  private userService = inject(UserService);
  private toast = inject(MessageService);
  private confirm = inject(ConfirmationService);

  users = signal<User[]>([]);
  loading = signal(true);
  saving = signal(false);
  showDialog = false;
  editMode = signal(false);
  editId = '';
  form = { name: '', email: '', phoneNumber: '' };

  ngOnInit(): void { this.loadUsers(); }

  loadUsers(): void {
    this.loading.set(true);
    this.userService.getAll().subscribe(res => {
      if (res.success && res.data) this.users.set(res.data);
      this.loading.set(false);
    });
  }

  openCreate(): void { this.editMode.set(false); this.form = { name: '', email: '', phoneNumber: '' }; this.showDialog = true; }

  openEdit(user: User): void {
    this.editMode.set(true);
    this.editId = user.id;
    this.form = { name: user.name, email: user.email, phoneNumber: user.phoneNumber };
    this.showDialog = true;
  }

  saveUser(): void {
    if (!this.form.name || !this.form.email || !this.form.phoneNumber) {
      this.toast.add({ severity: 'warn', summary: 'Required', detail: 'All fields are required.' }); return;
    }
    this.saving.set(true);
const req = (this.editMode()
  ? this.userService.update(this.editId, this.form)
  : this.userService.create(this.form)) as Observable<any>;

    req.subscribe({
      next: (res: any) => {
        this.saving.set(false);
        if (res.success) {
          this.showDialog = false;
          this.toast.add({ severity: 'success', summary: 'Saved', detail: `User ${this.editMode() ? 'updated' : 'created'} successfully.` });
          this.loadUsers();
        } else this.toast.add({ severity: 'error', summary: 'Error', detail: res.message });
      },
      error : (err:any) => { this.saving.set(false); this.toast.add({ severity: 'error', summary: 'Error', detail: err.error?.message ?? 'Operation failed.' }); }
    });
  }

  deleteUser(user: User): void {
    this.confirm.confirm({
      message: `Delete user <strong>${user.name}</strong>?`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.userService.delete(user.id).subscribe({
          next: res => { if (res.success) { this.toast.add({ severity: 'success', summary: 'Deleted', detail: 'User removed.' }); this.loadUsers(); } },
          error: () => this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to delete.' })
        });
      }
    });
  }
}


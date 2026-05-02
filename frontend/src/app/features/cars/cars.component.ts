import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CarService, UserService } from '../../core/services/api.services';
import { Car, User } from '../../core/models';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { DropdownModule } from 'primeng/dropdown';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MessageService, ConfirmationService } from 'primeng/api';
import { TooltipModule } from 'primeng/tooltip';
@Component({
  selector: 'app-cars',
  standalone: true,
  imports: [CommonModule, FormsModule, TableModule, ButtonModule, DialogModule,
    InputTextModule, InputNumberModule, DropdownModule, TagModule, ToastModule,
    ConfirmDialogModule, TooltipModule],
providers: [MessageService, ConfirmationService] ,
  templateUrl: './cars.component.html',
  styleUrl: './cars.component.css'
})
export class CarsComponent implements OnInit {
 private carService = inject(CarService);
  private userService = inject(UserService);
  private toast = inject(MessageService);
  private confirm = inject(ConfirmationService);

  cars = signal<Car[]>([]);
  loading = signal(true);
  saving = signal(false);

  showCreate = false;
  showAssign = false;
  showRentalPrice = false;

  newPlate = '';
  newRentalPrice: number | null = null;
  selectedCar = signal<Car | null>(null);
  selectedUserId = '';
  rentalPriceValue: number | null = null;
  userOptions = signal<{label: string, value: string}[]>([]);

  ngOnInit(): void {
    this.loadCars();
    this.loadUsers();
  }

  loadCars(): void {
    this.loading.set(true);
    this.carService.getAll().subscribe(res => {
      if (res.success && res.data) this.cars.set(res.data);
      this.loading.set(false);
    });
  }

  loadUsers(): void {
    this.userService.getAll().subscribe(res => {
      if (res.success && res.data)
        this.userOptions.set(res.data.map(u => ({ label: `${u.name} (${u.email})`, value: u.id })));
    });
  }

  openCreate(): void { this.newPlate = ''; this.newRentalPrice = null; this.showCreate = true; }

  createCar(): void {
    if (!this.newPlate.trim()) { this.toast.add({ severity: 'warn', summary: 'Required', detail: 'Car plate is required.' }); return; }
    this.saving.set(true);
    this.carService.create({ carPlate: this.newPlate.trim(), rentalPrice: this.newRentalPrice ?? 0 }).subscribe({
      next: res => {
        this.saving.set(false);
        if (res.success) { this.showCreate = false; this.toast.add({ severity: 'success', summary: 'Registered', detail: `Car ${this.newPlate} added.` }); this.loadCars(); }
        else this.toast.add({ severity: 'error', summary: 'Error', detail: res.message });
      },
      error: err => { this.saving.set(false); this.toast.add({ severity: 'error', summary: 'Error', detail: err.error?.message ?? 'Failed to create car.' }); }
    });
  }

  openAssign(car: Car): void { this.selectedCar.set(car); this.selectedUserId = car.userId ?? ''; this.showAssign = true; }

  assignCar(): void {
    if (!this.selectedUserId) { this.toast.add({ severity: 'warn', summary: 'Required', detail: 'Please select a user.' }); return; }
    this.saving.set(true);
    this.carService.assignToUser({ carPlate: this.selectedCar()!.carPlate, userId: this.selectedUserId }).subscribe({
      next: res => {
        this.saving.set(false);
        if (res.success) { this.showAssign = false; this.toast.add({ severity: 'success', summary: 'Assigned', detail: 'Car assigned successfully.' }); this.loadCars(); }
        else this.toast.add({ severity: 'error', summary: 'Error', detail: res.message });
      },
      error: () => { this.saving.set(false); this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to assign car.' }); }
    });
  }

  openRentalPrice(car: Car): void { this.selectedCar.set(car); this.rentalPriceValue = car.rentalPrice; this.showRentalPrice = true; }

  saveRentalPrice(): void {
    this.saving.set(true);
    this.carService.setRentalPrice(this.selectedCar()!.carPlate, this.rentalPriceValue ?? 0).subscribe({
      next: res => {
        this.saving.set(false);
        if (res.success) { this.showRentalPrice = false; this.toast.add({ severity: 'success', summary: 'Updated', detail: 'Rental price updated.' }); this.loadCars(); }
        else this.toast.add({ severity: 'error', summary: 'Error', detail: res.message });
      },
      error: () => { this.saving.set(false); this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to update price.' }); }
    });
  }

  deleteCar(car: Car): void {
    this.confirm.confirm({
      message: `Delete car plate <strong>${car.carPlate}</strong>? This will also remove all associated fines and entrance fees.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.carService.delete(car.carPlate).subscribe({
          next: res => {
            if (res.success) { this.toast.add({ severity: 'success', summary: 'Deleted', detail: `Car ${car.carPlate} removed.` }); this.loadCars(); }
          },
          error: () => this.toast.add({ severity: 'error', summary: 'Error', detail: 'Failed to delete.' })
        });
      }
    });
  }

  onSearch(event: Event, dt: any): void {
    const val = (event.target as HTMLInputElement).value;
    dt.filterGlobal(val, 'contains');
  }
}

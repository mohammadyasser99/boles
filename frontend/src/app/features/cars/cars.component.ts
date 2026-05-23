import { Component, inject, OnInit, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
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
import { Router } from '@angular/router';
import {  distinctUntilChanged } from 'rxjs';

@Component({
  selector: 'app-cars',
  standalone: true,
  imports: [CommonModule, FormsModule, TableModule, ButtonModule, DialogModule,
    InputTextModule, InputNumberModule, DropdownModule, TagModule, ToastModule,
    ConfirmDialogModule, TooltipModule,ReactiveFormsModule],
providers: [MessageService, ConfirmationService] ,
  templateUrl: './cars.component.html',
  styleUrl: './cars.component.css'
})
export class CarsComponent implements OnInit {
 private carService = inject(CarService);
  private userService = inject(UserService);
  private toast = inject(MessageService);
  private confirm = inject(ConfirmationService);
  private router = inject(Router);
  // inside the class:
searchTerm = '';
searchBy = 'carplate';
searchByOptions = [
  { label: '🔤 Car Plate', value: 'carplate' },
  { label: '👤 Client Name', value: 'username' }
];
cars = signal<Car[]>([]);
totalRecords = signal(0);
page = signal(1);
rows = signal(10);
  loading = signal(true);
  saving = signal(false);

  showCreate = false;
  showAssign = false;
  showRentalPrice = false;
newBrand = '';
newModel = '';
newYear: number | null = null;
newChassisNumber = '';
  newPlate = '';
  newRentalPrice: number | null = null;
  selectedCar = signal<Car | null>(null);
  selectedUserId = '';
  rentalPriceValue: number | null = null;
  userOptions = signal<{label: string, value: string}[]>([]);

  private fb = inject(FormBuilder);

carForm!: FormGroup;

  ngOnInit(): void {
    this.initForm();
    this.loadCars();
    this.loadUsers();

  }

  initForm(): void {
    this.carForm = this.fb.group({
      carPlate: ['', Validators.required],
      brand: [''],
      model: [''],
      year: [null],
      chassisNumber: [''],
      rentalPrice: [0]
    });
  }

loadCars(): void {
  this.loading.set(true);
  this.carService.getAllWithDebs(this.page(), this.rows(), this.searchTerm, this.searchBy)
    .subscribe((res: any) => {
      if (res.success && res.data) {
        this.cars.set(res.data.items);
        this.totalRecords.set(res.data.totalCount);
      }
      this.loading.set(false);
    });
}


search(): void {
  this.page.set(1);
  this.loadCars();
}

clearSearch(): void {
  this.searchTerm = '';
  this.page.set(1);
  this.loadCars();
}




onPageChange(event: any): void {
  this.page.set(event.first / event.rows + 1);
  this.rows.set(event.rows);
  this.loadCars();
}
loadUsers(): void {
  this.userService.getAll().subscribe(res => {
    if (res.success && res.data) {
      const users = res.data.map((u: any) => ({
        label: `${u.name} (${u.email})`,
        value: u.id
      }));

      // 👇 add unassign option at top
      this.userOptions.set([
        { label: 'Unassign User', value: '' },
        ...users
      ]);
    }
  });
}

openCreate(): void {
  this.carForm.reset({
    carPlate: '',
    brand: '',
    model: '',
    year: null,
    chassisNumber: '',
    rentalPrice: 0
  });

  this.showCreate = true;
}
createCar(): void {
  if (this.carForm.invalid) {
    this.carForm.markAllAsTouched();
    return;
  }

  this.saving.set(true);

  const formValue = this.carForm.value;

  this.carService.create({
    carPlate: formValue.carPlate.trim(),
    brand: formValue.brand,
    model: formValue.model,
    year: formValue.year,
    chassisNumber: formValue.chassisNumber,
    rentalPrice: formValue.rentalPrice ?? 0
  }).subscribe({
    next: res => {
      this.saving.set(false);
      if (res.success) {
        this.showCreate = false;
        this.toast.add({
          severity: 'success',
          summary: 'Registered',
          detail: `Car ${formValue.carPlate} added.`
        });
        this.loadCars();
      }
    },
    error: () => {
      this.saving.set(false);
      this.toast.add({
        severity: 'error',
        summary: 'Error',
        detail: 'Failed to create car.'
      });
    }
  });
}


  openAssign(car: Car): void { this.selectedCar.set(car); this.selectedUserId = car.userId ?? ''; this.showAssign = true; }

  assignCar(): void {
    this.saving.set(true);
  
    const payload: any = {
      carPlate: this.selectedCar()!.carPlate,
      userId: this.selectedUserId || null   // 👈 IMPORTANT
    };
  
    this.carService.assignToUser(payload).subscribe({
      next: res => {
        this.saving.set(false);
  
        if (res.success) {
          this.showAssign = false;
  
          this.toast.add({
            severity: 'success',
            summary: 'Updated',
            detail: this.selectedUserId
              ? 'Car assigned successfully.'
              : 'Car unassigned successfully.'
          });
  
          this.loadCars();
        }
      },
      error: () => {
        this.saving.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Error',
          detail: 'Failed to update assignment.'
        });
      }
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

  goToDetails(car: Car): void {
    this.router.navigate([`/car-payment-report/${car.carPlate}`]);
  }


}

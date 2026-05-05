import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';

// PrimeNG
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { CalendarModule } from 'primeng/calendar';
import { MessageService } from 'primeng/api';
import { UserService } from 'src/app/core/services/api.services';
@Component({
  selector: 'app-create-user-and-car',
  standalone: true,
  imports: [CommonModule, FormsModule, HttpClientModule, InputTextModule, ButtonModule, ToastModule, CalendarModule],
  providers: [MessageService],
  templateUrl: './create-user-and-car.component.html',
  styleUrl: './create-user-and-car.component.css'
})
export class CreateUserAndCarComponent {
  isEditMode = false;
    model: any = {
    name: '',
    phoneNumber: '',
    email: '',
    nationalId: '',
    dateOfPayment: null,
    joinDate:null ,
    carPlate: '',
    brand: '',
    modelName: '',
    year: null,
    rentalPrice: null,
    chassisNumber: '',
    userId: null
  };
    private userService = inject(UserService);
    private toast = inject(MessageService);
    private route = inject(ActivatedRoute);

  constructor() {
    const routeUserId = this.route.snapshot.paramMap.get('userId') ?? this.route.snapshot.paramMap.get('userid');
    const queryUserId = this.route.snapshot.queryParamMap.get('userId') ?? this.route.snapshot.queryParamMap.get('userid');
    const userId = routeUserId ?? queryUserId;

    if (userId) {
      this.isEditMode = true;
      this.model.userId = userId;
      this.loadUserWithCar(userId);
    }
  }

  private loadUserWithCar(userId: string) {
    this.userService.getUserWithCar(userId).subscribe({
      next: (res) => {
        const data = res?.data;
        if (!data) return;

        this.model = {
          ...this.model,
          name: data.name ?? '',
          phoneNumber: data.phoneNumber ?? '',
          email: data.email ?? '',
          nationalId: data.nationalId ?? '',
          dateOfPayment: data.dateOfPayment ? new Date(data.dateOfPayment) : null,
          joinDate: data.joinDate ? new Date(data.joinDate) : null,
          carPlate: data.carPlate ?? '',
          brand: data.brand ?? '',
          modelName: data.model ?? '',
          year: data.year ?? null,
          rentalPrice: data.rentalPrice ?? null,
          chassisNumber: data.chassisNumber ?? '',
          userId: data.userId ?? userId
        };
      },
      error: (err) => {
        this.toast.add({
          severity: 'error',
          summary: 'Error',
          detail: err.error?.message || 'Failed to load user data'
        });
      }
    });
  }
  submit() {
    const payload = {
      ...this.model,
      model: this.model.modelName,
      dateOfPayment: this.model.dateOfPayment
        ? this.model.dateOfPayment.toISOString().split('T')[0]
        : null,
      joinDate: this.model.joinDate
        ? this.model.joinDate.toISOString().split('T')[0]
        : null
    };
  
    delete payload.modelName;
  
    if (!payload.name || !payload.phoneNumber || !payload.carPlate) {
      this.toast.add({
        severity: 'warn',
        summary: 'Validation',
        detail: 'Name, Phone, and Car Plate are required'
      });
      return;
    }
  
    // 🔥 SWITCH HERE
    const request$ = this.isEditMode
      ? this.userService.updateCarAndUser(payload)   // 👈 UPDATE API
      : this.userService.createwithcar(payload);     // 👈 CREATE API
  
      request$.subscribe({
        next: (res: any) => {
          // ✅ Handle both ApiResponse and plain string
          const message =
            typeof res === 'string'
              ? res
              : res?.message || 'Success';
      
          this.toast.add({
            severity: 'success',
            summary: 'Success',
            detail: message
          });
      
          if (!this.isEditMode) {
            this.reset();
          }
        },
        error: (err) => {
          // 🔥 IMPORTANT: sometimes 200 comes here بسبب parsing error
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
    this.model = {
      name: '',
      phoneNumber: '',
      email: '',
      nationalId: '',
      dateOfPayment: null,
      joinDate:null,
      carPlate: '',
      brand: '',
      modelName: '',
      year: null,
      rentalPrice: null,
      chassisNumber: '',
      userId: null
    };
  }
}

import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { InputTextModule } from 'primeng/inputtext';
import { FormsModule } from '@angular/forms';
import { CarService } from 'src/app/core/services/api.services';
import { TooltipModule } from 'primeng/tooltip';
import { DropdownModule } from 'primeng/dropdown';
import { PaginatorModule } from 'primeng/paginator';
import { MessageService } from 'primeng/api';

interface CarDebt {
  carPlate:     string;
  userName:     string | null;
  userId:       string | null;
  totalDebs:    number;
  unpaidRental: number;
  unpaidFines:  number;
  unpaidFees:   number;
}

interface UserGroup {
  userId:      string | null;
  userName:    string | null;
  cars:        CarDebt[];
  totalRental: number;
  totalFines:  number;
  totalFees:   number;
  grandTotal:  number;
}

@Component({
  selector: 'app-users-arrear',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    TableModule, ButtonModule,
    SkeletonModule, TagModule,
    InputTextModule, TooltipModule,
    DropdownModule, PaginatorModule,
  ],
  templateUrl: './users-arrear.component.html',
  styleUrl: './users-arrear.component.css'
})
export class UsersArrearComponent implements OnInit {
  private carService = inject(CarService);
  private router     = inject(Router);
private toast = inject(MessageService);
  searchByOptions = [
    { label: 'Username',    value: 'username'   },
    { label: 'National ID', value: 'nationalid' },
    { label: 'Phone',       value: 'phone'      },
    { label: 'Email',       value: 'email'      },
  ];

  searchValue   = '';
  searchByValue = 'username';

  loading      = signal(false);
  errorMsg     = signal('');
  allCars      = signal<CarDebt[]>([]);
  currentPage  = signal(1);
  totalRecords = signal(0);
  readonly pageSize    = 10;
  readonly skeletonRows = Array(10).fill({});

  userGroups = computed<UserGroup[]>(() => {
    const cars = this.allCars();
    const map  = new Map<string, UserGroup>();

    for (const car of cars) {
      const key = car.userId ?? car.carPlate;

      if (!map.has(key)) {
        map.set(key, {
          userId:      car.userId,
          userName:    car.userName,
          cars:        [],
          totalRental: 0,
          totalFines:  0,
          totalFees:   0,
          grandTotal:  0,
        });
      }

      const g = map.get(key)!;
      g.cars.push(car);
      g.totalRental += car.unpaidRental;
      g.totalFines  += car.unpaidFines;
      g.totalFees   += car.unpaidFees;
      g.grandTotal  += car.totalDebs;
    }

    return Array.from(map.values())
      .sort((a, b) => b.grandTotal - a.grandTotal);
  });

  ngOnInit() { this.load(); }

  load(page = 1) {
    this.loading.set(true);
    this.currentPage.set(page);

    this.carService.getAllWithDebs(
      page,
      this.pageSize,
      this.searchValue,
      this.searchByValue
    ).subscribe({
      next: (res: any) => {
        const paged = res?.data;
        const items = paged?.items ?? res?.data ?? [];

        this.totalRecords.set(paged?.totalCount ?? items.length);

        this.allCars.set(items.map((c: any) => ({
          carPlate:     c.carPlate,
          userName:     c.userName,
          userId:       c.userId,
          totalDebs:    c.totaldebs    ?? 0,
          unpaidRental: c.unpaidRental ?? 0,
          unpaidFines:  c.unpaidFines  ?? 0,
          unpaidFees:   c.unpaidFees   ?? 0,
        })));
        this.loading.set(false);
      },
      error: (err: any) => {
        this.errorMsg.set(err?.error?.message ?? 'Failed to load data.');
        this.loading.set(false);
      }
    });
  }

  onPageChange(event: any) {
    this.load(event.page + 1);
  }

  clearSearch() {
    this.searchValue   = '';
    this.searchByValue = 'username';
    this.load(1);
  }

goToReport(carPlate: string) {
  const car = this.allCars().find(c => c.carPlate === carPlate);

  if (!car?.userId) {
    this.toast.add({
      severity: 'warn',
      summary: 'Not Assigned',
      detail: 'This car is not assigned to any user'
    });
    return;
  }

  this.router.navigate(['/car-payment-report', carPlate]);
}

  fmt(v: number) {
    return new Intl.NumberFormat('en-AE', {
      style: 'currency', currency: 'AED', minimumFractionDigits: 0
    }).format(v);
  }

  debtSeverity(total: number): 'success' | 'warning' | 'danger' {
    if (total === 0)  return 'success';
    if (total < 1000) return 'warning';
    return 'danger';
  }
}
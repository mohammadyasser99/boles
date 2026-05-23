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
  userId:       string | null;
  userName:     string | null;
  cars:         CarDebt[];
  totalRental:  number;
  totalFines:   number;
  totalFees:    number;
  grandTotal:   number;
}
@Component({
  selector: 'app-users-arrear',
  standalone: true,
  imports: [    CommonModule, FormsModule,
    TableModule, ButtonModule,
    SkeletonModule, TagModule, InputTextModule, TooltipModule, DropdownModule],
  templateUrl: './users-arrear.component.html',
  styleUrl: './users-arrear.component.css'
})
export class UsersArrearComponent  implements OnInit {
  private carService = inject(CarService);
  private router     = inject(Router);

  search   = signal('');
searchBy = signal('username');

searchByOptions = [
  { label: 'Username',    value: 'username'   },
  { label: 'National ID', value: 'nationalid' },
  { label: 'Phone',       value: 'phone'      },
  { label: 'Email',       value: 'email'      },
];
  loading   = signal(false);
  errorMsg  = signal('');
  allCars   = signal<CarDebt[]>([]);
searchValue = '';
searchByValue = 'username';
  readonly skeletonRows = Array(8).fill({});

  userGroups = computed<UserGroup[]>(() => {
    const cars = this.allCars();
    const map = new Map<string, UserGroup>();

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
      g.totalRental  += car.unpaidRental;
      g.totalFines   += car.unpaidFines;
      g.totalFees    += car.unpaidFees;
      g.grandTotal   += car.totalDebs;
    }

    let groups = Array.from(map.values())
      .sort((a, b) => b.grandTotal - a.grandTotal);



    return groups;
  });

  ngOnInit() { this.load(); }
clearSearch() {
  this.searchValue = '';
  this.searchByValue = 'username';
  this.load();
}
 load(page = 1) {
  this.loading.set(true);

  this.carService.getAllWithDebs(
    page,
    200,
    this.searchValue,
    this.searchByValue
  ).subscribe({
      next: (res: any) => {
        const items = res?.data?.items ?? res?.data ?? [];
        this.allCars.set(items.map((c: any) => ({
          carPlate:     c.carPlate,
          userName:     c.userName,
          userId:       c.userId,
          totalDebs:    c.totaldebs   ?? 0,
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

  goToReport(carPlate: string) {
    this.router.navigate(['/car-payment-report', carPlate]);
  }

  fmt(v: number) {
    return new Intl.NumberFormat('en-AE', {
      style: 'currency', currency: 'AED', minimumFractionDigits: 0
    }).format(v);
  }

  debtSeverity(total: number): 'success' | 'warning' | 'danger' {
    if (total === 0)    return 'success';
    if (total < 1000)   return 'warning';
    return 'danger';
  }
}

import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FineService } from '../../core/services/api.services';
import { CarService } from '../../core/services/api.services';
import { UserService } from '../../core/services/api.services';
import { CarDebt, Car, User } from '../../core/models';
import { CardModule } from 'primeng/card';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';
import { ButtonModule } from 'primeng/button';
@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, CardModule, TableModule, TagModule, SkeletonModule, ButtonModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit {
private fineService = inject(FineService);
  private carService = inject(CarService);
  private userService = inject(UserService);

  loading = signal(true);
  debts = signal<CarDebt[]>([]);
  cars = signal<Car[]>([]);
  users = signal<User[]>([]);

  totalDebt = signal(0);
  carsWithDebt = signal(0);
  topDebts = signal<CarDebt[]>([]);

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);

    this.fineService.getAllDebts().subscribe(res => {
      if (res.success && res.data) {
        this.debts.set(res.data);
        this.totalDebt.set(res.data.reduce((s, d) => s + d.totalDebt, 0));
        this.carsWithDebt.set(res.data.filter(d => d.totalDebt > 0).length);
        this.topDebts.set([...res.data].sort((a, b) => b.totalDebt - a.totalDebt).slice(0, 8));
      }
      this.loading.set(false);
    });

    this.carService.getAll().subscribe(res => { if (res.success && res.data) this.cars.set(res.data); });
    this.userService.getAll().subscribe(res => { if (res.success && res.data) this.users.set(res.data); });
  }
}

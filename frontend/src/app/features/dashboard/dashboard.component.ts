import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FineService, PaymentService } from '../../core/services/api.services';
import { CarDebt, Car, User, SystemMonthlyRowDto } from '../../core/models';
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
private paymentService = inject(PaymentService)

  loading = signal(true);
  debts = signal<CarDebt[]>([]);
  cars = signal<Car[]>([]);
  users = signal<User[]>([]);
  systemSummary = signal<SystemMonthlyRowDto | null>(null);

  totalRevenue = signal(0);
  totalDebt = signal(0);
  netBalance = signal(0);
  totalFines = signal(0);
  totalEntranceFees = signal(0);
  finesCount = signal(0);
  entranceFeesCount = signal(0);

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
  
    this.paymentService.getSystemSummary().subscribe(res => {
      if (res.success && res.data) {
        const data = res.data;
  
        this.systemSummary.set(data);
  
        this.totalRevenue.set(data.totalRevenue);
        this.totalDebt.set(data.totalDebt);
        this.netBalance.set(data.netBalance);
  
        this.totalFines.set(data.totalFines);
        this.totalEntranceFees.set(data.totalEntranceFees);
        this.finesCount.set(data.finesCount);
        this.entranceFeesCount.set(data.entranceFeesCount);
      }
  
      this.loading.set(false);
    });
  }
}

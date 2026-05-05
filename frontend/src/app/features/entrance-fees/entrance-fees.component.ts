import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EntranceFeeService } from '../../core/services/api.services';
import { EntranceFeeImportResult } from '../../core/models';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
@Component({
  selector: 'app-entrance-fees',
  standalone: true,
  imports: [CommonModule, TableModule, ButtonModule, ToastModule],
  templateUrl: './entrance-fees.component.html',
  styleUrl: './entrance-fees.component.css'
})
export class EntranceFeesComponent implements OnInit{
private entranceFeeService = inject(EntranceFeeService);
  private toast = inject(MessageService);
  searchMode = signal<'trip' | 'car' | null>(null);
  selectedFile = signal<File | null>(null);
  uploading = signal(false);
  importResult = signal<EntranceFeeImportResult | null>(null);
fees = signal<any[]>([]);
totalRecords = signal(0);

page = signal(1);
pageSize = 10;
searchCarPlate = signal('');
searchTripNumber = signal('');
searchIsPaid = signal<boolean | null>(null);

loadingFees = signal(false);

processingTrip = signal<string | null>(null);

ngOnInit(): void {
  this.loadFees();
}

markAsPaid(tripNumber: string) {
  this.processingTrip.set(tripNumber);

  this.entranceFeeService.markAsPaid(tripNumber).subscribe({
    next: res => {
      this.processingTrip.set(null);

      if (res.success) {
        this.fees.update(list =>
          list.map(f =>
            f.tripNumber === tripNumber ? { ...f, isPaid: true } : f
          )
        );
      }
    },
    error: () => this.processingTrip.set(null)
  });
}

loadFees(): void {
  this.loadingFees.set(true);

  this.entranceFeeService.searchFees(
    this.searchTripNumber() || undefined,
    this.searchCarPlate() || undefined,
    this.searchIsPaid() ?? undefined,
    this.page(),
    this.pageSize
  )
    .subscribe({
      next: res => {
        this.loadingFees.set(false);
        if (res.success && res.data) {
          this.fees.set(res.data.items);
          this.totalRecords.set(res.data.totalCount);
        }
      },
      error: () => this.loadingFees.set(false)
    });
}
  onFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) this.selectedFile.set(input.files[0]);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    const file = event.dataTransfer?.files[0];
    if (file) this.selectedFile.set(file);
  }

  search(): void {
    this.page.set(1);
  
    const mode = this.searchMode();
  
    if (mode === 'trip') {
      this.searchCarPlate.set('');
    }
  
    if (mode === 'car') {
      this.searchTripNumber.set('');
    }
  
    this.loadFees();
  }

resetFilters(): void {
  this.searchTripNumber.set('');
  this.searchCarPlate.set('');
  this.searchIsPaid.set(null);
  this.page.set(1);
  this.loadFees();
}

onPageChange(event: any) {
  const page = event.first / event.rows + 1;
  this.page.set(page);
  this.pageSize = event.rows;
  this.loadFees();
}
  uploadFees(): void {
    if (!this.selectedFile()) return;
    this.uploading.set(true);
    this.importResult.set(null);

    this.entranceFeeService.importFees(this.selectedFile()!).subscribe({
      next: res => {
        this.uploading.set(false);
        if (res.success && res.data) {
          this.importResult.set(res.data);
          this.selectedFile.set(null);
          this.toast.add({ severity: 'success', summary: 'Import Complete', detail: `${res.data.newFeesAdded} new trips added.` });
        } else this.toast.add({ severity: 'error', summary: 'Error', detail: res.message });
      },
      error: err => { this.uploading.set(false); this.toast.add({ severity: 'error', summary: 'Error', detail: err.error?.message ?? 'Upload failed.' }); }
    });
  }
}

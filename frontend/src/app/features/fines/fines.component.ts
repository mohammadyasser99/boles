import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FineService } from '../../core/services/api.services';
import { FineImportResult } from '../../core/models';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { FormsModule } from '@angular/forms';
@Component({
  selector: 'app-fines',
  standalone: true,
  imports: [CommonModule, TableModule, ButtonModule, ToastModule, FormsModule],
  providers: [MessageService],
  templateUrl: './fines.component.html',
  styleUrl: './fines.component.css'
})
export class FinesComponent implements OnInit {

  private fineService = inject(FineService);
  private toast = inject(MessageService);

  // Upload
  selectedFile = signal<File | null>(null);
  uploading = signal(false);
  importResult = signal<FineImportResult | null>(null);

  // Table
  fines = signal<any[]>([]);
  totalRecords = signal(0);

  page = signal(1);
  pageSize = 10;

  searchViolationNumber = signal('');
  searchIsPaid = signal<boolean | null>(null);
  selectedSearchType: 'violation' | 'carPlate' = 'violation';
  searchCarPlate = signal('');
  loadingFines = signal(false);
  processingFine = signal<string | null>(null);

  ngOnInit(): void {}

  // ================= LOAD =================
  loadFines(): void {
    this.loadingFines.set(true);
  
    let violationNumber: string | undefined = undefined;
    let carPlate: string | undefined = undefined;
  
    if (this.selectedSearchType === 'violation') {
      violationNumber = this.searchViolationNumber() || undefined;
    } else {
      carPlate = this.searchCarPlate() || undefined;
    }
  
    this.fineService
      .searchFines(
        violationNumber,
        carPlate,
        this.searchIsPaid() ?? undefined,
        this.page(),
        this.pageSize
      )
      .subscribe({
        next: res => {
          this.loadingFines.set(false);
  
          if (res.success && res.data) {
            this.fines.set(res.data.items);
            this.totalRecords.set(res.data.totalCount);
          }
        },
        error: () => this.loadingFines.set(false)
      });
  }

  // ================= SEARCH =================
  search(): void {
    this.page.set(1);
    this.loadFines();
  }

  resetFilters(): void {
    this.searchViolationNumber.set('');
    this.searchCarPlate.set('');
    this.searchIsPaid.set(null);
  
    this.page.set(1);
    this.loadFines();
  }

  // ================= PAGINATION =================
  onPageChange(event: any) {
    const page = event.first / event.rows + 1;
    this.page.set(page);
    this.pageSize = event.rows;
    this.loadFines();
  }


  onSearchInput(value: string) {
    if (this.selectedSearchType === 'violation') {
      this.searchViolationNumber.set(value);
    } else {
      this.searchCarPlate.set(value);
    }
  }


  // ================= MARK PAID =================
  markFineAsPaid(violationNumber: string) {
    this.processingFine.set(violationNumber);

    this.fineService.markFineAsPaid(violationNumber).subscribe({
      next: res => {
        this.processingFine.set(null);

        if (res.success) {
          this.toast.add({
            severity: 'success',
            summary: 'Success',
            detail: 'Fine marked as paid'
          });

          // update UI
          this.fines.update(list =>
            list.map(f =>
              f.violationNumber === violationNumber
                ? { ...f, isPaid: true }
                : f
            )
          );
        }
      },
      error: err => {
        this.processingFine.set(null);
        this.toast.add({
          severity: 'error',
          summary: 'Error',
          detail: err.error?.message ?? 'Failed'
        });
      }
    });
  }

  // ================= FILE =================
  onFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files?.length) this.selectedFile.set(input.files[0]);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    const file = event.dataTransfer?.files[0];
    if (file) this.selectedFile.set(file);
  }

  uploadFines(): void {
    if (!this.selectedFile()) return;

    this.uploading.set(true);
    this.importResult.set(null);

    this.fineService.importFines(this.selectedFile()!).subscribe({
      next: res => {
        this.uploading.set(false);

        if (res.success && res.data) {
          this.importResult.set(res.data);
          this.selectedFile.set(null);

          this.toast.add({
            severity: 'success',
            summary: 'Import Complete',
            detail: `${res.data.newFinesAdded} new fines added`
          });

          this.loadFines();
        }
      },
      error: err => {
        this.uploading.set(false);
        this.toast.add({
          severity: 'error',
          summary: 'Error',
          detail: err.error?.message ?? 'Upload failed'
        });
      }
    });
  }
}
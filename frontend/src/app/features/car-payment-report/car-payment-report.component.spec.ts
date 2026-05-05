import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CarPaymentReportComponent } from './car-payment-report.component';

describe('CarPaymentReportComponent', () => {
  let component: CarPaymentReportComponent;
  let fixture: ComponentFixture<CarPaymentReportComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CarPaymentReportComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CarPaymentReportComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

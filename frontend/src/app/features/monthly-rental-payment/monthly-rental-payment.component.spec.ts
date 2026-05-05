import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MonthlyRentalPaymentComponent } from './monthly-rental-payment.component';

describe('MonthlyRentalPaymentComponent', () => {
  let component: MonthlyRentalPaymentComponent;
  let fixture: ComponentFixture<MonthlyRentalPaymentComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MonthlyRentalPaymentComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(MonthlyRentalPaymentComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

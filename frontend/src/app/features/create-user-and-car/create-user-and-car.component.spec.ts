import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CreateUserAndCarComponent } from './create-user-and-car.component';

describe('CreateUserAndCarComponent', () => {
  let component: CreateUserAndCarComponent;
  let fixture: ComponentFixture<CreateUserAndCarComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CreateUserAndCarComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CreateUserAndCarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

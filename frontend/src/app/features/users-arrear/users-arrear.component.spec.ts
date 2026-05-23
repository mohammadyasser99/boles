import { ComponentFixture, TestBed } from '@angular/core/testing';

import { UsersArrearComponent } from './users-arrear.component';

describe('UsersArrearComponent', () => {
  let component: UsersArrearComponent;
  let fixture: ComponentFixture<UsersArrearComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UsersArrearComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(UsersArrearComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';

import { EntranceFeesComponent } from './entrance-fees.component';

describe('EntranceFeesComponent', () => {
  let component: EntranceFeesComponent;
  let fixture: ComponentFixture<EntranceFeesComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EntranceFeesComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(EntranceFeesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

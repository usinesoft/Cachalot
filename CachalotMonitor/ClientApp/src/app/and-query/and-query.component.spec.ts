import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AndQueryComponent } from './and-query.component';

describe('AndQueryComponent', () => {
  let component: AndQueryComponent;
  let fixture: ComponentFixture<AndQueryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ AndQueryComponent ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(AndQueryComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

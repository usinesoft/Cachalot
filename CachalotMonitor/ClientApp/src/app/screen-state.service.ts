import { Injectable } from '@angular/core';
import { AndQuery } from './model/query';
import { DataScreenState, SchemaScreenState } from './model/state';

@Injectable({
  providedIn: 'root'
})
export class ScreenStateService {

  constructor() { }
  
  public data:DataScreenState = new  DataScreenState;

  public schema:SchemaScreenState = new  SchemaScreenState;

  public clearScreenState():void{
    this.data = new DataScreenState;
    this.schema = new SchemaScreenState;
  }

}

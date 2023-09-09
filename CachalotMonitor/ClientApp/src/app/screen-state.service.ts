import { Injectable } from "@angular/core";
import { DataScreenState, SchemaScreenState } from "./model/state";

@Injectable({
  providedIn: "root"
})
export class ScreenStateService {

  constructor() {}

  data = new DataScreenState;

  schema = new SchemaScreenState;

  clearScreenState(): void {
    this.data = new DataScreenState;
    this.schema = new SchemaScreenState;
  }

}

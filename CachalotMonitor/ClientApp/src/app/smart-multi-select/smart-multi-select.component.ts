import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';


@Component({
  selector: 'app-smart-multi-select',
  templateUrl: './smart-multi-select.component.html',
  styleUrls: ['./smart-multi-select.component.css']
})
export class SmartMultiSelectComponent implements OnInit {

  constructor() { }

  ngOnInit(): void {
  }

  // optional hint displayed under the control
  @Input()
  public hint: string | undefined;

  // optional label
  @Input()
  public label: string | undefined;

  @Input()
  public singleValue: boolean = false;

  @Input()
  public clearButton: boolean = false;

  // all values
  private _values: string[] = [];

  public get allValues(): string[] {
    return this._values;
  }



  // the model is either an array or a single value
  
  public get selection() : any {
    if(this.singleValue){
      return this.selectedValues.length > 0? this.selectedValues[0]:undefined;
    }
    else{
      return this.selectedValues;
    }    
  }

  public set selection(v : any) {
    if(Array.isArray(v)){
      this.selectedValues = v;
    }
    else{
      if(v){
        this.selectedValues = [v]; 
      }
      else{
        this.selectedValues = [];
      }
      
    }
  }
  


  @Input()
  public set allValues(v: string[]) {
    this._values = v;
    this.filteredValues = v;
  }

  // selected values
  private _selectedValues: string[] = [];

  public get selectedValues(): string[] {
    return this._selectedValues;
  }


  public clear(){
    console.log('clear was called');
    this._selectedValues = [];
    this.selectedValuesChange.emit(this._selectedValues);
  }

  @Input()
  public set selectedValues(v: string[]) {
    if (!Array.isArray(v)) {
      if(this._selectedValues.length == 0 || this._selectedValues[0] != v){
        this._selectedValues = [v];
        this.selectedValuesChange.emit(this._selectedValues);
      }
      
    }
    else {
      let filtered = v.filter(x => x); // remove empty values
      if (!this.arraysAreEqual(filtered, this._selectedValues)) {
        this._selectedValues = filtered;
        this.selectedValuesChange.emit(filtered);
      }
    }


  }

  @Output()
  public selectedValuesChange: EventEmitter<string[]> = new EventEmitter<string[]>();

  // filtered values (used if search enabled)
  public filteredValues: string[] = [];

  // options
  @Input()
  public isSingleValue: boolean = false;

  @Input()
  public canSelectAll: boolean = false;

  @Input()
  public canClearAll: boolean = false;

  @Input()
  public searchThreshold: number = 10;


  // search
  private _searchText: string | undefined;

  public get searchText(): string | undefined {
    return this._searchText;
  }

  public set searchText(v: string | undefined) {
    this._searchText = v;
    if (v) {
      this.filteredValues = this.allValues.filter(x => x.toLowerCase().indexOf(v) > -1)
    }
    else {
      this.filteredValues = [...this.allValues];
    }

  }


  public clearAll() {
    this.selectedValues = [];
  }

  public selectAll() {
    this._selectedValues = [];
    this.selectedValues = [...this.allValues]

  }


  private arraysAreEqual(a: string[] | undefined, b: string[] | undefined): boolean {

    if (!a && !b) {
      return true;
    }

    if (!a) {
      return false;
    }

    if (!b) {
      return false;
    }

    if (a.length != b.length) {
      return false;
    }

    for (let index = 0; index < a.length; index++) {
      const element1 = a[index];
      const element2 = b[index];

      if (element1 != element2) {
        return false;
      }

    }


    return true;
  }
}

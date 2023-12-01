import { Component, ElementRef, EventEmitter, Input, OnInit, Output, ViewChild } from "@angular/core";


@Component({
  selector: "app-smart-multi-select",
  templateUrl: "./smart-multi-select.component.html",
  styleUrls: ["./smart-multi-select.component.scss"]
})
export class SmartMultiSelectComponent implements OnInit {

  constructor() { }

  ngOnInit(): void {
  }

  // optional hint displayed under the control
  @Input()
  hint: string | undefined;

  // optional label
  @Input()
  label: string | undefined;

  @Input()
  singleValue = false;

  @Input()
  clearButton = false;

  @ViewChild('multiSelect') select: any;


  // all values
  private _values: string[] = [];

  get allValues(): string[] {
    return this._values;
  }


  // the model is either an array or a single value

  get selection(): any {
    if (this.singleValue) {
      return this.selectedValues.length > 0 ? this.selectedValues[0] : undefined;
    } else {
      return this.selectedValues;
    }
  }

  set selection(v: any) {
    if (Array.isArray(v)) {
      this.selectedValues = v;
    } else {
      if (v) {
        this.selectedValues = [v];
      } else {
        this.selectedValues = [];
      }

    }
  }


  @Input()
  set allValues(v: string[]) {
    this._values = v;
    this.filteredValues = v;
  }

  // selected values
  private _selectedValues: string[] = [];

  get selectedValues(): string[] {
    return this._selectedValues;
  }


  clear() {
    console.log("clear was called");
    this._selectedValues = [];
    this.selectedValuesChange.emit(this._selectedValues);
  }

  @Input()
  set selectedValues(v: string[]) {
    if (!Array.isArray(v)) {
      if (this._selectedValues.length == 0 || this._selectedValues[0] != v) {
        this._selectedValues = [v];
        this.selectedValuesChange.emit(this._selectedValues);
      }

    }
    else if (this.isSingleValue) {
      console.log('single value mode');

      let selected = [];

      if (this._selectedValues.length > 0) {
        let newElements = v.filter(x => !this._selectedValues.includes(x));
        console.log(newElements);
        selected = newElements;        
      }
      else{
        selected = v;
      }

      let newv = selected[0];
      let oldv = this._selectedValues[0]
      if(newv != oldv && newv){
        this._selectedValues = [newv];
        this.selectedValuesChange.emit(this._selectedValues);
        this.select.close();
      }
      

    }
    else {
      const filtered = v.filter(x => x); // remove empty values
      if (!this.arraysAreEqual(filtered, this._selectedValues)) {
        this._selectedValues = filtered;
        this.selectedValuesChange.emit(filtered);
      }
    }


  }

  @Output()
  selectedValuesChange = new EventEmitter<string[]>();

  // filtered values (used if search enabled)
  filteredValues: string[] = [];

  // options
  @Input()
  isSingleValue = false;

  @Input()
  canSelectAll = false;

  @Input()
  canClearAll = false;

  @Input()
  searchThreshold = 10;


  // search
  private _searchText: string | undefined;

  get searchText(): string | undefined {
    return this._searchText;
  }

  set searchText(v: string | undefined) {
    this._searchText = v;
    if (v) {
      const normalized = v.toLowerCase();
      console.log(`searching ${normalized}`);
      this.filteredValues = this.allValues.filter(x => x.toLowerCase().indexOf(normalized) > -1);
    } else {
      this.filteredValues = [...this.allValues];
    }

  }


  clearAll() {
    this.selectedValues = [];
  }

  selectAll() {
    this._selectedValues = [];
    this.selectedValues = [...this.allValues];

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

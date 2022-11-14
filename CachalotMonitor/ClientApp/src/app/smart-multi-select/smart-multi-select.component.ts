import { outputAst } from '@angular/compiler';
import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { MatFormFieldControl } from '@angular/material/form-field';

@Component({
  selector: 'app-smart-multi-select',
  templateUrl: './smart-multi-select.component.html',
  styleUrls: ['./smart-multi-select.component.css']
})
export class SmartMultiSelectComponent implements OnInit {

  constructor() { }

  ngOnInit(): void {
  }

  
  // all values
  private _values : string[] = [];
  
  public get values() : string[] {
    return this._values;
  }
  
  @Input()
  public set values(v : string[]) {
    this._values = v;
    this.filteredValues = v;
  }

  // selected values
  private _selectedValues : string[] = [];
  
  public get selectedValues() : string[] {
    return this._selectedValues;
  }

  public set selectedValues(v : string[]) {
    this._selectedValues = v;
    this.selectedValuesChange.emit(v);
  }

  @Output()
  public selectedValuesChange:EventEmitter<string[]> = new EventEmitter<string[]>();
  
  // filtered values (used if search enabled)
  public filteredValues:string[] = [];

  // options
  @Input()
  public isSingleValue:boolean = false;

  @Input()
  public canSelectAll:boolean = false;

  @Input()
  public canClearAll:boolean = false;

  @Input()
  public searchThreshold:number = 10;


  // search
  private _searchText : string|undefined;
  
  public get searchText() : string|undefined {
    return this._searchText;
  }

  public set searchText(v : string|undefined) {
    this._searchText = v;
    if(v){
      this.filteredValues = this.values.filter(x => x.toLowerCase().indexOf(v) > -1)
    }
    else{
      this.filteredValues = [...this.values];
    }
    
  }


  public clearAll(){
    this.selectedValues = [];
  }

  public selectAll(){
    this._selectedValues = [];
    this.selectedValues = [...this.values]
    
  }


}

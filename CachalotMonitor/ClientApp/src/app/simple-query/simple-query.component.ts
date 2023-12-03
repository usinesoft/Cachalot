import { Component, EventEmitter, Input, OnInit, Output } from "@angular/core";
import { SimpleQuery } from "../model/query";
import { QueryService } from "../query.service";

@Component({
  selector: "app-simple-query",
  templateUrl: "./simple-query.component.html",
  styleUrls: ["./simple-query.component.scss"]
})
export class SimpleQueryComponent implements OnInit {


  /*-----------data-----------------------*/


  private _query = new SimpleQuery;

  get query(): SimpleQuery {
    return this._query!;
  }

  @Input()
  set query(v: SimpleQuery | undefined) {
    if (v) {
      this._query = v;
      this.canSelectValues = v.possibleValues.length > 0;
      this.updateConfigForOperator();
      this.filteredValues = [...v.possibleValues];

      console.log("query input set");
      if (this._query.propertyName) {
        console.log("restaured query");
      } else {
        console.log("empty query");
      }
    }

  }

  @Output()
  queryChange = new EventEmitter<SimpleQuery>();


  private _collection: string | undefined;

  get collection(): string | undefined {
    return this._collection;
  }

  // The name of the collection (mandatory to retrieve query metadata)
  @Input()
  set collection(v: string | undefined) {
    this._collection = v;
  }

  // All the queryable properties in the collection
  @Input()
  properties: string[] = [];

  filteredValues: string[] = [];

  // adapter for smart-multiselect component
  get selectedProperties():string[]{
    if(!this.selectedProperty){
      return [];
    }
    
    return [this.selectedProperty];
  }
  
  set selectedProperties(value:string[]){
    if(value.length > 0){
      this.selectedProperty = value[0];
    }
    else{
      this.selectedProperty = undefined;
    }
  }

    
  get values(): string[] {
    return this._query.possibleValues;
  }

  get operators(): string[] {
    return this._query.availableOperators;
  }

  working = false;


  get selectedProperty(): string | undefined {
    return this._query.propertyName;
  }

  set selectedProperty(v: string | undefined) {
    
    if(v == this._query.propertyName)
      return;
    
      this._query.propertyName = v;
    this._query.values = [];

    // get metadata => data type => acceptable operators and values list if not too big (limited to 1000 values)
    if (this.collection && this.selectedProperty) {
      this.working = true;
      this.queryService.GetQueryMetadata(this.collection, this.selectedProperty).subscribe(md => {
          this._query.availableOperators = md.availableOperators.map(o => this.operatorToUnicode(o)!);
          this._query.possibleValues = [...md.possibleValues];
          this.filteredValues = [...md.possibleValues];
          this._query.propertyIsCollection = md.propertyIsCollection;
          this.operator = md.availableOperators[0];
          this.canSelectValues = md.possibleValues.length > 0;
          this.working = false;
          this._query.dataType = md.propertyType;
          this.queryChange.emit(this._query);
          console.log(
            `metadata received => data type = ${this._query.dataType} is collection = ${this._query.propertyIsCollection
            }`);
        },
        _err => {
          this.working = true;
        });
    }

  }


  clearAll() {
    console.log("clear called");
    this.selectedValues = [];
  }

  selectAll() {
    console.log("select all called");

  }


  multipleValuesAllowed = false;
  canSelectValues = false;

  // "is null" and "is not null" operators can not have values
  hasValue = true;


  get operator(): string | undefined {
    return this.operatorToUnicode(this._query.operator);
  }

  set operator(v: string | undefined) {
    this._query.operator = this.operatorFromUnicode(v);

    this.updateConfigForOperator();

    this.queryChange.emit(this._query);

    console.log(
      `operator changed => data type = ${this._query.dataType} is collection = ${this._query.propertyIsCollection
      } op = ${this.operator} multiple values = ${this.multipleValuesAllowed}`);
  }

  private updateConfigForOperator() {
    if (this._query.operator == "is null" || this._query.operator == "is not null") {
      this.hasValue = false;
    } else {
      this.hasValue = true;
    }

    if ((this._query.operator == "=" || this._query.operator == "!=") &&
      this._query.dataType != "SomeFloat" &&
      !this._query.propertyIsCollection) {
      this.multipleValuesAllowed = true;
    } else {
      this.multipleValuesAllowed = false;
    }
  }


  get selectedValues(): string[] {
    return this._query.values;
  }

  set selectedValues(v: string[]) {
    this._query.values = v.filter(v => v);
    this.queryChange.emit(this._query);
  }


  private _searchText: string | undefined;

  get searchText(): string | undefined {
    return this._searchText;
  }

  set searchText(v: string | undefined) {
    this._searchText = v;
    let normalized = v?.toLowerCase();
    if (normalized) {
      this.filteredValues = this.values.filter(x => x.toLowerCase().indexOf(normalized!) > -1);
    } else {
      this.filteredValues = [...this.values];
    }

  }


  get singleValue(): string | undefined {
    return this._query.values.length == 1 ? this._query.values[0] : undefined;
  }

  set singleValue(v: string | undefined) {
    if (v) {
      this._query.values = [v];
    } else {
      this._query.values = [];
    }

    this.queryChange.emit(this._query);
  }


  // Fancy characters for comparison operators
  operatorToUnicode(operator: string | undefined): string | undefined {

    if (!operator)
      return undefined;

    if (operator == "!=")
      return "\u2260";

    if (operator == ">=")
      return "\u2265";

    if (operator == "<=")
      return "\u2264";

    return operator;
  }

  operatorFromUnicode(operator: string | undefined): string | undefined {
    if (!operator)
      return undefined;


    if (operator == "\u2260")
      return "!=";

    if (operator == "\u2265")
      return ">=";

    if (operator == "\u2264")
      return "<=";

    return operator;
  }


  /*-----------initialization-----------------------*/

  constructor(private queryService: QueryService) {}

  ngOnInit(): void {
  }


}

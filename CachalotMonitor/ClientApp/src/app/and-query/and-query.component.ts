import { Component, EventEmitter, Input, OnInit, Output } from "@angular/core";
import { AndQuery, SimpleQuery } from "../model/query";

@Component({
  selector: "app-and-query",
  templateUrl: "./and-query.component.html",
  styleUrls: ["./and-query.component.scss"]
})
export class AndQueryComponent implements OnInit {


  /*-----------data-----------------------*/


  private _query = new AndQuery;

  get query(): AndQuery {
    return this._query!;
  }

  @Input()
  set query(v: AndQuery | undefined) {
    if (v) {
      this._query = v;

    }

  }

  @Output()
  queryChange = new EventEmitter<AndQuery>();


  // The name of the collection (mandatory to retrieve query metadata)
  private _collection: string | undefined;

  get collection(): string | undefined {
    return this._collection;
  }

  @Input()
  set collection(v: string | undefined) {
    this._collection = v;
  }


  // All the queryable properties in the collection
  @Input()
  properties: string[] = [];

  constructor() {}

  ngOnInit(): void {
    if (this._query.simpleQueries.length == 0) {
      this.newLine();
    }
  }


  newLine() {
    const nq = new SimpleQuery;
    this._query.simpleQueries.push(nq);
    this.queryChange.emit(this._query);
  }

  removeLine(q: SimpleQuery) {
    const index = this._query.simpleQueries.indexOf(q);
    this._query.simpleQueries.splice(index, 1);
    this.queryChange.emit(this._query);
  }

  queryChanged() {
    this.queryChange.emit(this._query);
  }

}

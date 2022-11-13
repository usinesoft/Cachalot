import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { AndQuery, SimpleQuery } from '../model/query';

@Component({
  selector: 'app-and-query',
  templateUrl: './and-query.component.html',
  styleUrls: ['./and-query.component.css']
})
export class AndQueryComponent implements OnInit {


   /*-----------data-----------------------*/


   private _query: AndQuery = new AndQuery;

   public get query(): AndQuery {
     return this._query!;
   }
 
   @Input()
   public set query(v: AndQuery|undefined) {
     if(v){
       this._query = v;
     }
     
   }
 
   @Output() queryChange = new EventEmitter<AndQuery>();


  // The name of the collection (mandatory to retrieve query metadata)
  private _collection:string|undefined;
  
  public get collection():string | undefined{
    return this._collection;
  } 

  @Input()
  public set collection(v: string | undefined) {
    this._collection = v;
  }

  

  // All the queryable properties in the collection
  @Input()
  public properties: string[] = [];

  constructor() { }

  ngOnInit(): void {
    if(this._query.simpleQueries.length == 0){
      this.newLine();
    }
  }

  public newLine(){
    this._query.simpleQueries.push(new SimpleQuery)
  }

  public removeLine(q:SimpleQuery){
    var index = this._query.simpleQueries.indexOf(q);
    this._query.simpleQueries.splice(index, 1);    
  }

}

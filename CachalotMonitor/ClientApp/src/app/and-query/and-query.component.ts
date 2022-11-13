import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { AndQuery } from '../model/query';

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

  constructor() { }

  ngOnInit(): void {
  }

}

import { Component, Input, OnInit } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-collection-card',
  templateUrl: './collection-card.component.html',
  styleUrls: ['./collection-card.component.css']
})
export class CollectionCardComponent implements OnInit {


  @Input()
  name:string|undefined;

  @Input()
  items:Number|undefined;
 
  @Input()
  layout:string|undefined;

  @Input()
  eviction:string|undefined;
  
  @Input()
  fullText:boolean|undefined;

  

  constructor(private router:Router) { }

  ngOnInit(): void {
  }


  public viewSchema(collection:string|undefined):void{
    this.router.navigate(["schema", collection]);
  }

}

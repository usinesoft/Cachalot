import { Component, Input, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ScreenStateService } from '../screen-state.service';

@Component({
  selector: 'app-collection-card',
  templateUrl: './collection-card.component.html',
  styleUrls: ['./collection-card.component.scss']
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

  

  constructor(private router:Router, private stateService:ScreenStateService) { }

  ngOnInit(): void {
  }


  public viewSchema(collection:string|undefined):void{

    this.stateService.schema.collectionName = collection;

    this.router.navigate(["schema"]);
  }

  public viewData(collection:string|undefined):void{

    if(this.stateService.data.collectionName != collection){
      this.stateService.data.collectionName = collection;
      this.stateService.data.currentQuery = undefined;
      
      this.stateService.data.visibleColumns = [];


    }
    

    this.router.navigate(["data"]);
  }


}

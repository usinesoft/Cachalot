import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Params } from '@angular/router';
import { CollectionSummary } from '../model/connection-data';
import { Schema } from '../model/schema';
import { MonitoringService } from '../monitoring.service';

@Component({
  selector: 'app-schema',
  templateUrl: './schema.component.html',
  styleUrls: ['./schema.component.css']
})
export class SchemaComponent implements OnInit {

  constructor(private monitoringService:MonitoringService, private route: ActivatedRoute) { }

  public collections:string[] = [];

  private _selectedCollection:string|undefined;

  public set selectedCollection(value:string|undefined){
    this._selectedCollection = value;
    if(value){
      this.schema = this.monitoringService. clusterInfo?.schema.find(s=>s.collectionName == value);
      this.summary = this.monitoringService.clusterInfo?.collectionsSummary.find(s=>s.name == value);
    }
    
  }

  public get selectedCollection():string|undefined{
    return this._selectedCollection;    
  }

  schema:Schema|undefined;

  summary:CollectionSummary|undefined;
  
  ngOnInit(): void {
     this.collections = this.monitoringService.clusterInfo?.schema.map(s=> s.collectionName) ?? [];
     console.log(this.collections);

     this.route.params.subscribe((params: Params) => this.selectedCollection = params['collection']);
  }

}

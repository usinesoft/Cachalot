import { Component, OnInit } from '@angular/core';
import { CollectionSummary } from '../model/connection-data';
import { SimpleQuery } from '../model/query';
import { Schema } from '../model/schema';
import { MonitoringService } from '../monitoring.service';
import { QueryService } from '../query.service';

@Component({
  selector: 'app-data',
  templateUrl: './data.component.html',
  styleUrls: ['./data.component.css']
})
export class DataComponent implements OnInit {

  public collections:string[] = [];

  private _selectedCollection:string|undefined;

  public set selectedCollection(value:string|undefined){
    this._selectedCollection = value;
    if(value){
      this.schema = this.monitoringService. clusterInfo?.schema.find(s=>s.collectionName == value);
      this.summary = this.monitoringService.clusterInfo?.collectionsSummary.find(s=>s.name == value);
      this.properties = this.schema?.serverSide.map(x=>x.name) ?? [];
    }
    
  }

  public get selectedCollection():string|undefined{
    return this._selectedCollection;    
  }

  schema:Schema|undefined;

  summary:CollectionSummary|undefined;

  properties:string[] = [];
  
  
  constructor(private monitoringService:MonitoringService, private queryService:QueryService) { }

  ngOnInit(): void {
    this.collections = this.monitoringService.clusterInfo?.schema.map(s=> s.collectionName) ?? [];
  }

  sql:string|undefined;

  private _currentQuery : SimpleQuery|undefined;
  public get currentQuery() : SimpleQuery|undefined {
    return this._currentQuery;
  }
  public set currentQuery(v : SimpleQuery|undefined) {
    this._currentQuery = v;
    this.sql = `${this._currentQuery?.propertyName} ${this._currentQuery?.operator} ${this._currentQuery?.values[0]}`
  }
  

}

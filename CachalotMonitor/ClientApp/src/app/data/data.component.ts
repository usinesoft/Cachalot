import { Component, OnInit } from '@angular/core';
import { CollectionSummary } from '../model/connection-data';
import { AndQuery, SimpleQuery } from '../model/query';
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

    var nq = new AndQuery;
    nq.simpleQueries.push(new SimpleQuery);
    this._currentQuery = nq;

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


  data:any[] = [];

  private _currentQuery : AndQuery|undefined;
  public get currentQuery() : AndQuery|undefined {
    return this._currentQuery;
  }
  public set currentQuery(v : AndQuery|undefined) {
    console.log('query changed');
    this._currentQuery = v;
    
    this.queryService.GetAsSql(this.selectedCollection!, this._currentQuery!).subscribe(data=>{
      
      let oldSql = this.sql;
      this.sql = data.sql;

      if(data.sql && data.sql != oldSql){
        this.queryService.ExecuteQuery(data.sql).subscribe(d=>{
          if(d.json){
            this.data = JSON.parse( d.json);
            console.log(this.data.length + ' items received');
          }
          
        });
      }
    }, err=> this.sql = err);
    
    
  }
  

}

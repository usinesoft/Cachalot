import { LayoutModule } from '@angular/cdk/layout';
import { Component, OnInit } from '@angular/core';
import { CollectionSummary } from '../model/connection-data';
import { ExecutionPlan, QueryExecutionPlan } from '../model/execution-plan';
import { AndQuery, SimpleQuery } from '../model/query';
import { Schema } from '../model/schema';
import { MonitoringService } from '../monitoring.service';
import { QueryService } from '../query.service';
import { ScreenStateService } from '../screen-state.service';

@Component({
  selector: 'app-data',
  templateUrl: './data.component.html',
  styleUrls: ['./data.component.css']
})
export class DataComponent implements OnInit {


  private differentValues(a: any, b: any) {
    var s1 = JSON.stringify(a);
    var s2 = JSON.stringify(b);

    return (s1 !== s2);
  }

  ///////////////////////////////////////////////////
  // dynamic data not stored in the state service

  public sql: string | undefined;

  public data: any[] = [];

  public schema: Schema | undefined;

  public summary: CollectionSummary | undefined;

  // all the queriable properties of the current collection
  public properties: string[] = [];

  // all collections
  public collections: string[] = [];

  // all ordered indexes
  public orderByProperties: string[] = [];


  fullTextQuery: string | undefined;


  public get visibleColumns(): string[] {
    return this.stateService.data.visibleColumns;
  }

  public set visibleColumns(v: string[]) {
    if (this.differentValues(v, this.stateService.data.visibleColumns)) {
      this.stateService.data.visibleColumns = v.filter(v => v);
    }

  }

  public search() {
    this.getData(true);
  }


  // single value but the data-binding needs a collection
  public get take(): string[] {
    return [this.stateService.data.currentQuery!.take];
  }

  public set take(v: string[]) {
    if (v.length == 1) {
      this.stateService.data.currentQuery!.take = v[0];
      this.getData();
    }
  }

  public get descending(): boolean {
    return this.stateService.data.currentQuery?.descending ?? false;
  }
  public set descending(v: boolean) {
    this.stateService.data.currentQuery!.descending = v;
    this.getData();
  }

  public get selectedCollection(): string | undefined {
    return this.stateService.data.collectionName;
  }

  public set selectedCollection(value: string | undefined) {

    console.log(this.stateService.data.collectionName + '-->' + value);
    if (this.stateService.data.collectionName != value && value) {

      this.stateService.data.collectionName = value;
      this.updateOnCollectionChange(value);

      var nq = new AndQuery;
      nq.simpleQueries.push(new SimpleQuery);
      this.currentQuery = nq;
    }



  }


  private init(collection:string){
    this.schema = this.monitoringService.clusterInfo?.schema.find(s => s.collectionName == collection);
    this.summary = this.monitoringService.clusterInfo?.collectionsSummary.find(s => s.name == collection);
    this.properties = this.schema?.serverSide.map(x => x.name) ?? [];
    this.orderByProperties = this.schema?.serverSide.filter(x => x.indexType == 'Ordered').map(x => x.name) ?? [];
    this.fullTextQuery = undefined;
    if(this.visibleColumns.length == 0){
      this.visibleColumns = this.properties.slice(0, 10);
    }
    
  }

  private updateOnCollectionChange(collection: string | undefined) {
    if (collection) {

      this.init(collection);

      console.log('update on collection changed');
    
      this.visibleColumns = this.properties.slice(0, 10);
    }

  }

  


  // the one selected for result ordering
  public get orderBy(): string[] {
    if (this.stateService.data.currentQuery?.orderBy) {
      return [this.stateService.data.currentQuery.orderBy];
    }
    return [];
  }

  public set orderBy(v: string[]) {

    this.stateService.data.currentQuery!.orderBy = v[0];
    this.getData();

  }

  constructor(private monitoringService: MonitoringService, private queryService: QueryService, private stateService: ScreenStateService) { }

  ngOnInit(): void {

    this.collections = this.monitoringService.clusterInfo?.schema.map(s => s.collectionName) ?? [];

    if (this.selectedCollection) {
      this.init(this.selectedCollection);
      this.getData();
    }
    else {
      this.selectedCollection = this.collections[0];
    }
  }

  public asJson(x: any): string {
    return JSON.stringify(this.cleanup(x), null, 2);
  }

  // when displaying as json remove tha properties starting with # which are only used for display
  public cleanup(x: any): any {
    var cloneObj = { ...x };
    delete cloneObj['#'];
    delete cloneObj['#json'];
    return cloneObj;
  }


  public get currentQuery(): AndQuery | undefined {
    return this.stateService.data.currentQuery;
  }

  public set currentQuery(v: AndQuery | undefined) {

    this.stateService.data.currentQuery = v;
    this.getData();
  }

  public exportJson() {

    this.queryService.DownloadAsStream(this.sql, this.fullTextQuery).subscribe(data => {
      console.log('result:' + data);
    });
  }

  lastQueryId:string|undefined;

  clientTimeInMilliseconds:number = 0;

  executionPlan:ExecutionPlan|undefined;
  
  public get queryPlan():QueryExecutionPlan|undefined{
    var plans = this.executionPlan?.queryPlans;
    if(plans){
      return plans[0];
    }
    return undefined;
  }
  
  private getData(force: boolean = false) {
    this.queryService.GetAsSql(this.selectedCollection!, this.currentQuery!).subscribe(data => {

      let oldSql = this.sql;
      this.sql = data.sql;

      let shouldFetchData = force; // if forced
      if (!shouldFetchData) { // if sql changed
        shouldFetchData = data.sql != undefined && data.sql != oldSql;
      }

      if (shouldFetchData) {
        this.executionPlan = undefined;
        
        this.queryService.ExecuteQuery(data.sql, this.fullTextQuery).subscribe(d => {
          if (d.json) {
            this.data = JSON.parse(d.json);

            // add line numbers and formatting information
            for (let index = 0; index < this.data.length; index++) {
              let element = this.data[index];
              // add line number
              element['#'] = index;
              // add expansion flag for display
              element['#json'] = false;

            }
            console.log(this.data.length + ' items received');
            this.clientTimeInMilliseconds = d.clientTimeInMilliseconds;
            this.lastQueryId = d.queryId;
            console.log(`client time (ms)= ${this.clientTimeInMilliseconds} query id= ${this.lastQueryId}`);

            // wait one second before asking for the execution plan as the @ACTIVITY table is updated asynchronously
            setTimeout(() => {
              this.queryService.GetExecutionPlan(this.lastQueryId!).subscribe({
                next:(data) => this.executionPlan = data
              });
            }, 1000);
          }

        });
      }
    }, err => this.sql = err);

  }


  // display json detail for line
  public switchDisplay(r: any) {
    let before = r['#json'];
    r['#json'] = !before;
    console.log('display json = ' + r['#json']);
  }

  ignoreLimit: boolean = false;

  working: boolean = false;

  public onFileSelected(event: any) {
    const file: File = event.target.files[0];

    if (file && this.selectedCollection) {
      this.fileName = file.name;


      this.working = true;
      this.queryService.UploadFile(file, this.selectedCollection).subscribe(data => {
        this.working = false;
        console.log('done');
      }, err => {
        this.working = false;
        console.log('eror:' + err);
      });
    }


  }

  fileName: string | undefined;

}

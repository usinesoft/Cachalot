import { Component, ElementRef, OnInit, ViewChild } from "@angular/core";
import { MatDialog } from "@angular/material/dialog";
import { ConnectionService } from "../connection.service";
import { ExecutionPlanComponent } from "../execution-plan/execution-plan.component";
import { CollectionSummary } from "../model/connection-data";
import { AndQuery, SimpleQuery } from "../model/query";
import { Schema } from "../model/schema";
import { MonitoringService } from "../monitoring.service";
import { QueryService } from "../query.service";
import { ScreenStateService } from "../screen-state.service";
import { HelpService } from "../help.service";
import { MatSnackBar } from "@angular/material/snack-bar";

@Component({
  selector: "app-data",
  templateUrl: "./data.component.html",
  styleUrls: ["./data.component.scss"]
})
export class DataComponent implements OnInit {

  constructor(private monitoringService: MonitoringService,
    private queryService: QueryService,
    private stateService: ScreenStateService,
    public dialog: MatDialog,
    public helpService: HelpService,
    private snackBar: MatSnackBar,
    private connectionService: ConnectionService) { }


  public get isAdmin() {
    return this.connectionService.isAdmin;
  }

  private differentValues(a: any, b: any) {
    const s1 = JSON.stringify(a);
    const s2 = JSON.stringify(b);

    return (s1 !== s2);
  }

  ///////////////////////////////////////////////////
  // dynamic data not stored in the state service

  sql: string | undefined;

  data: any[] = [];

  schema: Schema | undefined;

  summary: CollectionSummary | undefined;

  // all the queriable properties of the current collection
  properties: string[] = [];

  // all collections
  collections: string[] = [];

  // all ordered indexes
  orderByProperties: string[] = [];


  fullTextQuery: string | undefined;


  get visibleColumns(): string[] {
    return this.stateService.data.visibleColumns;
  }

  set visibleColumns(v: string[]) {
    if (this.differentValues(v, this.stateService.data.visibleColumns)) {
      this.stateService.data.visibleColumns = v.filter(v => v);
    }

  }

  search() {
    this.getData(true);
  }


  // single value but the data-binding needs a collection
  get take(): string[] {
    return [this.stateService.data.currentQuery!.take];
  }

  set take(v: string[]) {
    if (v.length == 1) {
      this.stateService.data.currentQuery!.take = v[0];
      this.getData();
    }
  }

  get descending(): boolean {
    return this.stateService.data.currentQuery?.descending ?? false;
  }

  set descending(v: boolean) {
    this.stateService.data.currentQuery!.descending = v;
    this.getData();
  }

  get selectedCollection(): string | undefined {
    return this.stateService.data.collectionName;
  }

  set selectedCollection(value: string | undefined) {

    console.log(this.stateService.data.collectionName + "-->" + value);
    if (this.stateService.data.collectionName != value && value) {

      this.stateService.data.collectionName = value;
      this.updateOnCollectionChange(value);

      const nq = new AndQuery;
      nq.simpleQueries.push(new SimpleQuery);
      this.currentQuery = nq;
    }


  }


  private init(collection: string) {
    this.schema =
      this.monitoringService.clusterInformation.getValue()?.schema.find(s => s.collectionName == collection);
    this.summary = this.monitoringService.clusterInformation.getValue()?.collectionsSummary
      .find(s => s.name == collection);
    this.properties = this.schema?.serverSide.map(x => x.name) ?? [];
    this.orderByProperties = this.schema?.serverSide.filter(x => x.indexType == "Ordered").map(x => x.name) ?? [];
    this.fullTextQuery = undefined;
    if (this.visibleColumns.length == 0) {
      this.visibleColumns = this.properties.slice(0, 10);
    }

  }

  private updateOnCollectionChange(collection: string | undefined) {
    if (collection) {

      this.init(collection);

      console.log("update on collection changed");

      this.visibleColumns = this.properties.slice(0, 10);
    }

  }


  // the one selected for result ordering
  get orderBy(): string[] {
    if (this.stateService.data.currentQuery?.orderBy) {
      return [this.stateService.data.currentQuery.orderBy];
    }
    return [];
  }

  set orderBy(v: string[]) {

    this.stateService.data.currentQuery!.orderBy = v[0];
    this.getData();

  }



  refresh(): void {
    this.getData(true);
  }

  ngOnInit(): void {

    this.fetchingData = false;
    this.collections = this.monitoringService.clusterInformation.getValue()?.schema.map(s => s.collectionName) ?? [];

    if (this.selectedCollection) {
      this.init(this.selectedCollection);
      this.getData();
    } else {
      this.selectedCollection = this.collections[0];
    }
  }

  asJson(x: any): string {
    return JSON.stringify(this.cleanup(x), null, 2);
  }

  // when displaying as json remove tha properties starting with # which are only used for display
  cleanup(x: any): any {
    const cloneObj = { ...x };
    delete cloneObj["#"];
    delete cloneObj["#json"];
    return cloneObj;
  }


  get currentQuery(): AndQuery | undefined {
    return this.stateService.data.currentQuery;
  }

  set currentQuery(v: AndQuery | undefined) {

    this.stateService.data.currentQuery = v;
    this.getData();
  }

  onEnter() {
    this.refresh();
  }

  exportJson() {

    let sql = this.sql;
    if (this.ignoreLimit) {
      const start = sql?.indexOf("TAKE");
      if (start != -1) {
        sql = sql?.substring(0, start);
      }
    }

    console.log(`SQL for download:${sql}`);

    this.queryService.DownloadAsStream(sql, this.fullTextQuery).subscribe(data => {
      console.log(`result:${data}`);
    });
  }

  lastQueryId: string | undefined;

  clientTimeInMilliseconds = 0;


  openPlan(): void {
    this.dialog.open(ExecutionPlanComponent,
      {
        data: {
          queryId: this.lastQueryId,
          clientTimeInMilliseconds: this.clientTimeInMilliseconds
        }
      });
  }


  public deleteResult() {



    if (this.sql && this.sql.toLowerCase().includes('where')) {
      this.queryService.ExecuteDelete(this.sql).subscribe(data => {
        this.snackBar.open(`${data.itemsChanged} items deleted`, "", { duration: 3000, panelClass: "green-snackbar" });
      })
    }
    else {
      this.snackBar.open('Empty queries (no WHERE clause) are not allowed ', "", { duration: 3000, panelClass: "red-snackbar" });
    }

    this.confirmDeleteMode = false;

  }

  fetchingData:boolean = false;


  private getData(force: boolean = false) {

    // avoid recursive calls after control update
    if(this.fetchingData){
      return;
    }

    this.currentQuery!.fullTextQuery = this.fullTextQuery;

    this.fetchingData = true;
    this.queryService.Execute(this.selectedCollection!, this.currentQuery!).subscribe(
      data => {

        this.sql = data.sql;
        if (data.json) {
          this.data = JSON.parse(data.json);

          // add line numbers and formatting information
          for (let index = 0; index < this.data.length; index++) {
            const element = this.data[index];
            // add line number
            element["#"] = index;
            // add expansion flag for display
            element["#json"] = false;

          }
          console.log(this.data.length + " items received");
          this.clientTimeInMilliseconds = data.clientTimeInMilliseconds;
          this.lastQueryId = data.queryId;
          console.log(`client time (ms)= ${this.clientTimeInMilliseconds} query id= ${this.lastQueryId}`);

          

        }
        this.fetchingData = false;

      },
      error => {
        this.sql = error;
        this.fetchingData = false;
      }

    );



  }


  // display json detail for line
  switchDisplay(r: any) {
    const before = r["#json"];
    r["#json"] = !before;
    console.log(`display json = ${r["#json"]}`);
  }

  ignoreLimit = false;

  working = false;

  onFileSelected(event: any) {

    this.fileToUpload = event.target.files[0];

    if (this.fileToUpload && this.selectedCollection) {
      this.fileName = this.fileToUpload.name;

    }

  }


  confirmDeleteMode: boolean = false;

  fileToUpload: File | undefined;

  @ViewChild('fileUpload')
  upload: ElementRef | undefined;

  uploadFile() {
    this.working = true;
    this.queryService.UploadFile(this.fileToUpload!, this.selectedCollection!).subscribe(data => {
      this.working = false;
      this.clearUpload();
      this.snackBar.open("Upload successfull", "", { duration: 2000, panelClass: "green-snackbar" });
    },
      err => {
        this.working = false;
        this.clearUpload();
        this.snackBar.open(err.errorMessage ?? "Error while uploading", "", { duration: 3000, panelClass: "red-snackbar" });
      });

  }

  clearUpload() {
    this.fileName = undefined;
    this.fileToUpload = undefined;
    this.upload!.nativeElement.value = "";
  }

  fileName: string | undefined;

}

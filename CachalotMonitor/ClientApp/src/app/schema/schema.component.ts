import { Component, OnInit } from "@angular/core";
import { ConnectionService } from "../connection.service";
import { CollectionSummary } from "../model/connection-data";
import { Schema, SchemaUpdateRequest, ServerSide } from "../model/schema";
import { MonitoringService } from "../monitoring.service";
import { ScreenStateService } from "../screen-state.service";
import { HelpService } from "../help.service";

@Component({
  selector: "app-schema",
  templateUrl: "./schema.component.html",
  styleUrls: ["./schema.component.scss"]
})
export class SchemaComponent implements OnInit {

  constructor(private monitoringService: MonitoringService, private stateService: ScreenStateService, private connectionService:ConnectionService, public helpService:HelpService) {}

  public get isAdmin(){
    return this.connectionService.isAdmin;
  }


  collections: string[] = [];

  serverSide: string[] = [];

  private editedProperty: string | undefined;


  get editedProperties(): string[] {
    return this.editedProperty ? [this.editedProperty] : [];
  }

  set editedProperties(v: string[]) {
    if (v.length == 1) {
      this.editedProperty = v[0];

      this.selectedIndexType = this.schema?.serverSide.find(x => x.name == this.editedProperty)?.indexType;

      console.log(`index type=${this.selectedIndexType} for ${this.editedProperty}`);
    } else {
      this.editedProperty = undefined;
    }
  }


  indexTypes: string[] = ["None", "Dictionary", "Ordered"];

  private _selectedIndexType: string | undefined;

  get selectedIndexType(): string | undefined {
    return this._selectedIndexType;
  }

  set selectedIndexType(value: string | undefined) {
    this._selectedIndexType = value;
  }

  editMode = false;

  // system tables like @ACTIVITY can not be modified
  canBeEdited = false;

  set selectedCollection(value: string | undefined) {
    this.stateService.schema.collectionName = value;

    if (value?.startsWith("@")) {
      this.canBeEdited = false;
    } else {
      this.canBeEdited = true;
    }


    if (value) {
      this.schema = this.monitoringService.clusterInformation.getValue()?.schema.find(s => s.collectionName == value);
      this.summary =
        this.monitoringService.clusterInformation.getValue()?.collectionsSummary.find(s => s.name == value);
      this.serverSide = this.schema?.serverSide.map(x => x.name) ?? [];
    }

  }

  updateAfterSave() {
    this.monitoringService.updateOnce().subscribe(data => {
      this.schema = data.schema.find(s => s.collectionName == this.selectedCollection);
      this.summary = this.monitoringService.clusterInformation.getValue()?.collectionsSummary
        .find(s => s.name == this.selectedCollection);
      this.serverSide = this.schema?.serverSide.map(x => x.name) ?? [];
    });

  }

  get selectedCollection(): string | undefined {
    return this.stateService.schema.collectionName;
  }

  working = false;

  confirmDictionary(prop:ServerSide){
    prop.confirmMode = true;
    prop.upgradeTo = 'Dictionary';
  }

  confirmOrdered(prop:ServerSide){
    prop.confirmMode = true;
    prop.upgradeTo = 'Ordered';
  }

  upgrade(prop: ServerSide) {
    if (prop.upgradeTo == "Dictionary") {
      prop.indexType = "Dictionary";
    } else {
      prop.indexType = "Ordered";
    }

    this.editedProperty = prop.name;
    this._selectedIndexType = prop.indexType;

    this.updateSchema();
  }

  updateSchema() {
    const request: SchemaUpdateRequest = {
      collectionName: this.selectedCollection!,
      propertyName: this.editedProperty!,
      indexType: this._selectedIndexType!
    };

    this.working = true;
    this.monitoringService.updateSchema(request).subscribe(data => {
        this.working = false;
        this.editMode = false;
        this.updateAfterSave();
      },
      err => this.working = false);
  }

  schema: Schema | undefined;

  summary: CollectionSummary | undefined;

  ngOnInit(): void {
    this.collections = this.monitoringService.clusterInformation.getValue()?.schema.map(s => s.collectionName) ?? [];

    if (this.stateService.schema.collectionName) {
      this.selectedCollection = this.stateService.schema.collectionName;
    } else {
      this.selectedCollection = this.collections[0];
    }


  }

}

import { Component, OnInit } from "@angular/core";
import { ClusterInformation, CollectionSummary } from "../model/connection-data";
import { MonitoringService } from "../monitoring.service";

@Component({
  selector: "app-collections",
  templateUrl: "./collections.component.html",
  styleUrls: ["./collections.component.scss"]
})
export class CollectionsComponent implements OnInit {

  constructor(private service: MonitoringService) {}

  cluster: ClusterInformation | null = null;

  identifyCollection(index: Number, collection: CollectionSummary) {
    return collection.name;
  }

  ngOnInit(): void {
    this.service.clusterInformation.subscribe(data => {
      this.cluster = data;
    });
  }

}
